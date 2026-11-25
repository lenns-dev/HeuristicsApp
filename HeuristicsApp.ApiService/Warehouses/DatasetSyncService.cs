using System.IO.Compression;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace HeuristicsApp.ApiService.Warehouses;

/// <summary>
/// Replica la carpeta <see cref="WarehouseDatasetOptions.Path"/> de un repositorio GitHub en la
/// carpeta local de instancias: descarga lo nuevo o modificado y borra lo que ya no existe en remoto.
///
/// 1. Fija el commit actual de la rama (todas las lecturas usan ese commit: sin carreras con pushes).
/// 2. Lista el árbol remoto y compara cada fichero por su hash de blob git (SHA-1), sin descargar nada.
/// 3. Descarga los cambios: uno a uno si son pocos, o el zip del commit si son muchos (límite de peticiones).
///    Cada fichero se verifica contra su hash y se sustituye de forma atómica.
/// 4. Borra los ficheros locales que no están en remoto y reindexa el proveedor.
///
/// Solo hay una sincronización a la vez; corre en segundo plano y expone su estado en <see cref="Status"/>.
/// </summary>
public sealed class DatasetSyncService : IDisposable
{
    private const string TempSuffix = ".sync-tmp";
    private static readonly byte[] LfsPointerPrefix = "version https://git-lfs"u8.ToArray();

    private readonly WarehouseDatasetOptions _options;
    private readonly IWarehouseProvider _warehouses;
    private readonly IHostApplicationLifetime _lifetime;
    private readonly ILogger<DatasetSyncService> _logger;
    private readonly HttpClient _http;

    private readonly Lock _gate = new();
    private DatasetStatus _status;
    private Task _running = Task.CompletedTask;

    public DatasetSyncService(
        IOptions<WarehouseDatasetOptions> options,
        IWarehouseProvider warehouses,
        IHostApplicationLifetime lifetime,
        ILogger<DatasetSyncService> logger)
    {
        _options = options.Value;
        _warehouses = warehouses;
        _lifetime = lifetime;
        _logger = logger;

        // Cliente propio, fuera de IHttpClientFactory: el handler de resiliencia por defecto de
        // ServiceDefaults corta a los 30 s, y el zip de un dataset grande tarda bastante más.
        _http = new HttpClient(new SocketsHttpHandler
        {
            AutomaticDecompression = DecompressionMethods.All,
            PooledConnectionLifetime = TimeSpan.FromMinutes(10),
        })
        {
            BaseAddress = new Uri("https://api.github.com/"),
            Timeout = Timeout.InfiniteTimeSpan,
        };
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("HeuristicsApp-DatasetSync");
        _http.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");

        var o = _options;
        _status = new DatasetStatus(
            Configured: o.IsConfigured,
            Repository: o.IsConfigured ? $"{o.Owner}/{o.Repo}" : null,
            Branch: string.IsNullOrWhiteSpace(o.Branch) ? null : o.Branch,
            Commit: null,
            RemotePath: o.RemotePath,
            LocalPath: warehouses.Root,
            State: "idle",
            DryRun: false,
            Message: o.IsConfigured ? null : "Falta configurar WarehouseDataset:Owner y WarehouseDataset:Repo en la API.",
            Changes: null,
            Downloaded: 0,
            StartedAt: null,
            FinishedAt: null);
    }

    public DatasetStatus Status
    {
        get { lock (_gate) return _status; }
    }

    /// <summary>Lanza una sincronización (o solo la comprobación si <paramref name="dryRun"/>); false si ya hay una en curso.</summary>
    public bool TryStart(bool dryRun)
    {
        lock (_gate)
        {
            if (!_running.IsCompleted) return false;

            _status = _status with
            {
                State = "checking",
                DryRun = dryRun,
                Message = "Consultando el repositorio…",
                Changes = null,
                Downloaded = 0,
                StartedAt = DateTimeOffset.Now,
                FinishedAt = null,
            };
            _running = Task.Run(() => RunAsync(dryRun, _lifetime.ApplicationStopping));
            return true;
        }
    }

    public void Dispose() => _http.Dispose();

    private async Task RunAsync(bool dryRun, CancellationToken ct)
    {
        var o = _options;
        var touchedDisk = false;
        try
        {
            var branch = string.IsNullOrWhiteSpace(o.Branch) ? await GetDefaultBranchAsync(ct) : o.Branch;
            var commit = await GetCommitShaAsync(branch, ct);
            Update(s => s with { Branch = branch, Commit = commit[..7], Message = "Listando ficheros remotos…" });

            var remote = await ListRemoteAsync(commit, ct);
            if (remote.Count == 0)
                throw new InvalidOperationException(
                    $"La carpeta '{o.RemotePath}' no existe o está vacía en {o.Owner}/{o.Repo}@{branch}. No se ha modificado nada.");

            Update(s => s with { Message = $"Comparando {remote.Count} ficheros remotos con la carpeta local…" });
            var root = _warehouses.Root;
            Directory.CreateDirectory(root);
            var plan = Compare(root, remote, ct);
            Update(s => s with { Changes = plan.Changes });

            if (dryRun || !plan.Changes.HasChanges)
            {
                Finish("succeeded", plan.Changes.HasChanges
                    ? "Comprobación terminada: hay cambios pendientes de sincronizar."
                    : "La carpeta local ya está al día.");
                return;
            }

            touchedDisk = true;
            Update(s => s with { State = "downloading", Message = $"Descargando {plan.Downloads.Count} ficheros…" });
            if (plan.Downloads.Count <= o.IndividualDownloadLimit)
                await DownloadIndividuallyAsync(commit, root, plan.Downloads, ct);
            else
                await DownloadFromZipAsync(commit, root, plan.Downloads, ct);

            Update(s => s with { State = "applying", Message = "Eliminando ficheros que ya no están en el repositorio…" });
            foreach (var rel in plan.Deletions)
                File.Delete(FullPath(root, rel));
            RemoveEmptyDirectories(root);

            _warehouses.Reload();
            Finish("succeeded", $"Sincronizado con {o.Owner}/{o.Repo}@{branch} ({commit[..7]}).");
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            Finish("failed", "Sincronización interrumpida al detener la API.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Warehouse dataset sync failed.");
            Finish("failed", ex.Message);
        }
        finally
        {
            // Los ficheros ya sustituidos se quedan (cada uno es atómico): el índice debe reflejarlos.
            // Repetir la sincronización completa lo que faltara.
            if (touchedDisk) _warehouses.Reload();
        }
    }

    private sealed record RemoteFile(string RepoPath, string Sha, long Size);

    private sealed record SyncPlan(List<(string Rel, RemoteFile File)> Downloads, List<string> Deletions, DatasetChanges Changes);

    private static SyncPlan Compare(string root, Dictionary<string, RemoteFile> remote, CancellationToken ct)
    {
        var local = Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .ToDictionary(f => Path.GetRelativePath(root, f).Replace('\\', '/'), f => f, StringComparer.OrdinalIgnoreCase);

        var downloads = new List<(string Rel, RemoteFile File)>();
        int unchanged = 0, added = 0, updated = 0;
        foreach (var (rel, file) in remote)
        {
            ct.ThrowIfCancellationRequested();
            if (!local.TryGetValue(rel, out var path))
            {
                added++;
                downloads.Add((rel, file));
            }
            else if (new FileInfo(path).Length != file.Size || GitBlobSha(path) != file.Sha)
            {
                updated++;
                downloads.Add((rel, file));
            }
            else
            {
                unchanged++;
            }
        }

        var deletions = local.Keys.Where(k => !remote.ContainsKey(k)).ToList();
        var changes = new DatasetChanges(unchanged, added, updated, deletions.Count, downloads.Sum(d => d.File.Size));
        return new SyncPlan(downloads, deletions, changes);
    }

    // ---------------------------------------------------------------- GitHub

    private string RepoUrl => $"repos/{Uri.EscapeDataString(_options.Owner!)}/{Uri.EscapeDataString(_options.Repo!)}";

    private async Task<string> GetDefaultBranchAsync(CancellationToken ct)
    {
        using var res = await SendAsync(RepoUrl, "application/vnd.github+json", ct);
        using var doc = await JsonDocument.ParseAsync(await res.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
        return doc.RootElement.GetProperty("default_branch").GetString()!;
    }

    private async Task<string> GetCommitShaAsync(string branch, CancellationToken ct)
    {
        using var res = await SendAsync($"{RepoUrl}/commits/{Uri.EscapeDataString(branch)}", "application/vnd.github.sha", ct);
        return (await res.Content.ReadAsStringAsync(ct)).Trim();
    }

    /// <summary>Ficheros bajo la carpeta remota, indexados por su ruta relativa a ella.</summary>
    private async Task<Dictionary<string, RemoteFile>> ListRemoteAsync(string commit, CancellationToken ct)
    {
        using var res = await SendAsync($"{RepoUrl}/git/trees/{commit}?recursive=1", "application/vnd.github+json", ct);
        using var doc = await JsonDocument.ParseAsync(await res.Content.ReadAsStreamAsync(ct), cancellationToken: ct);

        if (doc.RootElement.TryGetProperty("truncated", out var truncated) && truncated.GetBoolean())
            throw new InvalidOperationException(
                "GitHub ha truncado el listado del repositorio (demasiados ficheros); este método no puede sincronizarlo completo.");

        var prefix = _options.RemotePath + "/";
        var files = new Dictionary<string, RemoteFile>(StringComparer.OrdinalIgnoreCase);
        foreach (var e in doc.RootElement.GetProperty("tree").EnumerateArray())
        {
            if (e.GetProperty("type").GetString() != "blob") continue;
            var path = e.GetProperty("path").GetString()!;
            if (!path.StartsWith(prefix, StringComparison.Ordinal)) continue;

            files[path[prefix.Length..]] = new RemoteFile(path, e.GetProperty("sha").GetString()!, e.GetProperty("size").GetInt64());
        }
        return files;
    }

    private async Task DownloadIndividuallyAsync(string commit, string root, List<(string Rel, RemoteFile File)> downloads, CancellationToken ct)
    {
        foreach (var (rel, file) in downloads)
        {
            var path = string.Join('/', file.RepoPath.Split('/').Select(Uri.EscapeDataString));
            using var res = await SendAsync($"{RepoUrl}/contents/{path}?ref={commit}", "application/vnd.github.raw", ct);
            await using var body = await res.Content.ReadAsStreamAsync(ct);
            await WriteVerifiedAsync(body, root, rel, file, ct);
            Update(s => s with { Downloaded = s.Downloaded + 1 });
        }
    }

    private async Task DownloadFromZipAsync(string commit, string root, List<(string Rel, RemoteFile File)> downloads, CancellationToken ct)
    {
        var zipPath = Path.Combine(Path.GetTempPath(), $"heuristicsapp-dataset-{commit[..7]}-{Guid.NewGuid():N}.zip");
        try
        {
            // GitHub redirige a codeload con un token temporal en la URL (válido también para repos privados).
            using (var res = await SendAsync($"{RepoUrl}/zipball/{commit}", "application/vnd.github+json", ct))
            await using (var body = await res.Content.ReadAsStreamAsync(ct))
            await using (var zipFile = File.Create(zipPath))
            {
                var buffer = new byte[1 << 20];
                long total = 0, lastReport = 0;
                int read;
                while ((read = await body.ReadAsync(buffer, ct)) > 0)
                {
                    await zipFile.WriteAsync(buffer.AsMemory(0, read), ct);
                    total += read;
                    if (total - lastReport >= 16 << 20)
                    {
                        lastReport = total;
                        Update(s => s with { Message = $"Descargando el zip del commit… {total / (1 << 20)} MB" });
                    }
                }
            }

            Update(s => s with { Message = $"Extrayendo {downloads.Count} ficheros…" });
            using var zip = ZipFile.OpenRead(zipPath);

            // Las entradas llevan como prefijo una carpeta "owner-repo-sha/": se quita para obtener la ruta del repo.
            var entries = zip.Entries
                .Where(e => e.FullName.IndexOf('/') is var i && i >= 0 && i < e.FullName.Length - 1)
                .ToDictionary(e => e.FullName[(e.FullName.IndexOf('/') + 1)..], StringComparer.OrdinalIgnoreCase);

            foreach (var (rel, file) in downloads)
            {
                if (!entries.TryGetValue(file.RepoPath, out var entry))
                    throw new InvalidOperationException($"El zip del commit no contiene '{file.RepoPath}'.");

                await using var body = entry.Open();
                await WriteVerifiedAsync(body, root, rel, file, ct);
                Update(s => s with { Downloaded = s.Downloaded + 1 });
            }
        }
        finally
        {
            if (File.Exists(zipPath)) File.Delete(zipPath);
        }
    }

    private async Task<HttpResponseMessage> SendAsync(string url, string accept, CancellationToken ct)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        req.Headers.Accept.ParseAdd(accept);
        if (!string.IsNullOrWhiteSpace(_options.Token))
            req.Headers.Authorization = new("Bearer", _options.Token);

        var res = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
        if (res.IsSuccessStatusCode) return res;

        var detail = await res.Content.ReadAsStringAsync(ct);
        if (detail.Length > 200) detail = detail[..200] + "…";
        var code = res.StatusCode;
        res.Dispose();

        throw new InvalidOperationException(code switch
        {
            HttpStatusCode.NotFound =>
                $"GitHub devolvió 404 ({url}): el repositorio, la rama o el commit no existen, o el token no tiene acceso a él.",
            HttpStatusCode.Unauthorized =>
                "GitHub rechazó el token (401). Revisa WarehouseDataset:Token.",
            HttpStatusCode.Forbidden or HttpStatusCode.TooManyRequests =>
                $"GitHub denegó la petición ({(int)code}), posiblemente por límite de peticiones o permisos del token: {detail}",
            _ => $"GitHub devolvió {(int)code} ({url}): {detail}",
        });
    }

    // ---------------------------------------------------------------- Disco

    /// <summary>Escribe en un temporal, comprueba el hash git del contenido y lo mueve a su sitio de forma atómica.</summary>
    private static async Task WriteVerifiedAsync(Stream source, string root, string rel, RemoteFile file, CancellationToken ct)
    {
        var dest = FullPath(root, rel);
        Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
        var tmp = dest + TempSuffix;
        try
        {
            await using (var fs = File.Create(tmp))
                await source.CopyToAsync(fs, ct);

            if (GitBlobSha(tmp) != file.Sha)
                throw new InvalidOperationException($"El contenido descargado de '{rel}' no coincide con el del repositorio.");
            if (file.Size < 1024 && StartsWith(tmp, LfsPointerPrefix))
                throw new InvalidOperationException(
                    $"'{rel}' está almacenado con Git LFS, que esta sincronización no soporta.");

            File.Move(tmp, dest, overwrite: true);
        }
        finally
        {
            if (File.Exists(tmp)) File.Delete(tmp);
        }
    }

    /// <summary>Hash que git asigna al contenido de un fichero: SHA-1 de "blob {tamaño}\0" + contenido.</summary>
    private static string GitBlobSha(string path)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA1);
        using var fs = File.OpenRead(path);
        hash.AppendData(Encoding.ASCII.GetBytes($"blob {fs.Length}\0"));

        var buffer = new byte[1 << 16];
        int read;
        while ((read = fs.Read(buffer)) > 0)
            hash.AppendData(buffer, 0, read);
        return Convert.ToHexStringLower(hash.GetHashAndReset());
    }

    private static bool StartsWith(string path, byte[] prefix)
    {
        using var fs = File.OpenRead(path);
        var head = new byte[prefix.Length];
        return fs.Read(head) == prefix.Length && head.AsSpan().SequenceEqual(prefix);
    }

    /// <summary>Ruta absoluta de un fichero relativo, impidiendo que una ruta remota escape de la carpeta raíz.</summary>
    private static string FullPath(string root, string rel)
    {
        var rootFull = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var full = Path.GetFullPath(Path.Combine(rootFull, rel));
        if (!full.StartsWith(rootFull, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Ruta fuera de la carpeta de instancias: '{rel}'.");
        return full;
    }

    private static void RemoveEmptyDirectories(string root)
    {
        foreach (var dir in Directory.EnumerateDirectories(root, "*", SearchOption.AllDirectories).OrderByDescending(d => d.Length))
            if (!Directory.EnumerateFileSystemEntries(dir).Any())
                Directory.Delete(dir);
    }

    private void Update(Func<DatasetStatus, DatasetStatus> change)
    {
        lock (_gate) _status = change(_status);
    }

    private void Finish(string state, string message)
        => Update(s => s with { State = state, Message = message, FinishedAt = DateTimeOffset.Now });
}
