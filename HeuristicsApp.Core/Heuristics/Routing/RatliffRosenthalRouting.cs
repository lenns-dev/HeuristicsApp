namespace HeuristicsApp.Core;

/// <summary>
/// Distancia óptima exacta (no una heurística) para recorrer todos
/// los artículos de un lote en un almacén de bloque único (dos pasillos
/// transversales, frontal y trasero)
///
/// A diferencia de las demás estrategias de este proyecto, esta implementación no
/// reconstruye la secuencia física de paradas: la tabla de la programación dinámica
/// original solo emite la distancia mínima a través de estados abstractos que no
/// codifican un camino explícito (el óptimo real puede visitar parcialmente varios
/// pasillos fuera de orden, no es un simple recorrido en S con una única excepción).
/// Reconstruir esa ruta de forma fiel requeriría propagar "backpointers" por toda la
/// tabla
/// 
/// Comprobar viabilidad futura y preguntar
/// </summary>
public sealed class RatliffRosenthalRouting : IRoutingStrategy
{
    public string Key => "ratliff-rosenthal";
    public string DisplayName => "Ratliff & Rosenthal (Optimal)";

    public PickRoute Route(Batch batch, WarehouseLayout layout, CancellationToken ct)
    {
        var picksByAisle = batch.Lines
            .Select(l => l.Sku)
            .Distinct()
            .Where(layout.LocationsBySku.ContainsKey)
            .Select(sku => layout.LocationsBySku[sku])
            .GroupBy(loc => loc.AisleId)
            .ToDictionary(g => g.Key, g => g.Select(p => p.Position.Y).ToList());

        var depot = layout.Depot;
        if (picksByAisle.Count == 0)
            return new PickRoute(batch, [depot], 0);

        var aisles = layout.Aisles.OrderBy(a => a.X).ToList();
        var n = aisles.Count;

        // Alturas por pasillo, medidas desde su frente. El depósito se ancla al frente
        // del primer pasillo (equivalente al caso DEPOT_CORNER del algoritmo original).
        var heightsByIndex = new Dictionary<int, List<double>> { [0] = [0.0] };
        for (var i = 0; i < n; i++)
        {
            if (!picksByAisle.TryGetValue(aisles[i].Id, out var ys)) continue;

            var front = Math.Min(aisles[i].FrontY, aisles[i].BackY);
            foreach (var y in ys)
            {
                ct.ThrowIfCancellationRequested();
                var height = y - front;
                if (heightsByIndex.TryGetValue(i, out var list))
                {
                    if (!list.Contains(height)) list.Add(height);
                }
                else
                {
                    heightsByIndex[i] = [height];
                }
            }
        }

        var numSteps = (n * 2) - 1;
        var table = new double?[numSteps][];
        for (var i = 0; i < numSteps; i++) table[i] = new double?[7];

        var step = 0;
        for (var i = 0; i < n; i++)
        {
            ct.ThrowIfCancellationRequested();

            var shelfLength = Math.Abs(aisles[i].BackY - aisles[i].FrontY);
            var hasPicks = false;
            double maxAlt = -1, minAlt = 0, maxGap = 0;

            if (heightsByIndex.TryGetValue(i, out var heights))
            {
                hasPicks = true;
                heights.Sort();
                maxAlt = heights[^1];
                minAlt = heights[0];
                var previous = -1.0;
                foreach (var h in heights)
                {
                    var gap = h - previous;
                    if (previous > -1 && gap > maxGap) maxGap = gap;
                    previous = h;
                }
            }

            var path = new double[6];
            path[0] = shelfLength;
            path[1] = (shelfLength - minAlt) * 2;
            path[2] = maxAlt == 0 ? 0.0 : maxAlt * 2;
            path[3] = (shelfLength - maxGap) * 2;
            path[4] = shelfLength * 2;
            path[5] = 0.0;

            var crossDistance = i + 1 < n ? aisles[i + 1].X - aisles[i].X : 0.0;
            var pathV = new[] { crossDistance * 2, crossDistance * 2, crossDistance * 2, crossDistance * 4, 0.0, 0.0 };

            if (step == 0)
            {
                table[step][0] = path[0];
                table[step][1] = path[1];
                table[step][2] = path[2];
                table[step][3] = path[4];
                table[step][4] = path[3];
                table[step][5] = hasPicks ? -1.0 : path[5];
                table[step][6] = -1.0;
            }
            else
            {
                var cross = table[step];
                var local = table[step - 1];

                cross[0] = local[0] + pathV[0];
                cross[1] = local[1] + pathV[1];
                cross[4] = local[1] + pathV[3];
                if (local[1] != null && local[1] != -1) cross[6] = local[1] + pathV[4];
                cross[2] = local[2] + pathV[2];
                if (cross[4] == null || cross[4] > local[2] + pathV[3]) cross[4] = local[2] + pathV[3];
                if (local[2] != null && local[2] != -1 && (cross[6] == null || cross[6] > local[2] + pathV[4]))
                    cross[6] = local[2] + pathV[4];
                if (cross[1] == null || cross[1] > local[3] + pathV[1]) cross[1] = local[3] + pathV[1];
                if (cross[2] == null || cross[2] > local[3] + pathV[2]) cross[2] = local[3] + pathV[2];
                cross[3] = local[3] + pathV[3];
                if (local[3] != null && local[3] != -1 && (cross[6] == null || cross[6] > local[3] + pathV[4]))
                    cross[6] = local[3] + pathV[4];
                if (cross[4] == null || cross[4] > local[4] + pathV[3]) cross[4] = local[4] + pathV[3];
                if (local[5] != null && local[5] != -1) cross[5] = local[5] + pathV[4];
                if (local[6] != null && local[6] != -1 && cross[6] > local[6] + pathV[4]) cross[6] = local[6] + pathV[4];

                var selectValue = double.MaxValue;
                var maxPath = hasPicks ? path.Length - 1 : path.Length;
                for (var j = 1; j < maxPath; j++)
                    if (path[j] < selectValue) selectValue = path[j];

                step++;
                var next = table[step];
                var prev = table[step - 1];

                next[3] = prev[0] + path[0];
                next[0] = prev[0] + selectValue;
                if (!hasPicks && next[0] > prev[0] + path[5]) next[0] = prev[0] + path[5];
                if (next[0] == null || next[0] > prev[1] + path[0]) next[0] = prev[1] + path[0];
                next[1] = prev[1] + path[1];
                next[4] = prev[1] + path[2];
                if (next[4] == null || next[4] > prev[1] + path[3]) next[4] = prev[1] + path[3];
                if (next[3] == null || next[3] > prev[1] + path[4]) next[3] = prev[1] + path[4];
                if (!hasPicks && (next[1] == null || next[1] > prev[1] + path[5])) next[1] = prev[1] + path[5];
                if (next[0] == null || next[0] > prev[2] + path[0]) next[0] = prev[2] + path[0];
                if (next[4] == null || next[4] > prev[2] + path[1]) next[4] = prev[2] + path[1];
                next[2] = prev[2] + path[2];
                if (next[4] == null || next[4] > prev[2] + path[3]) next[4] = prev[2] + path[3];
                if (next[3] == null || next[3] > prev[2] + path[4]) next[3] = prev[2] + path[4];
                if (!hasPicks && (next[2] == null || next[2] > prev[2] + path[5])) next[2] = prev[2] + path[5];
                if (next[0] == null || next[0] > prev[3] + path[0]) next[0] = prev[3] + path[0];
                if (next[3] == null || next[3] > prev[3] + selectValue) next[3] = prev[3] + selectValue;
                if (!hasPicks && (next[3] == null || next[3] > prev[3] + path[5])) next[3] = prev[3] + path[5];
                if (next[0] == null || next[0] > prev[4] + path[0]) next[0] = prev[4] + path[0];
                if (next[4] == null || next[4] > prev[4] + path[1]) next[4] = prev[4] + path[1];
                if (next[4] == null || next[4] > prev[4] + path[2]) next[4] = prev[4] + path[2];
                if (next[4] == null || next[4] > prev[4] + path[3]) next[4] = prev[4] + path[3];
                if (next[3] == null || next[3] > prev[4] + path[4]) next[3] = prev[4] + path[4];
                if (!hasPicks && (next[4] == null || next[4] > prev[4])) next[4] = prev[4];

                if (prev[5] != null && prev[5] != -1 && next[0] > prev[5] + path[0]) next[0] = prev[5] + path[0];
                if (prev[5] != null && prev[5] != -1 && next[1] > prev[5] + path[1]) next[1] = prev[5] + path[1];
                if (prev[5] != null && prev[5] != -1 && next[2] > prev[5] + path[2]) next[2] = prev[5] + path[2];
                if (prev[5] != null && prev[5] != -1 && next[4] > prev[5] + path[3]) next[4] = prev[5] + path[3];
                if (prev[5] != null && prev[5] != -1 && next[3] > prev[5] + path[4]) next[3] = prev[5] + path[4];

                if (!hasPicks)
                {
                    if (prev[5] != null && prev[5] != -1 && next[5] == null) next[5] = prev[5] + path[5];
                    else if (prev[5] != null && prev[5] != -1 && next[5] > prev[5] + path[5]) next[5] = prev[5] + path[5];
                }

                if (!hasPicks)
                {
                    if (prev[6] != null && prev[6] != -1 && next[6] == null) next[6] = prev[6] + path[5];
                    else if (prev[6] != null && prev[6] != -1 && next[6] > prev[6] + path[5]) next[6] = prev[6] + path[5];
                }
            }

            step++;
        }

        step--;

        var min = double.MaxValue;
        for (var j = 0; j < table[step].Length; j++)
        {
            var value = table[step][j];
            if (value is > 0 && min > value && j is not 0 and not 4 and not 5) min = value.Value;
        }

        return new PickRoute(batch, [depot], min);
    }
}
