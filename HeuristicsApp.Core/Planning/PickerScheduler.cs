namespace HeuristicsApp.Core;

/// <summary>Lote asignado a un picker, con su instante de inicio y su duración (desplazamiento + recogida).</summary>
public sealed record PickerAssignment(int PickerId, PickRoute Route, double StartTime, double Duration)
{
    public double EndTime => StartTime + Duration;
}

/// <summary>
/// Reparte los lotes entre K pickers: cada lote, en el orden en que lo genera el batching,
/// se asigna al picker que antes queda libre (empates: el de menor id). Todos parten del
/// depósito en t=0 y encadenan sus lotes sin esperas.
/// </summary>
public static class PickerScheduler
{
    public static IReadOnlyList<PickerAssignment> Assign(IReadOnlyList<PickRoute> routes, int pickers, PickerTiming timing)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(pickers, 1);

        var freeAt = new PriorityQueue<int, (double At, int Id)>();
        for (var p = 0; p < pickers; p++)
            freeAt.Enqueue(p, (0d, p));

        var assignments = new List<PickerAssignment>(routes.Count);
        foreach (var route in routes)
        {
            freeAt.TryDequeue(out var picker, out var slot);
            var duration = timing.BatchTime(route);
            assignments.Add(new PickerAssignment(picker, route, slot.At, duration));
            freeAt.Enqueue(picker, (slot.At + duration, picker));
        }
        return assignments;
    }
}
