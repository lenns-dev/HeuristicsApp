namespace HeuristicsApp.Core;

/// <summary>
/// Routing por mayor hueco (Largest Gap): el picker recorre por completo el
/// primer y el �ltimo pasillo visitado (necesario para pasar del pasillo
/// transversal frontal al trasero, y viceversa); en cada pasillo intermedio
/// entra y sale por el mismo extremo, evitando el hueco posterior al art�culo
/// m�s alejado desde ese extremo. Si el lote solo visita un pasillo, no hace
/// falta ning�n recorrido completo: se trata como intermedio.
///
/// A diferencia del algoritmo original, que asume que cada pasillo intermedio
/// puede elegir libremente el extremo m�s favorable (el hueco mayor puede
/// quedar tanto al principio como al final), esta implementaci�n mantiene el
/// lado de entrada determinado por el pasillo anterior (como en
/// <see cref="SShapeRouting"/> y <see cref="CombinedRouting"/>): cambiar de
/// lado en un pasillo intermedio exigir�a atravesarlo por completo, perdiendo
/// el ahorro que motiva esta heur�stica.
/// </summary>
public sealed class LargestGapRouting : IRoutingStrategy
{
    public string Key => "largest-gap";
    public string DisplayName => "Largest Gap";

    public PickRoute Route(Batch batch, WarehouseLayout layout, CancellationToken ct)
    {
        var picksByAisle = batch.Lines
            .Select(l => l.Sku)
            .Distinct()
            .Where(layout.LocationsBySku.ContainsKey)
            .Select(sku => layout.LocationsBySku[sku])
            .GroupBy(loc => loc.AisleId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var depot = layout.Depot;

        // Sin art�culos localizables -> ruta trivial
        if (picksByAisle.Count == 0)
            return new PickRoute(batch, [depot], 0);

        var aisleXById = layout.Aisles.ToDictionary(a => a.Id, a => a.X);
        var backY = layout.Aisles.Count > 0 ? layout.Aisles.Max(a => a.BackY) : 0d;
        var frontY = depot.Y;
        var visited = picksByAisle.Keys.OrderBy(id => aisleXById.GetValueOrDefault(id)).ToList();

        var stops = new List<Coordinate> { depot };
        var goingUp = true; // true: se entra el pasillo por el frente (lado del dep�sito)

        for (var i = 0; i < visited.Count; i++)
        {
            ct.ThrowIfCancellationRequested();

            var x = aisleXById.GetValueOrDefault(visited[i]);
            var picks = picksByAisle[visited[i]];
            var isBoundary = visited.Count > 1 && (i == 0 || i == visited.Count - 1);

            if (isBoundary)
            {
                // Recorrido completo: obligatorio para cambiar de pasillo transversal.
                if (goingUp)
                {
                    stops.Add(new Coordinate(x, frontY));
                    foreach (var p in picks.OrderBy(p => p.Position.Y))
                        stops.Add(p.Position);
                    stops.Add(new Coordinate(x, backY));
                }
                else
                {
                    stops.Add(new Coordinate(x, backY));
                    foreach (var p in picks.OrderByDescending(p => p.Position.Y))
                        stops.Add(p.Position);
                    stops.Add(new Coordinate(x, frontY));
                }
                goingUp = !goingUp;
            }
            else
            {
                // Entra y sale por el mismo extremo, evitando el hueco final.
                var entryY = goingUp ? frontY : backY;
                var orderedPicks = goingUp
                    ? picks.OrderBy(p => p.Position.Y)
                    : picks.OrderByDescending(p => p.Position.Y);

                stops.Add(new Coordinate(x, entryY));
                foreach (var p in orderedPicks)
                    stops.Add(p.Position);
                stops.Add(new Coordinate(x, entryY));
                // goingUp no cambia: se sale por el mismo lado por el que se entr�.
            }
        }

        // Regreso al dep�sito v�a cross-aisle si es necesario.
        var lastExit = stops[^1];
        if (lastExit.Y != depot.Y)
            stops.Add(new Coordinate(depot.X, lastExit.Y));
        stops.Add(depot);

        var distance = 0d;
        for (var i = 1; i < stops.Count; i++)
            distance += layout.Distance(stops[i - 1], stops[i]);

        return new PickRoute(batch, stops, distance);
    }
}
