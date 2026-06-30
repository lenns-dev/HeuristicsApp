using HeuristicsApp.Core;
using HeuristicsApp.Core.Instances;

namespace HeuristicsApp.ApiService.Warehouses;

public interface IWarehouseProvider
{
    IReadOnlyList<WarehouseInstanceInfo> List();
    WarehouseProblem? Find(Guid warehouseId);
}

/// <summary>
/// Carga todas las instancias *.json del directorio de salida (copiadas desde
/// Core/WarehouseInstances) y las indexa por su Guid derivado del nombre.
/// </summary>
public sealed class FileWarehouseProvider : IWarehouseProvider
{
    private readonly Dictionary<Guid, WarehouseProblem> _byId = [];
    private readonly List<WarehouseInstanceInfo> _index = [];

    public FileWarehouseProvider(ILogger<FileWarehouseProvider> logger)
    {
        var root = Path.Combine(AppContext.BaseDirectory, "WarehouseInstances");
        if (!Directory.Exists(root))
        {
            logger.LogWarning("No warehouse instances found at {Root}.", root);
            return;
        }

        foreach (var path in Directory.EnumerateFiles(root, "*.json", SearchOption.AllDirectories))
        {
            try
            {
                using var stream = File.OpenRead(path);
                var name = Path.GetFileNameWithoutExtension(path);
                var problem = WarehouseInstanceLoader.Load(stream, name);
                _byId[problem.WarehouseId] = problem;
                _index.Add(new WarehouseInstanceInfo(problem.WarehouseId, name));
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to load warehouse instance {Path}.", path);
            }
        }

        logger.LogInformation("Loaded {Count} warehouse instances.", _index.Count);
    }

    public IReadOnlyList<WarehouseInstanceInfo> List() => _index;
    public WarehouseProblem? Find(Guid warehouseId) => _byId.GetValueOrDefault(warehouseId);
}