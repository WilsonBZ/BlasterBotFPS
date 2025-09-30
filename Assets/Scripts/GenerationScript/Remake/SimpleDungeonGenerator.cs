using System.Collections.Generic;
using UnityEngine;
using System;

public class SimpleDungeonGenerator : MonoBehaviour
{
    [Header("Room Templates")]
    public List<RoomTemplate> startRooms = new List<RoomTemplate>();
    public List<RoomTemplate> normalRooms = new List<RoomTemplate>();
    public List<RoomTemplate> endRooms = new List<RoomTemplate>();

    [Header("Corridor Prefabs")]
    public GameObject straightCorridorPrefab;
    public GameObject cornerCorridorPrefab;

    [Header("Generation Settings")]
    public int minRooms = 4;
    public int maxRooms = 8;
    public int seed = 0;
    public float gridSize = 4f;
    public int maxAttemptsPerRoom = 50;
    public int maxGenerationAttempts = 10;

    [Header("Debug")]
    public bool drawGizmos = true;
    public Color gridColor = Color.gray;

    private System.Random rng;
    private HashSet<Vector3Int> occupiedCells = new HashSet<Vector3Int>();
    private List<PlacedRoom> placedRooms = new List<PlacedRoom>();
    private List<GameObject> corridorPieces = new List<GameObject>();

    void Start()
    {
        GenerateDungeon();
    }

    [ContextMenu("Generate Dungeon")]
    public void GenerateDungeon()
    {
        ClearDungeon();

        rng = new System.Random(seed == 0 ? DateTime.Now.Millisecond : seed);

        bool success = false;
        for (int attempt = 0; attempt < maxGenerationAttempts && !success; attempt++)
        {
            success = TryGenerateDungeon();
            if (!success)
            {
                Debug.LogWarning($"Generation attempt {attempt + 1} failed, retrying...");
                ClearDungeon();
            }
        }

        if (success)
        {
            Debug.Log($"Dungeon generated successfully with {placedRooms.Count} rooms!");
        }
        else
        {
            Debug.LogError("Failed to generate dungeon after multiple attempts!");
        }
    }

    [ContextMenu("Clear Dungeon")]
    public void ClearDungeon()
    {
        foreach (var room in placedRooms)
        {
            if (room.instance != null)
                DestroyImmediate(room.instance);
        }
        placedRooms.Clear();

        foreach (var corridor in corridorPieces)
        {
            if (corridor != null)
                DestroyImmediate(corridor);
        }
        corridorPieces.Clear();

        occupiedCells.Clear();
    }

    private bool TryGenerateDungeon()
    {
        int targetRoomCount = rng.Next(minRooms, maxRooms + 1);

        if (!PlaceStartRoom())
            return false;

        for (int i = 1; i < targetRoomCount; i++)
        {
            bool isEndRoom = (i == targetRoomCount - 1);

            if (!PlaceNextRoom(isEndRoom))
                return false;
        }

        return true;
    }

    private bool PlaceStartRoom()
    {
        if (startRooms.Count == 0)
        {
            Debug.LogError("No start rooms available!");
            return false;
        }

        var startTemplate = startRooms[rng.Next(startRooms.Count)];
        Vector3Int startPos = Vector3Int.zero;

        if (!CanPlaceRoom(startPos, startTemplate.size))
            return false;

        var startRoom = CreateRoom(startTemplate, startPos, RoomType.Start);
        placedRooms.Add(startRoom);
        MarkAreaOccupied(startPos, startTemplate.size);

        return true;
    }

    private bool PlaceNextRoom(bool isEndRoom)
    {
        for (int attempt = 0; attempt < maxAttemptsPerRoom; attempt++)
        {
            var sourceRoom = GetRandomRoomWithAvailableDoors();
            if (sourceRoom == null)
                return false;

            var exitDirection = sourceRoom.GetRandomAvailableDoor(rng);

            if (TryPlaceRoomFromDoor(sourceRoom, exitDirection, isEndRoom, out PlacedRoom newRoom))
            {
                if (CreateCorridorBetweenRooms(sourceRoom, exitDirection, newRoom))
                {
                    sourceRoom.usedDoors.Add(exitDirection);
                    placedRooms.Add(newRoom);
                    return true;
                }
                else
                {
                    if (newRoom.instance != null)
                        DestroyImmediate(newRoom.instance);
                }
            }
        }

        return false;
    }

    private bool TryPlaceRoomFromDoor(PlacedRoom sourceRoom, Direction exitDirection, bool isEndRoom, out PlacedRoom newRoom)
    {
        newRoom = null;

        var availableTemplates = isEndRoom ? endRooms : normalRooms;
        if (availableTemplates.Count == 0)
            return false;

        var exitPos = GetDoorWorldPosition(sourceRoom, exitDirection);
        var requiredEntranceDirection = exitDirection.Opposite();

        for (int straightLength = 1; straightLength <= 3; straightLength++)
        {
            for (int turnType = 0; turnType < 3; turnType++) 
            {
                Vector3Int targetPos;
                Direction entranceDir;

                if (turnType == 0) 
                {
                    targetPos = exitPos + exitDirection.ToVector3Int() * (straightLength + 1);
                    entranceDir = requiredEntranceDirection;
                }
                else 
                {
                    Direction turnDirection = (turnType == 1) ?
                        RotateDirection(exitDirection, -1) : 
                        RotateDirection(exitDirection, 1);   

                    targetPos = exitPos +
                               exitDirection.ToVector3Int() * straightLength +
                               turnDirection.ToVector3Int() * 2;
                    entranceDir = turnDirection.Opposite();
                }

                foreach (var template in availableTemplates)
                {
                    if (template.availableDoors.Contains(entranceDir))
                    {
                        Vector3Int adjustedPos = AdjustRoomPosition(targetPos, entranceDir, template.size);

                        if (CanPlaceRoom(adjustedPos, template.size))
                        {
                            newRoom = CreateRoom(template, adjustedPos, isEndRoom ? RoomType.End : RoomType.Normal);
                            MarkAreaOccupied(adjustedPos, template.size);
                            return true;
                        }
                    }
                }
            }
        }

        return false;
    }

    private bool CreateCorridorBetweenRooms(PlacedRoom roomA, Direction dirFromA, PlacedRoom roomB)
    {
        try
        {
            var startPos = GetDoorWorldPosition(roomA, dirFromA);
            var endPos = GetDoorWorldPosition(roomB, dirFromA.Opposite());

            if (startPos.x == endPos.x || startPos.z == endPos.z)
            {
                CreateStraightCorridor(startPos, endPos);
            }
            else
            {
                CreateLShapedCorridor(startPos, endPos);
            }

            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to create corridor: {e.Message}");
            return false;
        }
    }

    private void CreateStraightCorridor(Vector3Int start, Vector3Int end)
    {
        Vector3Int difference = end - start;
        Vector3Int direction = new Vector3Int(
            difference.x != 0 ? Math.Sign(difference.x) : 0,
            0,
            difference.z != 0 ? Math.Sign(difference.z) : 0
        );

        int distance = Math.Max(Math.Abs(difference.x), Math.Abs(difference.z));

        for (int i = 1; i < distance; i++)
        {
            Vector3Int corridorPos = start + direction * i;
            Quaternion rotation = Quaternion.LookRotation(new Vector3(direction.x, 0, direction.z));

            var corridor = Instantiate(straightCorridorPrefab,
                GridToWorld(corridorPos), rotation, transform);
            corridorPieces.Add(corridor);

            occupiedCells.Add(corridorPos);
        }
    }

    private void CreateLShapedCorridor(Vector3Int start, Vector3Int end)
    {
        Vector3Int corner = new Vector3Int(end.x, 0, start.z);

        int xStep = Math.Sign(end.x - start.x);
        for (int x = start.x + xStep; x != end.x; x += xStep)
        {
            Vector3Int pos = new Vector3Int(x, 0, start.z);
            Quaternion rot = Quaternion.LookRotation(new Vector3(xStep, 0, 0));

            var corridor = Instantiate(straightCorridorPrefab, GridToWorld(pos), rot, transform);
            corridorPieces.Add(corridor);
            occupiedCells.Add(pos);
        }

        int zStep = Math.Sign(end.z - start.z);
        Quaternion cornerRot = GetCornerRotation(new Vector3Int(xStep, 0, 0), new Vector3Int(0, 0, zStep));
        var cornerPiece = Instantiate(cornerCorridorPrefab, GridToWorld(corner), cornerRot, transform);
        corridorPieces.Add(cornerPiece);
        occupiedCells.Add(corner);

        for (int z = start.z + zStep; z != end.z; z += zStep)
        {
            Vector3Int pos = new Vector3Int(end.x, 0, z);
            Quaternion rot = Quaternion.LookRotation(new Vector3(0, 0, zStep));

            var corridor = Instantiate(straightCorridorPrefab, GridToWorld(pos), rot, transform);
            corridorPieces.Add(corridor);
            occupiedCells.Add(pos);
        }
    }

    private Quaternion GetCornerRotation(Vector3Int inDir, Vector3Int outDir)
    {
        if (inDir.x == 1 && outDir.z == 1) return Quaternion.Euler(0, 0, 0);    // E → N
        if (inDir.x == 1 && outDir.z == -1) return Quaternion.Euler(0, 90, 0);   // E → S
        if (inDir.x == -1 && outDir.z == 1) return Quaternion.Euler(0, 270, 0);  // W → N
        if (inDir.x == -1 && outDir.z == -1) return Quaternion.Euler(0, 180, 0); // W → S
        if (inDir.z == 1 && outDir.x == 1) return Quaternion.Euler(0, 0, 0);     // N → E
        if (inDir.z == 1 && outDir.x == -1) return Quaternion.Euler(0, 270, 0);  // N → W
        if (inDir.z == -1 && outDir.x == 1) return Quaternion.Euler(0, 90, 0);   // S → E
        if (inDir.z == -1 && outDir.x == -1) return Quaternion.Euler(0, 180, 0); // S → W

        return Quaternion.identity;
    }

    private PlacedRoom GetRandomRoomWithAvailableDoors()
    {
        var availableRooms = new List<PlacedRoom>();
        foreach (var room in placedRooms)
        {
            if (room.HasAvailableDoor())
                availableRooms.Add(room);
        }
        return availableRooms.Count > 0 ? availableRooms[rng.Next(availableRooms.Count)] : null;
    }

    private Vector3Int GetDoorWorldPosition(PlacedRoom room, Direction doorDirection)
    {
        var roomCenter = room.gridPosition + new Vector3Int(room.template.size.x / 2, 0, room.template.size.y / 2);
        var doorOffset = doorDirection.ToVector3Int() * (Math.Max(room.template.size.x, room.template.size.y) / 2);
        return roomCenter + doorOffset;
    }

    private Vector3Int AdjustRoomPosition(Vector3Int targetPos, Direction entranceDir, Vector2Int roomSize)
    {
        var entranceOffset = entranceDir.Opposite().ToVector3Int() * (roomSize.x / 2);
        return targetPos + entranceOffset;
    }

    private bool CanPlaceRoom(Vector3Int position, Vector2Int size)
    {
        for (int x = 0; x < size.x; x++)
        {
            for (int z = 0; z < size.y; z++)
            {
                var checkPos = position + new Vector3Int(x, 0, z);
                if (occupiedCells.Contains(checkPos))
                    return false;
            }
        }
        return true;
    }

    private void MarkAreaOccupied(Vector3Int position, Vector2Int size)
    {
        for (int x = 0; x < size.x; x++)
        {
            for (int z = 0; z < size.y; z++)
            {
                occupiedCells.Add(position + new Vector3Int(x, 0, z));
            }
        }
    }

    private PlacedRoom CreateRoom(RoomTemplate template, Vector3Int gridPos, RoomType type)
    {
        var worldPos = GridToWorld(gridPos);
        var instance = Instantiate(template.prefab, worldPos, Quaternion.identity, transform);

        return new PlacedRoom
        {
            instance = instance,
            template = template,
            gridPosition = gridPos,
            type = type
        };
    }

    private Vector3 GridToWorld(Vector3Int gridPos)
    {
        return new Vector3(gridPos.x * gridSize, 0, gridPos.z * gridSize);
    }

    private Direction RotateDirection(Direction dir, int steps)
    {
        int newDir = ((int)dir + steps) % 4;
        if (newDir < 0) newDir += 4;
        return (Direction)newDir;
    }

    private void OnDrawGizmos()
    {
        if (!drawGizmos) return;

        Gizmos.color = gridColor;
        foreach (var cell in occupiedCells)
        {
            Gizmos.DrawWireCube(GridToWorld(cell) + new Vector3(gridSize / 2, 0, gridSize / 2),
                               new Vector3(gridSize, 0.1f, gridSize));
        }
    }
}