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
        double pickerSpeed = DefaultPickerSpeed)
        => new(
            Batches: plan.Routes.Select(ToBatchPlan).ToList(),
            Frames: [], // TODO: generar con el simulador temporal (paso siguiente).
            Metrics: plan.ToMetrics(pickerSpeed));

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
