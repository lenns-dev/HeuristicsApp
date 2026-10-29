namespace HeuristicsApp.Core;

/// <summary>
/// Batching aleatorio: baraja los pedidos con una semilla fija y los
/// empaqueta en orden respetando la capacidad del picker. No optimiza nada; sirve
/// como punto de comparación y para ejercitar el resto del pipeline.
/// </summary>
public sealed class RandomBatching(int seed = 42) : IBatchingStrategy
{
    public string Key => "random";
    public string DisplayName => "Random";

    public IReadOnlyList<Batch> CreateBatches(WarehouseProblem problem, IRoutingStrategy routing, CancellationToken ct)
    {
        var rng = new Random(seed);
        var shuffled = problem.Orders.OrderBy(_ => rng.Next()).ToList();
        var cap = problem.Capacity;

        var batches = new List<Batch>();
        var current = new List<Order>();
        var items = 0;
        var weight = 0d;
        var batchId = 0;

        foreach (var order in shuffled)
        {
            ct.ThrowIfCancellationRequested();

            var orderItems = order.Lines.Sum(l => l.Quantity);
            var orderWeight = order.Lines.Sum(l => l.Quantity * l.UnitWeight);

            // Cierra el lote en curso si el pedido no cabe (salvo que esté vacío:
            // un pedido que por sí solo excede la capacidad va en su propio lote).
            if (current.Count > 0 &&
                (items + orderItems > cap.MaxItems || weight + orderWeight > cap.MaxWeight))
            {
                batches.Add(new Batch(batchId++, current));
                current = [];
                items = 0;
                weight = 0;
            }

            current.Add(order);
            items += orderItems;
            weight += orderWeight;
        }

        if (current.Count > 0)
            batches.Add(new Batch(batchId, current));

        return batches;
    }
}