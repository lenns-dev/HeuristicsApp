using HeuristicsApp.Core;
using HeuristicsApp.Core.Instances;
using Microsoft.Extensions.Options;

namespace HeuristicsApp.ApiService.Warehouses;

public interface IWarehouseProvider
{
    /// <summary>Carpeta local de la que se leen las instancias.</summary>
    string Root { get; }

    /// <summary>Carpetas de primer nivel dentro de la carpeta de instancias, con su número de instancias.</summary>
    IReadOnlyList<WarehouseFolder> Folders();

    /// <summary>Instancias de una carpeta (todas si es null); null si la carpeta no existe.</summary>
    IReadOnlyList<WarehouseSummary>? List(string? folder);

    /// <summary>Carga la instancia desde disco; null si el id no existe.</summary>
    WarehouseProblem? Find(Guid warehouseId);

    /// <summary>Vuelve a indexar la carpeta (p.ej. tras sincronizarla con el repositorio).</summary>
    void Reload();
}

/// <summary>
/// Indexa las instancias *.json de la carpeta configurada (<see cref="WarehouseDatasetOptions.InstancesPath"/>)
/// por carpeta de primer nivel. Al indexar solo recorre rutas: cada instancia se parsea al usarse,
/// porque el conjunto completo ocupa >1 GB. El id es un Guid derivado de la ruta relativa,
/// estable y único aunque se repitan nombres.
/// </summary>
public sealed class FileWarehouseProvider : IWarehouseProvider
{
    private sealed record Entry(Guid Id, string Key, string Path, string Folder, WarehouseSummary Summary);

    /// <summary>Índice inmutable; Reload construye uno nuevo y lo sustituye de golpe.</summary>
    private sealed record Index(
        IReadOnlyDictionary<Guid, Entry> ById,
        IReadOnlyDictionary<string, List<WarehouseSummary>> ByFolder,
        IReadOnlyList<WarehouseFolder> Folders);

    private readonly ILogger<FileWarehouseProvider> _logger;
    private volatile Index _index;

    public string Root { get; }

    public FileWarehouseProvider(
        IOptions<WarehouseDatasetOptions> options,
        IHostEnvironment env,
        ILogger<FileWarehouseProvider> logger)
    {
        _logger = logger;
        Root = options.Value.ResolveInstancesPath(env);
        _index = Build();
    }

    public void Reload() => _index = Build();

    public IReadOnlyList<WarehouseFolder> Folders() => _index.Folders;

    public IReadOnlyList<WarehouseSummary>? List(string? folder)
    {
        var index = _index;
        return folder is null
            ? index.Folders.SelectMany(f => index.ByFolder[f.Name]).ToList()
            : index.ByFolder.GetValueOrDefault(folder);
    }

    public WarehouseProblem? Find(Guid warehouseId)
    {
        if (!_index.ById.TryGetValue(warehouseId, out var entry))
            return null;

        using var stream = File.OpenRead(entry.Path);
        return WarehouseInstanceLoader.Load(stream, entry.Key);
    }

    private Index Build()
    {
        var byId = new Dictionary<Guid, Entry>();
        var byFolder = new Dictionary<string, List<WarehouseSummary>>(StringComparer.OrdinalIgnoreCase);
        var folders = new List<WarehouseFolder>();

        if (!Directory.Exists(Root))
        {
            _logger.LogWarning("No warehouse instances found at {Root}.", Root);
            return new(byId, byFolder, folders);
        }

        foreach (var path in Directory.EnumerateFiles(Root, "*.json", SearchOption.AllDirectories))
        {
            // p.ej. "W1-W2-W3-W4_Albareda/W1/abc/A_W1_ABC_50_12_ID1"
            var key = System.IO.Path.ChangeExtension(System.IO.Path.GetRelativePath(Root, path), null).Replace('\\', '/');
            var parts = key.Split('/');
            var folder = parts.Length > 1 ? parts[0] : "";
            var group = parts.Length > 2 ? string.Join('/', parts[1..^1]) : null;

            var id = WarehouseInstanceLoader.DeriveId(key);
            byId[id] = new Entry(id, key, path, folder, new WarehouseSummary(id, parts[^1], group));
        }

        foreach (var g in byId.Values.GroupBy(e => e.Folder, StringComparer.OrdinalIgnoreCase))
        {
            byFolder[g.Key] = g
                .OrderBy(e => e.Summary.Group ?? "", NaturalComparer.Instance)
                .ThenBy(e => e.Summary.Name, NaturalComparer.Instance)
                .Select(e => e.Summary)
                .ToList();
            folders.Add(new WarehouseFolder(g.Key, byFolder[g.Key].Count));
        }
        folders.Sort((a, b) => NaturalComparer.Instance.Compare(a.Name, b.Name));

        _logger.LogInformation("Indexed {Count} warehouse instances in {Folders} folders from {Root}.",
            byId.Count, folders.Count, Root);
        return new(byId, byFolder, folders);
    }

    /// <summary>Orden "natural": los tramos numéricos se comparan por valor (ID2 &lt; ID10).</summary>
    private sealed class NaturalComparer : IComparer<string>
    {
        public static readonly NaturalComparer Instance = new();

        public int Compare(string? x, string? y)
        {
            if (x is null || y is null) return string.Compare(x, y, StringComparison.OrdinalIgnoreCase);

            int i = 0, j = 0;
            while (i < x.Length && j < y.Length)
            {
                if (char.IsDigit(x[i]) && char.IsDigit(y[j]))
                {
                    var si = i; while (i < x.Length && char.IsDigit(x[i])) i++;
                    var sj = j; while (j < y.Length && char.IsDigit(y[j])) j++;
                    var a = x[si..i].TrimStart('0');
                    var b = y[sj..j].TrimStart('0');
                    var c = a.Length != b.Length ? a.Length.CompareTo(b.Length) : string.CompareOrdinal(a, b);
                    if (c != 0) return c;
                }
                else
                {
                    var c = char.ToUpperInvariant(x[i]).CompareTo(char.ToUpperInvariant(y[j]));
                    if (c != 0) return c;
                    i++; j++;
                }
            }
            return (x.Length - i).CompareTo(y.Length - j);
        }
    }
}
