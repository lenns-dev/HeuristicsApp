/// <summary>
/// Estado de la sincronización de la carpeta de instancias con el repositorio remoto.
/// State: idle | checking | downloading | applying | succeeded | failed.
/// </summary>
public sealed record DatasetStatus(
    bool Configured,
    string? Repository,
    string? Branch,
    string? Commit,
    string RemotePath,
    string LocalPath,
    string State,
    bool DryRun,
    string? Message,
    DatasetChanges? Changes,
    int Downloaded,
    DateTimeOffset? StartedAt,
    DateTimeOffset? FinishedAt)
{
    public bool IsRunning => State is "checking" or "downloading" or "applying";
}

/// <summary>Diferencias entre la carpeta local y la remota (en número de ficheros).</summary>
public sealed record DatasetChanges(int Unchanged, int Added, int Updated, int Deleted, long DownloadBytes)
{
    public int ToDownload => Added + Updated;
    public bool HasChanges => Added + Updated + Deleted > 0;
}
