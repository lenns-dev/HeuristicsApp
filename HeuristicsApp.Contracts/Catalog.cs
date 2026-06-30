public sealed record WarehouseSummary(Guid Id, string Name);
public sealed record StrategyInfo(string Key, string DisplayName);
public sealed record StrategyCatalog(
    IReadOnlyList<StrategyInfo> Batching,
    IReadOnlyList<StrategyInfo> Routing);