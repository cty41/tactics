using Tactics.Core.Board;
using Tactics.Core.Content;

namespace Tactics.Core.Runs;

public enum AdventureObjectKind { Campfire, Merchant, Npc, Chest, Altar, Exit, Rest, Store, Treasure, Battle, Event, Escort, RouteSubmit }

public sealed record AdventureBoardObject(
    string ObjectId,
    AdventureObjectKind Kind,
    GridPoint Cell,
    bool BlocksMovement,
    bool BlocksLineOfSight,
    string? TargetNodeId = null,
    bool IsLocked = false);

public sealed record AdventureActorPlacement(string ActorId, GridPoint Cell);

/// <summary>Engine-neutral, fixed-size exploration board consumed by Godot TileMapLayer rendering.</summary>
public sealed record AdventureBoardDefinition(
    ContentId ContentId,
    int Width,
    int Height,
    IReadOnlyList<GridPoint> BlockedCells,
    IReadOnlyList<AdventureBoardObject> Objects,
    IReadOnlyList<AdventureActorPlacement> Actors,
    GridPoint EntryCell,
    GridPoint ExitCell)
{
    public const int RequiredSize = 10;

    public void Validate()
    {
        if (Width != RequiredSize || Height != RequiredSize)
            throw new ArgumentException($"Adventure boards must be {RequiredSize}x{RequiredSize}.");
        GridPoint[] allCells = BlockedCells.Concat(Objects.Select(value => value.Cell))
            .Concat(Actors.Select(value => value.Cell)).Append(EntryCell).Append(ExitCell).ToArray();
        if (allCells.Any(cell => !Contains(cell))) throw new ArgumentException("Adventure board content must be inside the board.");
        if (BlockedCells.Distinct().Count() != BlockedCells.Count) throw new ArgumentException("Blocked cells must be unique.");
        if (Objects.Select(value => value.ObjectId).Distinct(StringComparer.Ordinal).Count() != Objects.Count)
            throw new ArgumentException("Adventure object ids must be unique.");
        if (Actors.Select(value => value.ActorId).Distinct(StringComparer.Ordinal).Count() != Actors.Count)
            throw new ArgumentException("Adventure actor ids must be unique.");
        if (Actors.Select(value => value.Cell).Distinct().Count() != Actors.Count)
            throw new ArgumentException("Adventure actors cannot share a cell.");
        if (Objects.GroupBy(value => value.Cell).Any(group => group.Count() > 1))
            throw new ArgumentException("Adventure objects cannot share a cell.");
        if (BlockedCells.Contains(EntryCell) || BlockedCells.Contains(ExitCell) ||
            Objects.Any(value => value.BlocksMovement && (value.Cell == EntryCell || value.Cell == ExitCell)))
            throw new ArgumentException("Entry and exit cells must be walkable.");
        if (Actors.Any(actor => BlockedCells.Contains(actor.Cell) ||
            Objects.Any(value => value.BlocksMovement && value.Cell == actor.Cell)))
            throw new ArgumentException("Adventure actors must occupy walkable, unblocked cells.");
        if (Actors.Any(actor => actor.Cell == EntryCell || actor.Cell == ExitCell))
            throw new ArgumentException("Entry and exit cells cannot be occupied by actors.");
    }

    public bool Contains(GridPoint cell) => cell.X >= 0 && cell.X < Width && cell.Y >= 0 && cell.Y < Height;

    public bool IsWalkable(GridPoint cell) => Contains(cell) && !BlockedCells.Contains(cell) &&
        !Objects.Any(value => value.Cell == cell && value.BlocksMovement);
}

public static class AdventureBoardPathfinder
{
    private static readonly GridPoint[] Directions = [new(0, -1), new(-1, 0), new(1, 0), new(0, 1)];

    public static IReadOnlyList<GridPoint> FindPath(AdventureBoardDefinition board, GridPoint origin, GridPoint destination,
        IReadOnlyCollection<GridPoint>? occupiedCells = null)
    {
        ArgumentNullException.ThrowIfNull(board);
        board.Validate();
        var occupied = occupiedCells is null ? new HashSet<GridPoint>() : occupiedCells.ToHashSet();
        occupied.Remove(origin);
        if (!board.IsWalkable(origin) || !board.IsWalkable(destination) || occupied.Contains(destination)) return Array.Empty<GridPoint>();
        var frontier = new Queue<GridPoint>();
        var previous = new Dictionary<GridPoint, GridPoint?> { [origin] = null };
        frontier.Enqueue(origin);
        while (frontier.Count > 0)
        {
            GridPoint current = frontier.Dequeue();
            if (current == destination) break;
            foreach (GridPoint direction in Directions)
            {
                GridPoint next = new(current.X + direction.X, current.Y + direction.Y);
                if (!board.IsWalkable(next) || occupied.Contains(next) || previous.ContainsKey(next)) continue;
                previous[next] = current;
                frontier.Enqueue(next);
            }
        }
        if (!previous.ContainsKey(destination)) return Array.Empty<GridPoint>();
        var path = new List<GridPoint>();
        for (GridPoint? current = destination; current.HasValue; current = previous[current.Value]) path.Add(current.Value);
        path.Reverse();
        return path;
    }
}
