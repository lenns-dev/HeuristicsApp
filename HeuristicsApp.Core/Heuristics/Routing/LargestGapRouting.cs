namespace HeuristicsApp.Core;

/// <summary>
/// Routing por mayor hueco (Largest Gap): en cada pasillo visitado el picker
/// entra y sale por el mismo extremo, evitando recorrer el "hueco" más grande
/// entre artículos consecutivos (o respecto a los pasillos transversales).
/// </summary>
public sealed class LargestGapRouting : IRoutingStrategy
{
    public string Key => "largest-gap";
    public string DisplayName => "Largest Gap";

    public PickRoute Route(Batch batch, WarehouseLayout layout, CancellationToken ct)
    {
        // TODO: 1) mapear líneas -> ubicaciones por pasillo,
        //       2) en cada pasillo localizar el mayor gap entre picks,
        //       3) recorrer dejando ese gap sin atravesar, 4) acumular distancia.
        throw new NotImplementedException();
    }
}