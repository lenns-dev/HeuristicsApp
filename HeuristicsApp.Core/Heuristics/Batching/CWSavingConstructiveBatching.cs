namespace HeuristicsApp.Core;

/// <summary>
/// Heurística constructiva Clarke-Wright Savings para batching: inicializa un lote
/// por pedido y los fusiona iterativamente eligiendo siempre la pareja cuya unión
/// produce el mayor ahorro en coste de routing, respetando la capacidad del picker.
/// </summary>
public sealed class CWSavingConstructiveBatching(IRoutingStrategy routing) : IBatchingStrategy
{
    public string Key => "cwSavingConstructive";
    public string DisplayName => "CW Saving Constructive";

    public IReadOnlyList<Batch> CreateBatches(WarehouseProblem problem, CancellationToken ct)
    {
        var orders = problem.Orders.ToList();
        if (orders.Count == 0) return [];

        return ApplyClarkeAndWright(orders, problem, ct);
    }

    private List<Batch> ApplyClarkeAndWright(List<Order> initialOrders, WarehouseProblem problem, CancellationToken ct)
    {
        if (initialOrders.Count == 1)
            return [new Batch(0, initialOrders)];

        if (initialOrders.Count == 2)
        {
            var w0 = OrderWeight(initialOrders[0]);
            var w1 = OrderWeight(initialOrders[1]);
            return w0 + w1 <= problem.Capacity.MaxWeight
                ? [new Batch(0, initialOrders)]
                : [new Batch(0, [initialOrders[0]]), new Batch(1, [initialOrders[1]])];
        }

        var batches = initialOrders.Select((o, i) => new Batch(i, [o])).ToList();

        while (true)
        {
            ct.ThrowIfCancellationRequested();
            var updated = CombineBestBatches(batches, problem, ct);
            if (updated is null) break;
            batches = updated;
        }

        return batches.Select((b, i) => new Batch(i, b.Orders)).ToList();
    }

    private List<Batch>? CombineBestBatches(List<Batch> batches, WarehouseProblem problem, CancellationToken ct)
    {
        Batch? bestA = null, bestB = null;
        double bestSaving = double.MinValue;
        var hasChange = false;

        for (var i = 0; i < batches.Count; i++)
        {
            for (var j = i + 1; j < batches.Count; j++)
            {
                ct.ThrowIfCancellationRequested();

                if (BatchWeight(batches[i]) + BatchWeight(batches[j]) > problem.Capacity.MaxWeight)
                    continue;

                var combined = MergeBatches(batches[i], batches[j]);
                var costI = routing.Route(batches[i], problem.Layout, ct).Distance;
                var costJ = routing.Route(batches[j], problem.Layout, ct).Distance;
                var combinedCost = routing.Route(combined, problem.Layout, ct).Distance;
                var saving = costI + costJ - combinedCost;

                if (saving > bestSaving)
                {
                    bestSaving = saving;
                    bestA = batches[i];
                    bestB = batches[j];
                    hasChange = true;
                }
            }
        }

        if (!hasChange) return null;

        var result = new List<Batch>(batches);
        result.Remove(bestA!);
        result.Remove(bestB!);
        result.Add(MergeBatches(bestA!, bestB!));
        return result;
    }

    private static Batch MergeBatches(Batch a, Batch b) =>
        new(0, [.. a.Orders, .. b.Orders]);

    private static double OrderWeight(Order order) =>
        order.Lines.Sum(l => l.Quantity * l.UnitWeight);

    private static double BatchWeight(Batch batch) =>
        batch.Orders.Sum(OrderWeight);
}
