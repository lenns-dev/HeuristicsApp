namespace HeuristicsApp.Core;

/// <summary>
/// Routing en S (S-Shape / Traversal): el picker atraviesa por completo cada
/// pasillo con al menos un artículo, serpenteando de un extremo al otro.
/// </summary>
public sealed class SShapeRouting : IRoutingStrategy
{
    public string Key => "s-shape";
    public string DisplayName => "S-Shape (Traversal)";

    public PickRoute Route(Batch batch, WarehouseLayout layout, CancellationToken ct)
    {
        // 1) Mapear líneas -> ubicaciones únicas (cada SKU se visita una vez),
        //    agrupadas por pasillo.
        var picksByAisle = batch.Lines
            .Select(l => l.Sku)
            .Distinct()
            .Where(layout.LocationsBySku.ContainsKey)
            .Select(sku => layout.LocationsBySku[sku])
            .GroupBy(loc => loc.AisleId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var depot = layout.Depot;

        // Sin artículos localizables -> ruta trivial en el depósito.
        if (picksByAisle.Count == 0)
            return new PickRoute(batch, [depot], 0);

        // 2) Ordenar los pasillos visitados por X (orden físico izq. -> der.).
        var aisleXById = layout.Aisles.ToDictionary(a => a.Id, a => a.X);
        var backY = layout.Aisles.Count > 0 ? layout.Aisles.Max(a => a.BackY) : 0d;
        var visited = picksByAisle.Keys.OrderBy(id => aisleXById.GetValueOrDefault(id)).ToList();

        // 3) Serpentear: un pasillo de frente->fondo, el siguiente de fondo->frente.
        //    Los segmentos entre pasillos son siempre horizontales (mismo Y),
        //    lo que representa el recorrido por el cross-aisle.
        var stops = new List<Coordinate> { depot };
        var goingUp = true;

        foreach (var aisleId in visited)
        {
            ct.ThrowIfCancellationRequested();

            var x = aisleXById.GetValueOrDefault(aisleId);
            var picks = picksByAisle[aisleId];

            if (goingUp)
            {
                stops.Add(new Coordinate(x, depot.Y));           // entra por el cross-aisle frontal
                foreach (var p in picks.OrderBy(p => p.Position.Y))
                    stops.Add(p.Position);
                stops.Add(new Coordinate(x, backY));             // sale por el cross-aisle trasero
            }
            else
            {
                stops.Add(new Coordinate(x, backY));             // entra por el cross-aisle trasero
                foreach (var p in picks.OrderByDescending(p => p.Position.Y))
                    stops.Add(p.Position);
                stops.Add(new Coordinate(x, depot.Y));           // sale por el cross-aisle frontal
            }

            goingUp = !goingUp;
        }

        // 4) Regreso al depósito vía cross-aisle si es necesario.
        //    Si el último exit fue en backY (número impar de pasillos visitados),
        //    hay que recorrer el cross-aisle trasero hasta la columna del depósito
        //    antes de bajar: evita la diagonal visual que cruzaría todo el almacén.
        var lastExit = stops[^1];
        if (lastExit.Y != depot.Y)
            stops.Add(new Coordinate(depot.X, lastExit.Y)); // cross-aisle trasero -> columna depósito
        stops.Add(depot);

        var distance = 0d;
        for (var i = 1; i < stops.Count; i++)
            distance += layout.Distance(stops[i - 1], stops[i]);

        return new PickRoute(batch, stops, distance);
    }
}