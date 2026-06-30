namespace HeuristicsApp.Core.Instances;

internal sealed record InstanceFileDto(
    string? NameInstanceSet,
    string? Url,
    IReadOnlyList<InstanceSetDto>? InstanceSet);

internal sealed record InstanceSetDto(
    IReadOnlyList<LayoutDto>? Layout,
    IReadOnlyList<OrdersDto>? Orders);

internal sealed record LayoutDto(
    int NumberOfAisles,
    int NumberOfItems,
    int NumberOfSlots,
    int DepotPlacement,
    int OrderLocation,
    double ShelfLength,
    double ShelfWidth,
    double AisleWidth,
    int CrossAisles,
    double WorkerCapacity,
    double PickingTime,
    double OutsideTurnTime,
    double InsideTurnTime,
    double TravelSpeed,
    IReadOnlyList<AisleDto>? Aisles);

internal sealed record AisleDto(
    int AisleId,
    double DistanceFromOriginRight,
    double DistanceFromOriginLeft,
    int Side);

internal sealed record OrdersDto(int NumOrders, IReadOnlyList<OrderDto>? OrderList);

internal sealed record OrderDto(
    int Id,
    long DueDate,
    long ArrivalTime,
    double ServiceTime,
    int NumReferences,
    double Weight,
    double CompletionTime,
    IReadOnlyList<ProductDto>? Products);

internal sealed record ProductDto(int Id, int Aisle, int Side, double HeightPosition, double Weight);