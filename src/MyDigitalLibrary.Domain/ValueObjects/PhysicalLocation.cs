namespace MyDigitalLibrary.Domain.ValueObjects;

public sealed record PhysicalLocation
{
    public string? Room { get; }
    public string? Shelf { get; }
    public string? Box { get; }

    public PhysicalLocation(string? room, string? shelf, string? box)
    {
        if (room is null && shelf is null && box is null)
            throw new ArgumentException("A physical location must specify at least one of room, shelf, or box.");

        Room = room;
        Shelf = shelf;
        Box = box;
    }
}
