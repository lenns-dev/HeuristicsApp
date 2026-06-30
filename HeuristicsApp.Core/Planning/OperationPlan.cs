namespace HeuristicsApp.Core;

/// <summary>Resultado agregado de ejecutar batching + routing sobre un problema.</summary>
public sealed record OperationPlan(
    IReadOnlyList<PickRoute> Routes,
    double TotalDistance,
    TimeSpan ComputeTime)
{
    public int BatchCount => Routes.Count;

    /// <summary>Construye el plan a partir de las rutas, agregando sus métricas.</summary>
    public static OperationPlan From(IReadOnlyList<PickRoute> routes, TimeSpan computeTime)
        => new(routes, routes.Sum(r => r.Distance), computeTime);
}