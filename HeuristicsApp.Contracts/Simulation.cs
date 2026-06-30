public sealed record PlanRequest(Guid WarehouseId, string BatchingKey, string RoutingKey, BatchingOptions? Options = null);
public sealed record BatchingOptions(int MaxItemsPerBatch, double MaxWeight, int Pickers);

public sealed record SimulationResult(
    IReadOnlyList<BatchPlan> Batches,
    IReadOnlyList<SimulationFrame> Frames,
    PlanMetrics Metrics);

public sealed record BatchPlan(int BatchId, int OrderCount, double Distance, IReadOnlyList<RoutePoint> Stops);
public sealed record RoutePoint(double X, double Y);

public sealed record SimulationFrame(double TimestampSeconds, IReadOnlyList<AgentState> Agents);
public sealed record AgentState(int PickerId, double X, double Y, string? Action);
public sealed record PlanMetrics(double TotalDistance, TimeSpan EstimatedDuration, int BatchCount, double ComputeMs);