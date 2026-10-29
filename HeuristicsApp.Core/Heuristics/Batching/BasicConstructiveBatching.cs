namespace HeuristicsApp.Core;

/// <summary>
/// Batching constructivo básico: procesa los pedidos en orden y los asigna a lotes
/// respetando la capacidad en peso del picker. En modo compacto intenta rellenar los
/// lotes ya abiertos antes de crear uno nuevo; en modo no compacto sólo evalúa el último.
/// </summary>
public sealed class BasicConstructiveBatching(bool compact = true) : IBatchingStrategy
{
    public string Key => "basicConstructive";
    public string DisplayName => "Basic Constructive";

    public IReadOnlyList<Batch> CreateBatches(WarehouseProblem problem, IRoutingStrategy routing, CancellationToken ct)
    {
        var orders = problem.Orders.ToList();
        var cap = problem.Capacity;

        var batchOrders = new List<List<Order>>();
        var batchWeights = new List<double>();

        if (orders.Count == 0)
            return [];

        foreach (var order in orders)
        {
            ct.ThrowIfCancellationRequested();

            var orderWeight = order.Lines.Sum(l => l.Quantity * l.UnitWeight);
            var assigned = false;

            if (compact)
            {
                for (var i = 0; i < batchOrders.Count; i++)
                {
                    if (cap.MaxWeight - batchWeights[i] >= orderWeight)
                    {
                        batchOrders[i].Add(order);
                        batchWeights[i] += orderWeight;
                        assigned = true;
                        break;
                    }
                }
            }
            else
            {
                if (batchOrders.Count > 0)
                {
                    var last = batchOrders.Count - 1;
                    if (cap.MaxWeight - batchWeights[last] >= orderWeight)
                    {
                        batchOrders[last].Add(order);
                        batchWeights[last] += orderWeight;
                        assigned = true;
                    }
                }
            }

            if (!assigned)
            {
                if (orderWeight > cap.MaxWeight)
                    throw new InvalidOperationException(
                        "Error de datos: un pedido individual supera el peso máximo del lote.");

                batchOrders.Add([order]);
                batchWeights.Add(orderWeight);
            }
        }

        return batchOrders
            .Select((o, i) => new Batch(i, o))
            .ToList();
    }
}