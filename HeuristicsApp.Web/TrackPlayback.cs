namespace HeuristicsApp.Web;

/// <summary>
/// Reconstruye el estado de los pickers en un instante arbitrario interpolando
/// linealmente sobre sus recorridos temporizados.
/// </summary>
public static class TrackPlayback
{
    public static SimulationFrame At(IReadOnlyList<PickerTrack> tracks, double timeSeconds)
        => new(timeSeconds, tracks.Where(t => t.Points.Count > 0).Select(t => StateAt(t, timeSeconds)).ToList());

    private static AgentState StateAt(PickerTrack track, double t)
    {
        var pts = track.Points;
        if (t <= pts[0].TimeSeconds)
            return new AgentState(track.PickerId, pts[0].BatchId, pts[0].X, pts[0].Y, "moving");

        var last = pts[^1];
        if (t >= last.TimeSeconds)
            return new AgentState(track.PickerId, last.BatchId, last.X, last.Y, "done");

        // Primer punto con instante >= t (búsqueda binaria): el agente está en el tramo [i-1, i].
        int lo = 1, hi = pts.Count - 1;
        while (lo < hi)
        {
            var mid = (lo + hi) / 2;
            if (pts[mid].TimeSeconds < t) lo = mid + 1; else hi = mid;
        }

        var a = pts[lo - 1];
        var b = pts[lo];
        var span = b.TimeSeconds - a.TimeSeconds;
        var f = span > 0 ? (t - a.TimeSeconds) / span : 0;
        return new AgentState(track.PickerId, b.BatchId, a.X + (b.X - a.X) * f, a.Y + (b.Y - a.Y) * f, "moving");
    }
}
