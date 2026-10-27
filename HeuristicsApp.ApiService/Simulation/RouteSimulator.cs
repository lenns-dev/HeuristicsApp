using HeuristicsApp.Core;

namespace HeuristicsApp.ApiService.Simulation;

/// <summary>Línea temporal de un lote: puntos por los que pasa (con paradas de recogida) y sus artículos.</summary>
public sealed record BatchTimeline(IReadOnlyList<TrackPoint> Points, IReadOnlyList<PickItem> Items);

/// <summary>
/// Convierte las asignaciones lote→picker en recorridos temporizados: para cada lote, la
/// secuencia de paradas con el instante en que pasa por cada una, deteniéndose en cada
/// artículo el tiempo de recogida del modelo de tiempos. El cliente interpola la posición
/// en cualquier instante, así la animación respeta la duración real del plan.
/// Es una preocupación de presentación: vive en la API, no en el dominio.
/// </summary>
public sealed class RouteSimulator
{
    private const double Eps = 1e-6;

    public BatchTimeline Simulate(PickerAssignment assignment, WarehouseLayout layout, PickerTiming timing)
    {
        var stops = assignment.Route.Stops;
        var batchId = assignment.Route.Batch.Id;
        if (stops.Count == 0)
            return new([], []);

        // Una recogida por SKU, con sus unidades y el primer punto de la ruta en que se alcanza.
        var picks = assignment.Route.Batch.Lines
            .GroupBy(l => l.Sku)
            .Where(g => layout.LocationsBySku.ContainsKey(g.Key))
            .Select(g =>
            {
                var pos = layout.LocationsBySku[g.Key].Position;
                return (Sku: g.Key, Pos: pos, Units: g.Sum(l => l.Quantity), At: FirstPass(stops, pos));
            })
            .ToList();
        var events = picks
            .Where(p => p.At is not null)
            .OrderBy(p => p.At!.Value.Segment).ThenBy(p => p.At!.Value.Fraction)
            .ToList();

        var points = new List<TrackPoint>();
        var pickedAt = new Dictionary<string, double>();
        var t = assignment.StartTime;
        var here = stops[0];
        points.Add(new TrackPoint(t, here.X, here.Y, batchId));

        // Tramo 0 = el punto inicial; tramo i > 0 = stops[i-1] → stops[i].
        var e = 0;
        for (var seg = 0; seg < stops.Count; seg++)
        {
            var from = seg == 0 ? stops[0] : stops[seg - 1];
            var to = stops[seg];

            for (; e < events.Count && events[e].At!.Value.Segment == seg; e++)
            {
                var p = Lerp(from, to, events[e].At!.Value.Fraction);
                t += timing.TravelTime(here.DistanceTo(p)); // métrica del dominio
                here = p;
                points.Add(new TrackPoint(t, here.X, here.Y, batchId));

                var dwell = timing.PickTime(events[e].Units);
                if (dwell > 0)
                {
                    t += dwell;
                    points.Add(new TrackPoint(t, here.X, here.Y, batchId));
                }
                pickedAt[events[e].Sku] = t; // recogido al terminar la parada
            }

            if (seg > 0)
            {
                t += timing.TravelTime(here.DistanceTo(to));
                here = to;
                points.Add(new TrackPoint(t, here.X, here.Y, batchId));
            }
        }

        var items = picks
            .Select(p => new PickItem(p.Sku, p.Pos.X, p.Pos.Y, pickedAt.TryGetValue(p.Sku, out var at) ? at : null))
            .ToList();
        return new(points, items);
    }

    /// <summary>Encadena las líneas temporales de los lotes de cada picker en su recorrido completo.</summary>
    public IReadOnlyList<PickerTrack> BuildTracks(IEnumerable<(PickerAssignment Assignment, BatchTimeline Timeline)> timelines)
        => timelines
            .GroupBy(x => x.Assignment.PickerId)
            .OrderBy(g => g.Key)
            .Select(g => new PickerTrack(g.Key, g
                .OrderBy(x => x.Assignment.StartTime)
                .SelectMany(x => x.Timeline.Points)
                .ToList()))
            .ToList();

    /// <summary>Primer punto de la ruta que pasa por p: (tramo, fracción dentro del tramo), o null si nunca pasa.</summary>
    private static (int Segment, double Fraction)? FirstPass(IReadOnlyList<Coordinate> stops, Coordinate p)
    {
        if (Euclid(stops[0], p) < Eps) return (0, 0);

        for (var i = 1; i < stops.Count; i++)
            if (FractionOnSegment(stops[i - 1], stops[i], p) is double f)
                return (i, f);
        return null;
    }

    // Fracción [0,1] del segmento a→b en la que se encuentra p, o null si p no está sobre él.
    private static double? FractionOnSegment(Coordinate a, Coordinate b, Coordinate p)
    {
        double dx = b.X - a.X, dy = b.Y - a.Y;
        var len2 = dx * dx + dy * dy;
        if (len2 < Eps * Eps) return Euclid(a, p) < Eps ? 0 : null;

        var cross = dx * (p.Y - a.Y) - dy * (p.X - a.X);
        if (Math.Abs(cross) / Math.Sqrt(len2) > Eps) return null;

        var f = (dx * (p.X - a.X) + dy * (p.Y - a.Y)) / len2;
        return f is >= -1e-9 and <= 1 + 1e-9 ? Math.Clamp(f, 0, 1) : null;
    }

    private static Coordinate Lerp(Coordinate a, Coordinate b, double f)
        => new(a.X + (b.X - a.X) * f, a.Y + (b.Y - a.Y) * f);

    private static double Euclid(Coordinate a, Coordinate b)
        => Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));
}
