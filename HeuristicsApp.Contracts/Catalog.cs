/// <summary>Instancia de almacén; Group es la subcarpeta dentro de su carpeta (null si cuelga directamente).</summary>
public sealed record WarehouseSummary(Guid Id, string Name, string? Group = null);
public sealed record WarehouseFolder(string Name, int Count);
public sealed record StrategyInfo(string Key, string DisplayName);
public sealed record StrategyCatalog(
    IReadOnlyList<StrategyInfo> Batching,
    IReadOnlyList<StrategyInfo> Routing);