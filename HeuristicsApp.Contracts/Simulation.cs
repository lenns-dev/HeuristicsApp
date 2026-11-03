public sealed record PlanRequest(Guid WarehouseId, string BatchingKey, string RoutingKey, BatchingOptions? Options = null);
public sealed record BatchingOptions(int? MaxItemsPerBatch = null, double? MaxWeight = null, int? Pickers = null);

public sealed record SimulationResult(
    LayoutView Layout,
    IReadOnlyList<BatchPlan> Batches,
    IReadOnlyList<PickerTrack> Tracks,
    PlanMetrics Metrics);

public sealed record LayoutView(RoutePoint Depot, IReadOnlyList<AisleView> Aisles, LayoutDimensions? Dimensions = null);
public sealed record LayoutDimensions(double Width, double Length, double AisleSpacing, double AisleWidth, double ShelfWidth);
public sealed record AisleView(int Id, double X, double FrontY, double BackY);

public sealed record BatchPlan(int BatchId, int PickerId, int OrderCount, double Distance, double StartSeconds, IReadOnlyList<RoutePoint> Stops, IReadOnlyList<PickItem> Items);
/// <summary>Artículo a recoger en un lote y el instante en que el picker llega a él (null si la ruta no pasa por su ubicación).</summary>
public sealed record PickItem(string Sku, double X, double Y, double? PickedAtSeconds);
public sealed record RoutePoint(double X, double Y);

/// <summary>Recorrido temporizado de un picker: sus lotes encadenados, con el instante de paso por cada parada.</summary>
public sealed record PickerTrack(int PickerId, IReadOnlyList<TrackPoint> Points);
public sealed record TrackPoint(double TimeSeconds, double X, double Y, int BatchId);

/// <summary>Estado de los pickers en un instante; lo calcula el cliente a partir de los tracks.</summary>
public sealed record SimulationFrame(double TimestampSeconds, IReadOnlyList<AgentState> Agents);
public sealed record AgentState(int PickerId, int BatchId, double X, double Y, string? Action);
public sealed record PlanMetrics(double TotalDistance, TimeSpan EstimatedDuration, int BatchCount, int PickerCount, double ComputeMs,
    double TravelSpeed, double PickTimePerItem);
