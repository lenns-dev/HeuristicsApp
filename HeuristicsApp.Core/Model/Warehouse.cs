namespace HeuristicsApp.Core;

/// <summary>Punto 2D en la rejilla del almacén.</summary>
public readonly record struct Coordinate(double X, double Y)
{
    /// <summary>Distancia rectilínea (Manhattan), la natural al moverse por pasillos.</summary>
    public double DistanceTo(Coordinate other)
        => Math.Abs(X - other.X) + Math.Abs(Y - other.Y);
}

/// <summary>Pasillo vertical del almacén. Las ubicaciones cuelgan de un pasillo.</summary>
public sealed record Aisle(int Id, double X, double FrontY, double BackY);

/// <summary>Hueco de picking asociado a un SKU dentro de un pasillo.</summary>
public sealed record StorageLocation(string Sku, int AisleId, Coordinate Position);

/// <summary>
/// Medidas físicas del almacén, en las unidades de la instancia.
/// AisleSpacing es la distancia entre ejes de pasillos consecutivos.
/// </summary>
public sealed record WarehouseDimensions(
    double Width,
    double Length,
    double AisleSpacing,
    double AisleWidth,
    double ShelfWidth);

/// <summary>Topología física del almacén: depósito, pasillos y ubicaciones.</summary>
public sealed record WarehouseLayout(
    Coordinate Depot,
    IReadOnlyList<Aisle> Aisles,
    IReadOnlyDictionary<string, StorageLocation> LocationsBySku,
    WarehouseDimensions? Dimensions = null)
{
    /// <summary>Distancia entre dos puntos según la métrica del almacén.</summary>
    public double Distance(Coordinate a, Coordinate b) => a.DistanceTo(b);
}