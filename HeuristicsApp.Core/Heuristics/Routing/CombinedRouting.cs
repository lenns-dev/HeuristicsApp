namespace HeuristicsApp.Core;

/// <summary>
/// Routing combinado (Combined): en cada pasillo visitado el picker elige, seg�n
/// el lado por el que entra, entre atravesarlo por completo (como S-Shape,
/// cambiando de lado para el siguiente pasillo) o entrar y salir por el mismo
/// extremo evitando el hueco final tras el art�culo m�s alejado (como Largest
/// Gap, manteniendo el lado), qued�ndose siempre con la opci�n m�s corta.
///
/// A diferencia del algoritmo original, que aproxima el hueco m�s grande de
/// todo el pasillo (incluidos huecos entre dos art�culos en mitad del mismo)
/// sumando un t�rmino extra de anchura de pasillo sin que corresponda a un
/// camino f�sico real, esta implementaci�n reconstruye una ruta v�lida: la
/// vuelta por el mismo extremo solo puede evitar el hueco posterior al �ltimo
/// art�culo alcanzable desde ese extremo, no un hueco interior entre dos
/// art�culos (que obligar�a a atravesarlo igualmente para recoger los art�culos
/// situados al otro lado).
/// </summary>
public sealed class CombinedRouting : IRoutingStrategy
{
    public string Key => "combined";
    public string DisplayName => "Combined (S-Shape + Largest Gap)";

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

        foreach (var aisleId in visited)
        {
            ct.ThrowIfCancellationRequested();

            var x = aisleXById.GetValueOrDefault(aisleId);
            var picks = picksByAisle[aisleId];

            var fullTraversalCost = backY - frontY;
            var farthestY = goingUp ? picks.Max(p => p.Position.Y) : picks.Min(p => p.Position.Y);
            var returnCost = goingUp ? (farthestY - frontY) * 2 : (backY - farthestY) * 2;

            if (returnCost < fullTraversalCost)
            {
                // Largest Gap: entra y sale por el mismo extremo, evitando el hueco final.
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
            else if (goingUp)
            {
                // S-Shape: atraviesa el pasillo de frente a fondo.
                stops.Add(new Coordinate(x, frontY));
                foreach (var p in picks.OrderBy(p => p.Position.Y))
                    stops.Add(p.Position);
                stops.Add(new Coordinate(x, backY));
                goingUp = false;
            }
            else
            {
                // S-Shape: atraviesa el pasillo de fondo a frente.
                stops.Add(new Coordinate(x, backY));
                foreach (var p in picks.OrderByDescending(p => p.Position.Y))
                    stops.Add(p.Position);
                stops.Add(new Coordinate(x, frontY));
                goingUp = true;
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
