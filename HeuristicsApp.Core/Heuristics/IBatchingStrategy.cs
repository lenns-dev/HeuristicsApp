using HeuristicsApp.Core;

public interface IBatchingStrategy
{
    string Key { get; }            // "savings"
    string DisplayName { get; }    // "Clarke-Wright (Savings)"
    IReadOnlyList<Batch> CreateBatches(WarehouseProblem problem, CancellationToken ct);
}