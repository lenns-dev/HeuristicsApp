namespace HeuristicsApp.Core;

/// <summary>
/// Batching por semilla (Seed Algorithm): elige un pedido semilla y le añade
/// pedidos según un criterio de proximidad hasta agotar la capacidad del picker.
/// </summary>
public sealed class SeedBatching : IBatchingStrategy
{
    public string Key => "seed";
    public string DisplayName => "Seed Algorithm";

    public IReadOnlyList<Batch> CreateBatches(WarehouseProblem problem, CancellationToken ct)
    {
        // TODO: 1) seleccionar semilla, 2) regla de adición por proximidad,
        //       3) respetar problem.Capacity (MaxItems / MaxWeight).
        throw new NotImplementedException();
    }
}