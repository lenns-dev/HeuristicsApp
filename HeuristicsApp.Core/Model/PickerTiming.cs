namespace HeuristicsApp.Core;

/// <summary>
/// Modelo de tiempos del picker, en las unidades de la instancia: TravelSpeed en distancia
/// por unidad de tiempo y PickTimePerItem en unidades de tiempo por unidad recogida.
/// Un lote dura su recorrido a TravelSpeed más PickTimePerItem por cada unidad que contiene.
/// </summary>
public sealed record PickerTiming(double TravelSpeed, double PickTimePerItem)
{
    /// <summary>Velocidad usada cuando la instancia no define una válida.</summary>
    public const double DefaultTravelSpeed = 1.2;

    public static readonly PickerTiming Default = new(DefaultTravelSpeed, 0);

    /// <summary>Construye el modelo desde los valores de la instancia, saneando ausentes o negativos.</summary>
    public static PickerTiming From(double travelSpeed, double pickTimePerItem)
        => new(travelSpeed > 0 ? travelSpeed : DefaultTravelSpeed, Math.Max(0, pickTimePerItem));

    public double TravelTime(double distance) => distance / TravelSpeed;

    public double PickTime(int units) => units * PickTimePerItem;

    /// <summary>Duración total de un lote: desplazamiento más recogida de todas sus unidades.</summary>
    public double BatchTime(PickRoute route)
        => TravelTime(route.Distance) + PickTime(route.Batch.Lines.Sum(l => l.Quantity));
}
