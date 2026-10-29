using HeuristicsApp.Core;

public interface IBatchingStrategy
{
    string Key { get; }            // "savings"
    string DisplayName { get; }    // "Clarke-Wright (Savings)"
    /// <summary>
    /// Agrupa los pedidos en lotes. <paramref name="routing"/> es el routing elegido para el plan,
    /// para las heurísticas que evalúan el coste de recorrer cada lote (las demás lo ignoran).
    /// </summary>
    IReadOnlyList<Batch> CreateBatches(WarehouseProblem problem, IRoutingStrategy routing, CancellationToken ct);
}