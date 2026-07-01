using HeuristicsApp.Core;

namespace HeuristicsApp.ApiService.Simulation;

/// <summary>
/// Convierte las rutas resueltas en una secuencia de frames temporizados,
/// muestreando la posición de cada agente (uno por lote) a intervalos regulares.
/// Es una preocupación de presentación: vive en la API, no en el dominio.
/// </summary>
public sealed class RouteSimulator
{
    private const int TargetFrames = 120;

    public IReadOnlyList<SimulationFrame> Simulate(IReadOnlyList<PickRoute> routes, double pickerSpeed)
    {
        if (routes.Count == 0 || pickerSpeed <= 0)
            return [];

        var tracks = routes.Select(Track.From).ToList();
        var totalDistance = tracks.Max(t => t.Total);
        if (totalDistance <= 0)
            return [];

        // Todos los agentes arrancan a la vez (t=0); la ruta más larga marca la duración.
        var totalTime = totalDistance / pickerSpeed;
        var dt = totalTime / (TargetFrames - 1);

        var frames = new List<SimulationFrame>(TargetFrames);
        for (var k = 0; k < TargetFrames; k++)
        {
            var t = k * dt;
            var distance = t * pickerSpeed;

            var agents = new List<AgentState>(tracks.Count);
            foreach (var track in tracks)
            {
                var (x, y, done) = track.PositionAt(distance);
                agents.Add(new AgentState(track.PickerId, x, y, done ? "done" : "moving"));
            }
            frames.Add(new SimulationFrame(t, agents));
        }
        return frames;
    }

    /// <summary>Polilínea de una ruta con su longitud acumulada, para interpolar rápido.</summary>
    private sealed class Track
    {
        private readonly Coordinate[] _pts;
        private readonly double[] _cum;

        public int PickerId { get; }
        public double Total => _cum[^1];

        private Track(int pickerId, Coordinate[] pts, double[] cum)
            => (PickerId, _pts, _cum) = (pickerId, pts, cum);

        public static Track From(PickRoute route)
        {
            var pts = route.Stops.Count > 0 ? route.Stops.ToArray() : [new Coordinate(0, 0)];
            var cum = new double[pts.Length];
            for (var i = 1; i < pts.Length; i++)
                cum[i] = cum[i - 1] + pts[i - 1].DistanceTo(pts[i]); // métrica del dominio
            return new Track(route.Batch.Id, pts, cum);
        }

        public (double X, double Y, bool Done) PositionAt(double distance)
        {
            if (distance >= Total)
            {
                var last = _pts[^1];
                return (last.X, last.Y, true);
            }

            var i = 1;
            while (i < _cum.Length && _cum[i] < distance) i++;

            var a = _pts[i - 1];
            var b = _pts[i];
            var segLen = _cum[i] - _cum[i - 1];
            var f = segLen > 0 ? (distance - _cum[i - 1]) / segLen : 0;
            return (a.X + (b.X - a.X) * f, a.Y + (b.Y - a.Y) * f, false);
        }
    }
}