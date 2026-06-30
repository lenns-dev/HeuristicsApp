using HeuristicsApp.Core;

public interface IRoutingStrategy
{
    string Key { get; }
    string DisplayName { get; }
    PickRoute Route(Batch batch, WarehouseLayout layout, CancellationToken ct);
}