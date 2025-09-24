using System.Collections.Generic;
using UnityEngine;

public enum RoomType { Start, Normal, End }
public enum Direction { North, East, South, West }

public static class DirectionExtensions
{
    public static Vector3Int ToVector3Int(this Direction dir)
    {
        switch (dir)
        {
            case Direction.North: return new Vector3Int(0, 0, 1);
            case Direction.East: return new Vector3Int(1, 0, 0);
            case Direction.South: return new Vector3Int(0, 0, -1);
            case Direction.West: return new Vector3Int(-1, 0, 0);
            default: return Vector3Int.zero;
        }
    }

    public static Direction Opposite(this Direction dir)
    {
        switch (dir)
        {
            case Direction.North: return Direction.South;
            case Direction.East: return Direction.West;
            case Direction.South: return Direction.North;
            case Direction.West: return Direction.East;
            default: return Direction.North;
        }
    }
}

[System.Serializable]
public class RoomTemplate
{
    public GameObject prefab;
    public Vector2Int size = new Vector2Int(1, 1);
    public List<Direction> availableDoors = new List<Direction>();
}

[System.Serializable]
public class PlacedRoom
{
    public GameObject instance;
    public RoomTemplate template;
    public Vector3Int gridPosition;
    public RoomType type;
    public List<Direction> usedDoors = new List<Direction>();

    public bool HasAvailableDoor()
    {
        return usedDoors.Count < template.availableDoors.Count;
    }

    public Direction GetRandomAvailableDoor(System.Random rng)
    {
        var available = new List<Direction>();
        foreach (var door in template.availableDoors)
        {
            if (!usedDoors.Contains(door))
                available.Add(door);
        }
        return available.Count > 0 ? available[rng.Next(available.Count)] : Direction.North;
    }
}