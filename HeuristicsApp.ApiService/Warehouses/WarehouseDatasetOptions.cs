namespace HeuristicsApp.ApiService.Warehouses;

/// <summary>
/// Configuración de la carpeta de instancias y del repositorio GitHub del que se sincroniza
/// (sección "WarehouseDataset"). El token nunca debe ir en appsettings: usar user-secrets en
/// local o la variable de entorno WarehouseDataset__Token en Docker.
/// </summary>
public sealed class WarehouseDatasetOptions
{
    public const string Section = "WarehouseDataset";

    /// <summary>Carpeta local de instancias. Relativa al content root; por defecto, la copia en la salida de compilación.</summary>
    public string? InstancesPath { get; set; }

    /// <summary>Propietario (usuario u organización) del repositorio GitHub.</summary>
    public string? Owner { get; set; }

    /// <summary>Nombre del repositorio GitHub.</summary>
    public string? Repo { get; set; }

    /// <summary>Rama; si se omite, la rama por defecto del repositorio.</summary>
    public string? Branch { get; set; }

    /// <summary>Carpeta del repositorio cuyo contenido se replica en <see cref="InstancesPath"/>.</summary>
    public string Path { get; set; } = "dataset";

    /// <summary>Token de acceso personal (lectura de contenidos) para repositorios privados.</summary>
    public string? Token { get; set; }

    /// <summary>Hasta cuántos ficheros se descargan uno a uno; por encima se descarga el zip del commit.</summary>
    public int IndividualDownloadLimit { get; set; } = 200;

    public bool IsConfigured => !string.IsNullOrWhiteSpace(Owner) && !string.IsNullOrWhiteSpace(Repo);

    public string RemotePath => Path.Trim('/');

    public string ResolveInstancesPath(IHostEnvironment env)
        => string.IsNullOrWhiteSpace(InstancesPath)
            ? System.IO.Path.Combine(AppContext.BaseDirectory, "WarehouseInstances")
            : System.IO.Path.GetFullPath(System.IO.Path.Combine(env.ContentRootPath, InstancesPath));
}
