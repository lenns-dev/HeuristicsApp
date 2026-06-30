namespace HeuristicsApp.Core;

/// <summary>
/// Batching por ahorros (Clarke-Wright Savings): parte de un lote por pedido y
/// los fusiona iterativamente priorizando las parejas con mayor "ahorro" de
/// distancia, mientras la capacidad del picker lo permita.
/// </summary>
public sealed class SavingsBatching : IBatchingStrategy
{
    public string Key => "savings";
    public string DisplayName => "Clarke-Wright (Savings)";

    public IReadOnlyList<Batch> CreateBatches(WarehouseProblem problem, CancellationToken ct)
    {
        // TODO: 1) lote inicial por pedido (depot -> pedido -> depot),
        //       2) calcular savings s(i,j) = d(0,i) + d(0,j) - d(i,j),
        //       3) fusionar por ahorro descendente respetando problem.Capacity.
        throw new NotImplementedException();
    }
}