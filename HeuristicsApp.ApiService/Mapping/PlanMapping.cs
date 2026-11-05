using HeuristicsApp.ApiService.Simulation;
using HeuristicsApp.Core;

namespace HeuristicsApp.ApiService.Mapping;

/// <summary>
/// Traduce el resultado de dominio <see cref="OperationPlan"/> a los DTOs de
/// transporte (<see cref="SimulationResult"/>) que consume la Web.
/// Mantiene a Core sin dependencia alguna de Contracts.
/// </summary>
public static class PlanMapping
{
    public static SimulationResult ToSimulationResult(
        this OperationPlan plan,
        WarehouseLayout layout,
        RouteSimulator simulator)
    {
        // Una línea temporal por lote (recorrido + paradas de recogida), compartida por lotes y tracks.
        var timelines = plan.Assignments
            .Select(a => (Assignment: a, Timeline: simulator.Simulate(a, layout, plan.Timing)))
            .ToList();

        return new(
            Layout: layout.ToLayoutView(),
            Batches: timelines.OrderBy(x => x.Assignment.Route.Batch.Id).Select(x => x.Assignment.ToBatchPlan(x.Timeline)).ToList(),
            Tracks: simulator.BuildTracks(timelines),
            Metrics: plan.ToMetrics());
    }

    private static LayoutView ToLayoutView(this WarehouseLayout layout)
        => new(
            Depot: new RoutePoint(layout.Depot.X, layout.Depot.Y),
            Aisles: layout.Aisles.Select(a => new AisleView(a.Id, a.X, a.FrontY, a.BackY)).ToList(),
            Dimensions: layout.Dimensions is { } d
                ? new LayoutDimensions(d.Width, d.Length, d.AisleSpacing, d.AisleWidth, d.ShelfWidth)
                : null);

    private static BatchPlan ToBatchPlan(this PickerAssignment assignment, BatchTimeline timeline)
        => new(
            BatchId: assignment.Route.Batch.Id,
            PickerId: assignment.PickerId,
            OrderCount: assignment.Route.Batch.Orders.Count,
            Distance: assignment.Route.Distance,
            StartSeconds: assignment.StartTime,
            Stops: assignment.Route.Stops.Select(s => new RoutePoint(s.X, s.Y)).ToList(),
            Items: timeline.Items);

    private static PlanMetrics ToMetrics(this OperationPlan plan)
        => new(
            TotalDistance: plan.TotalDistance,
            // Duración real con K pickers en paralelo: lo que tarda el último en terminar.
            EstimatedDuration: TimeSpan.FromSeconds(plan.Makespan),
            BatchCount: plan.BatchCount,
            PickerCount: plan.Pickers,
            ComputeMs: plan.ComputeTime.TotalMilliseconds,
            TravelSpeed: plan.Timing.TravelSpeed,
            PickTimePerItem: plan.Timing.PickTimePerItem);
}
