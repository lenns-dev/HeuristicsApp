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
    /// <summary>
    /// Velocidad de referencia del picker (unidades de layout por segundo).
    /// Placeholder hasta tener un modelo de tiempos propio en el dominio.
    /// </summary>
    private const double DefaultPickerSpeed = 1.2;

    public static SimulationResult ToSimulationResult(
        this OperationPlan plan,
        WarehouseLayout layout,
        RouteSimulator simulator,
        double pickerSpeed = DefaultPickerSpeed)
        => new(
            Layout: layout.ToLayoutView(),
            Batches: plan.Routes.Select(ToBatchPlan).ToList(),
            Frames: simulator.Simulate(plan.Routes, pickerSpeed),
            Metrics: plan.ToMetrics(pickerSpeed));

    private static LayoutView ToLayoutView(this WarehouseLayout layout)
        => new(
            Depot: new RoutePoint(layout.Depot.X, layout.Depot.Y),
            Aisles: layout.Aisles.Select(a => new AisleView(a.Id, a.X, a.FrontY, a.BackY)).ToList());

    private static BatchPlan ToBatchPlan(PickRoute route)
        => new(
            BatchId: route.Batch.Id,
            OrderCount: route.Batch.Orders.Count,
            Distance: route.Distance,
            Stops: route.Stops.Select(s => new RoutePoint(s.X, s.Y)).ToList());

    private static PlanMetrics ToMetrics(this OperationPlan plan, double pickerSpeed)
        => new(
            TotalDistance: plan.TotalDistance,
            EstimatedDuration: TimeSpan.FromSeconds(plan.TotalDistance / pickerSpeed),
            BatchCount: plan.BatchCount,
            ComputeMs: plan.ComputeTime.TotalMilliseconds);
}
