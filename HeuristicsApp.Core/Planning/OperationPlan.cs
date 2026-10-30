namespace HeuristicsApp.Core;

/// <summary>Resultado agregado de ejecutar batching + routing sobre un problema.</summary>
public sealed record OperationPlan(
    IReadOnlyList<PickRoute> Routes,
    IReadOnlyList<PickerAssignment> Assignments,
    int Pickers,
    PickerTiming Timing,
    double TotalDistance,
    TimeSpan ComputeTime)
{
    public int BatchCount => Routes.Count;

    /// <summary>Instante en que termina el último picker (makespan, en unidades de tiempo de la instancia).</summary>
    public double Makespan => Assignments.Count == 0 ? 0 : Assignments.Max(a => a.EndTime);

    /// <summary>Construye el plan a partir de las rutas, repartiéndolas entre los pickers y agregando sus métricas.</summary>
    public static OperationPlan From(IReadOnlyList<PickRoute> routes, int pickers, PickerTiming timing, TimeSpan computeTime)
        => new(routes, PickerScheduler.Assign(routes, pickers, timing), pickers, timing, routes.Sum(r => r.Distance), computeTime);
}
