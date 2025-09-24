// ProceduralPathDungeon_Generator.cs
// Single-file collection of Unity C# scripts for a deterministic, grid-based
// procedural generator that places preset room prefabs connected by straight
// and L-shaped hallways (90-degree turns only). The generator produces a
// single path (no loops), where each intermediate room has exactly 2 doors
using System;
using System.Collections.Generic;
using UnityEngine;

#region DirectionHelpers
public enum Direction { North = 0, East = 1, South = 2, West = 3 }

public static class DirectionExtensions
{
    public static Vector2Int ToVec2Int(this Direction d)
    {
        switch (d)
        {
            case Direction.North: return new Vector2Int(0, 1);
            case Direction.East: return new Vector2Int(1, 0);
            case Direction.South: return new Vector2Int(0, -1);
            case Direction.West: return new Vector2Int(-1, 0);
            default: return Vector2Int.zero;
        }
    }

    public static Direction RotateClockwise(this Direction d) => (Direction)(((int)d + 1) & 3);
    public static Direction RotateCounterClockwise(this Direction d) => (Direction)(((int)d + 3) & 3);
    public static Direction Opposite(this Direction d) => (Direction)(((int)d + 2) & 3);

    // convert direction to a Y-axis rotation (Unity degrees)
    public static float ToYAngle(this Direction d)
    {
        switch (d)
        {
            case Direction.North: return 0f;
            case Direction.East: return 90f;
            case Direction.South: return 180f;
            case Direction.West: return 270f;
            default: return 0f;
        }
    }
}
#endregion

#region RoomAuthoring
[Serializable]
public struct RoomSocket
{
    // local grid coordinate with (0,0) == bottom-left cell of the room's footprint
    public Vector2Int localGridPos;
    public Direction orientation; // which way the door faces when room rotation == 0
    public string id; // optional debugging id
}

[DisallowMultipleComponent]
public class RoomAuthoring : MonoBehaviour
{
    [Tooltip("Room footprint size in grid cells. The room's root transform should be located at the bottom-left corner of this rectangle.")]
    public Vector2Int size = new Vector2Int(3, 3);

    [Tooltip("Define door sockets in local grid coordinates (0..size-1). Orientation is the direction the door faces at rotation 0.")]
    public List<RoomSocket> sockets = new List<RoomSocket>();

    // Optional: visualize sockets in editor
#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        // draw footprint
        Vector3 baseWorld = transform.position;
        float cell = 1f; // visual only
        Vector3 sizeWorld = new Vector3(size.x * cell, 0.01f, size.y * cell);
        Gizmos.DrawWireCube(baseWorld + new Vector3(sizeWorld.x / 2f, 0, sizeWorld.z / 2f), sizeWorld);

        Gizmos.color = Color.cyan;
        foreach (var s in sockets)
        {
            Vector3 sockPos = baseWorld + new Vector3((s.localGridPos.x + 0.5f) * cell, 0.01f, (s.localGridPos.y + 0.5f) * cell);
            Gizmos.DrawSphere(sockPos, 0.12f);
            UnityEditor.Handles.Label(sockPos + Vector3.up * 0.2f, s.orientation.ToString() + " " + s.id);
        }
    }
#endif
}
#endregion

#region RuntimeHelpers
internal class PlacedRoom
{
    public GameObject instance;
    public Vector2Int originGrid; // bottom-left grid cell where this room is placed
    public Vector2Int size; // rotated size
    public int rotationSteps; // number of 90deg clockwise rotations applied (0..3)
    public RoomAuthoring authoring;
    public int incomingSocketIndex; // index into authoring.sockets used to connect from previous room (or -1 for start)
    public int outgoingSocketIndex; // index into authoring.sockets chosen for the next connection or -1 for end
}

#endregion

#region DungeonGenerator
[DisallowMultipleComponent]
public class DungeonGenerator : MonoBehaviour
{
    [Header("Prefabs")]
    [Tooltip("Room prefabs: prefab root must contain a RoomAuthoring component describing size + sockets.")]
    public List<GameObject> roomPrefabs = new List<GameObject>();

    [Tooltip("1x1 straight corridor tile prefab. Should be created so its local forward (z) aligns with grid +Y (North)")]
    public GameObject corridorStraightPrefab;

    [Tooltip("1x1 corner corridor tile prefab. Should have geometry that fits one grid cell and turns a 90deg corner.")]
    public GameObject corridorCornerPrefab;

    [Header("Generation Settings")]
    public int minRooms = 4;
    public int maxRooms = 8;
    public int seed = 0;
    public float cellSize = 4f; // world units per grid cell
    public int maxCorridorStraightLen = 3; // maximum straight segment length
    public int maxCorridorTurnFirstLen = 2; // L-turn: number of straight cells before corner
    public int maxCorridorTurnSecondLen = 2; // L-turn: number of straight cells after corner

    [Tooltip("How many attempts to try placing a single next room before giving up and restarting the whole generation.")]
    public int maxAttemptsPerRoom = 200;

    [Tooltip("How many times to restart generation if stuck (increase if you frequently fail)")]
    public int maxRestarts = 5;

    [Tooltip("If true, generator runs at Start(). Otherwise call GeneratePath() manually.")]
    public bool generateOnStart = true;

    // internals
    private System.Random rng;
    private HashSet<Vector2Int> occupied = new HashSet<Vector2Int>();
    private List<GameObject> spawned = new List<GameObject>();

    [ContextMenu("GeneratePath")]
    public void GeneratePath()
    {
        ClearGenerated();
        rng = new System.Random(seed == 0 ? Environment.TickCount : seed);

        if (roomPrefabs == null || roomPrefabs.Count == 0)
        {
            Debug.LogError("No room prefabs assigned to DungeonGenerator.");
            return;
        }
        if (corridorStraightPrefab == null || corridorCornerPrefab == null)
        {
            Debug.LogError("Assign corridorStraightPrefab and corridorCornerPrefab.");
            return;
        }

        bool success = false;
        for (int restart = 0; restart <= maxRestarts && !success; restart++)
        {
            occupied.Clear();
            DestroySpawnedAndClearList();
            success = TryGenerateOnce();
            if (!success) Debug.LogWarning($"Generation attempt {restart + 1} failed; restarting...");
        }

        if (!success)
        {
            Debug.LogError("Dungeon generation failed after multiple restarts. Try more restarts or increase available room prefabs or grid space.");
        }
    }

    private bool TryGenerateOnce()
    {
        int targetRooms = rng.Next(minRooms, maxRooms + 1);
        // place start room at origin (0,0) on grid
        // pick random room that can be a start (must have at least 1 socket)
        int startPrefabIndex = rng.Next(roomPrefabs.Count);
        GameObject startPrefab = roomPrefabs[startPrefabIndex];
        RoomAuthoring startAuth = startPrefab.GetComponent<RoomAuthoring>();
        if (startAuth == null || startAuth.sockets.Count == 0)
        {
            Debug.LogError($"Room prefab {startPrefab.name} missing RoomAuthoring or sockets.");
            return false;
        }

        // instantiate start with a random outgoing socket and a random rotation
        int chosenStartSocketIndex = rng.Next(startAuth.sockets.Count);
        // choose rotation such that socket points to a random direction (we can choose any rot since outgoing will define d_prev)
        int startRotation = rng.Next(0, 4);
        Direction startOutgoingWorldDir = RotateDirection(startAuth.sockets[chosenStartSocketIndex].orientation, startRotation);

        // compute rotated size and ensure start origin at (0,0)
        Vector2Int startSizeRot = RotatedSize(startAuth.size, startRotation);
        Vector2Int startOriginGrid = Vector2Int.zero; // by convention place start at origin

        // check occupancy for start room
        if (!IsAreaFreeForRoom(startOriginGrid, startSizeRot))
        {
            Debug.LogError("Start room cannot be placed at origin. Please ensure your room prefabs are authored with the root at the bottom-left and not overlapping origin.");
            return false;
        }

        PlacedRoom prev = new PlacedRoom();
        prev.authoring = startAuth;
        prev.originGrid = startOriginGrid;
        prev.size = startSizeRot;
        prev.rotationSteps = startRotation;
        prev.incomingSocketIndex = -1; // no incoming socket for start
        prev.outgoingSocketIndex = chosenStartSocketIndex;

        // instantiate start in world
        GameObject startInst = Instantiate(startPrefab, GridToWorld(startOriginGrid), Quaternion.Euler(0, startRotation * 90f, 0), this.transform);
        spawned.Add(startInst);
        prev.instance = startInst;
        MarkAreaOccupied(prev.originGrid, prev.size);

        // compute prev socket world grid position (the socket's global grid cell)
        Vector2Int prevSocketGrid = GetWorldGridForSocket(prev);
        Direction prevOutgoingDir = startOutgoingWorldDir;

        List<Vector2Int> corridorCellsThisStep = new List<Vector2Int>();

        // place subsequent rooms
        List<PlacedRoom> placedRooms = new List<PlacedRoom>() { prev };

        for (int placedCount = 1; placedCount < targetRooms; placedCount++)
        {
            bool placed = false;
            for (int attempt = 0; attempt < maxAttemptsPerRoom && !placed; attempt++)
            {
                // pick whether this will be the last room
                bool willBeEnd = (placedCount == targetRooms - 1);

                // choose straight or L-turn randomly
                int turnChoice = rng.Next(0, 3); // 0=straight, 1=left, 2=right
                if (turnChoice == 0)
                {
                    // straight: choose straight length
                    int L = rng.Next(1, maxCorridorStraightLen + 1);
                    Vector2Int targetSocketGrid = prevSocketGrid + prevOutgoingDir.ToVec2Int() * (L + 1);

                    // corridor cells are prevSocketGrid + prevOutgoingDir*i for i=1..L
                    corridorCellsThisStep.Clear();
                    for (int i = 1; i <= L; i++) corridorCellsThisStep.Add(prevSocketGrid + prevOutgoingDir.ToVec2Int() * i);

                    // final corridor dir = prevOutgoingDir (no turn)
                    Direction finalDir = prevOutgoingDir;
                    Direction requiredIncomingSocketOrientation = finalDir.Opposite();

                    // try to find a room prefab that has a socket with requiredIncomingSocketOrientation
                    if (TryPlaceRoomAtSocket(targetSocketGrid, requiredIncomingSocketOrientation, willBeEnd, corridorCellsThisStep, out PlacedRoom newPlaced, out Vector2Int newSocketGrid))
                    {
                        // successful placement
                        placed = true;
                        spawned.Add(newPlaced.instance);
                        placedRooms.Add(newPlaced);

                        // mark corridor cells occupied and optionally instantiate corridor visuals now
                        foreach (var c in corridorCellsThisStep) occupied.Add(c);
                        CorridorBuilder.BuildFromGridPath(prevSocketGrid, newSocketGrid, corridorStraightPrefab, corridorCornerPrefab, cellSize, this.transform, spawned);

                        // prepare for next iteration
                        prev = newPlaced;
                        prevSocketGrid = newSocketGrid; // socket we connected on new room
                        prevOutgoingDir = RotateDirection(prev.authoring.sockets[prev.outgoingSocketIndex].orientation, prev.rotationSteps);
                    }
                }
                else
                {
                    // L-turn: choose lengths L1 (before corner) and L2 (after corner). We'll place corner as described in algorithm notes.
                    int L1 = rng.Next(0, maxCorridorTurnFirstLen + 1);
                    int L2 = rng.Next(0, maxCorridorTurnSecondLen + 1);
                    Direction turnDir = (turnChoice == 1) ? prevOutgoingDir.RotateCounterClockwise() : prevOutgoingDir.RotateClockwise();
                    // target socket grid formula derived in design notes: prev + prevOutgoingDir*L1 + turnDir*(2+L2)
                    Vector2Int targetSocketGrid = prevSocketGrid + prevOutgoingDir.ToVec2Int() * L1 + turnDir.ToVec2Int() * (2 + L2);

                    // compute corridor cells: straight L1, corner, then L2 after corner
                    corridorCellsThisStep.Clear();
                    for (int i = 1; i <= L1; i++) corridorCellsThisStep.Add(prevSocketGrid + prevOutgoingDir.ToVec2Int() * i);
                    // corner
                    corridorCellsThisStep.Add(prevSocketGrid + prevOutgoingDir.ToVec2Int() * L1 + turnDir.ToVec2Int() * 1);
                    for (int j = 1; j <= L2; j++) corridorCellsThisStep.Add(prevSocketGrid + prevOutgoingDir.ToVec2Int() * L1 + turnDir.ToVec2Int() * (1 + j));

                    Direction finalDir = turnDir; // last corridor direction
                    Direction requiredIncomingSocketOrientation = finalDir.Opposite();

                    if (TryPlaceRoomAtSocket(targetSocketGrid, requiredIncomingSocketOrientation, willBeEnd, corridorCellsThisStep, out PlacedRoom newPlaced, out Vector2Int newSocketGrid))
                    {
                        placed = true;
                        spawned.Add(newPlaced.instance);
                        placedRooms.Add(newPlaced);

                        foreach (var c in corridorCellsThisStep) occupied.Add(c);
                        CorridorBuilder.BuildFromGridPath(prevSocketGrid, newSocketGrid, corridorStraightPrefab, corridorCornerPrefab, cellSize, this.transform, spawned);

                        prev = newPlaced;
                        prevSocketGrid = newSocketGrid;
                        prevOutgoingDir = RotateDirection(prev.authoring.sockets[prev.outgoingSocketIndex].orientation, prev.rotationSteps);
                    }
                }
            }

            if (!placed)
            {
                // failed to place the next room after many attempts - abort this generation
                Debug.LogWarning("Failed to place a room after many attempts. Aborting this run and will restart if restarts remain.");
                // cleanup spawned for this attempt
                DestroySpawnedAndClearList();
                return false;
            }
        }

        // success
        Debug.Log($"Succesfully generated path with {targetRooms} rooms.");
        return true;
    }

    private bool TryPlaceRoomAtSocket(Vector2Int targetSocketGrid, Direction requiredIncomingSocketOrientation, bool willBeEnd, List<Vector2Int> corridorCells, out PlacedRoom placedRoom, out Vector2Int placedSocketGrid)
    {
        placedRoom = null;
        placedSocketGrid = Vector2Int.zero;

        // quick fail: if any corridor cell collides with occupied space => fail
        foreach (var c in corridorCells) if (occupied.Contains(c)) return false;

        // try randomly selected room prefabs many times
        int prefabCount = roomPrefabs.Count;
        int startIndex = rng.Next(0, prefabCount);
        for (int prefOffset = 0; prefOffset < prefabCount; prefOffset++)
        {
            int idx = (startIndex + prefOffset) % prefabCount;
            GameObject pref = roomPrefabs[idx];
            RoomAuthoring auth = pref.GetComponent<RoomAuthoring>();
            if (auth == null) continue;
            // if willBeEnd, we only require the room to have a socket matching incoming. If not end, require at least 2 sockets
            if (!willBeEnd && auth.sockets.Count < 2) continue;
            if (auth.sockets.Count == 0) continue;

            // shuffle sockets order
            int[] order = Permutation(auth.sockets.Count);
            foreach (int sIndex in order)
            {
                var sock = auth.sockets[sIndex];
                // we need to rotate room so that sock's orientation becomes requiredIncomingSocketOrientation
                int rotSteps = ((int)requiredIncomingSocketOrientation - (int)sock.orientation + 4) % 4;

                // compute rotated local socket grid pos
                Vector2Int rotatedSocketLocal = RotateLocalGridPos(sock.localGridPos, auth.size, rotSteps);
                // compute rotated room footprint size
                Vector2Int rotatedSize = RotatedSize(auth.size, rotSteps);

                // determine origin grid so that rotatedSocketLocal maps to targetSocketGrid
                Vector2Int originGrid = targetSocketGrid - rotatedSocketLocal;

                // ensure the full room area is free
                if (!IsAreaFreeForRoom(originGrid, rotatedSize)) continue;

                // choose outgoing socket for non-end rooms: pick another socket index (not sIndex) randomly
                int outgoingIndex = -1;
                if (!willBeEnd)
                {
                    // find sockets other than incoming
                    List<int> otherSockets = new List<int>();
                    for (int k = 0; k < auth.sockets.Count; k++) if (k != sIndex) otherSockets.Add(k);
                    if (otherSockets.Count == 0) continue;
                    // shuffle
                    ShuffleList(otherSockets);
                    // pick the first that doesn't immediately point back at the previous incoming (optional constraint)
                    outgoingIndex = otherSockets[0];
                }

                // final checks: ensure none of the room cells overlap with corridor cells (they shouldn't, but double-check)
                bool collision = false;
                for (int rx = 0; rx < rotatedSize.x && !collision; rx++)
                {
                    for (int ry = 0; ry < rotatedSize.y && !collision; ry++)
                    {
                        Vector2Int cell = originGrid + new Vector2Int(rx, ry);
                        if (occupied.Contains(cell)) collision = true;
                    }
                }
                if (collision) continue;

                // passed checks: instantiate room
                Quaternion rot = Quaternion.Euler(0, rotSteps * 90f, 0);
                Vector3 worldPos = GridToWorld(originGrid);
                GameObject inst = Instantiate(pref, worldPos, rot, this.transform);
                // store placed info
                placedRoom = new PlacedRoom
                {
                    instance = inst,
                    authoring = auth,
                    originGrid = originGrid,
                    size = rotatedSize,
                    rotationSteps = rotSteps,
                    incomingSocketIndex = sIndex,
                    outgoingSocketIndex = outgoingIndex
                };

                // mark room cells as occupied
                for (int rx = 0; rx < rotatedSize.x; rx++)
                    for (int ry = 0; ry < rotatedSize.y; ry++)
                        occupied.Add(originGrid + new Vector2Int(rx, ry));

                // output the world grid position of the socket we connected to (this will be used to chain corridors)
                placedSocketGrid = targetSocketGrid;
                return true;
            }
        }

        return false;
    }

    private int[] Permutation(int n)
    {
        int[] arr = new int[n];
        for (int i = 0; i < n; i++) arr[i] = i;
        for (int i = 0; i < n; i++)
        {
            int j = rng.Next(i, n);
            int tmp = arr[i]; arr[i] = arr[j]; arr[j] = tmp;
        }
        return arr;
    }

    private void ShuffleList<T>(List<T> list)
    {
        for (int i = 0; i < list.Count; i++)
        {
            int j = rng.Next(i, list.Count);
            var tmp = list[i]; list[i] = list[j]; list[j] = tmp;
        }
    }

    private Vector2Int GetWorldGridForSocket(PlacedRoom placed)
    {
        // incomingSocketIndex -1 for start means use outgoing instead
        int sockIndex = (placed.incomingSocketIndex >= 0) ? placed.incomingSocketIndex : placed.outgoingSocketIndex;
        if (sockIndex < 0)
        {
            Debug.LogError("Placed room has no socket to compute world grid for.");
            return placed.originGrid;
        }
        RoomSocket sock = placed.authoring.sockets[sockIndex];
        Vector2Int rotatedLocal = RotateLocalGridPos(sock.localGridPos, placed.authoring.size, placed.rotationSteps);
        return placed.originGrid + rotatedLocal;
    }

    private bool IsAreaFreeForRoom(Vector2Int originGrid, Vector2Int roomSize)
    {
        for (int x = 0; x < roomSize.x; x++)
            for (int y = 0; y < roomSize.y; y++)
                if (occupied.Contains(originGrid + new Vector2Int(x, y))) return false;
        return true;
    }

    private void MarkAreaOccupied(Vector2Int originGrid, Vector2Int roomSize)
    {
        for (int x = 0; x < roomSize.x; x++)
            for (int y = 0; y < roomSize.y; y++)
                occupied.Add(originGrid + new Vector2Int(x, y));
    }

    private static Vector2Int RotatedSize(Vector2Int size, int rotSteps)
    {
        if ((rotSteps & 1) == 1) return new Vector2Int(size.y, size.x);
        return size;
    }

    private static Vector2Int RotateLocalGridPos(Vector2Int pos, Vector2Int size, int rotSteps)
    {
        // rotation around bottom-left origin
        // 0: (x,y)
        // 1 (90 CW): (y, size.x - 1 - x)
        // 2 (180): (size.x - 1 - x, size.y - 1 - y)
        // 3 (270 CW / 90 CCW): (size.y - 1 - y, x)
        switch (rotSteps & 3)
        {
            default:
            case 0: return new Vector2Int(pos.x, pos.y);
            case 1: return new Vector2Int(pos.y, size.x - 1 - pos.x);
            case 2: return new Vector2Int(size.x - 1 - pos.x, size.y - 1 - pos.y);
            case 3: return new Vector2Int(size.y - 1 - pos.y, pos.x);
        }
    }

    private static Direction RotateDirection(Direction local, int rotSteps)
    {
        return (Direction)(((int)local + rotSteps) & 3);
    }

    private Vector3 GridToWorld(Vector2Int grid)
    {
        return new Vector3(grid.x * cellSize, 0f, grid.y * cellSize) + this.transform.position;
    }

    private void DestroySpawnedAndClearList()
    {
        foreach (var go in spawned) if (go != null) DestroyImmediate(go);
        spawned.Clear();
    }

    private void ClearGenerated()
    {
        // clears only objects parented under generator
        List<GameObject> children = new List<GameObject>();
        for (int i = 0; i < transform.childCount; i++) children.Add(transform.GetChild(i).gameObject);
        foreach (var c in children) DestroyImmediate(c);
        spawned.Clear();
        occupied.Clear();
    }

    private void OnDrawGizmosSelected()
    {
        // show some basic info in editor when selected
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, 0.2f);
    }

    private void Awake()
    {
        if (generateOnStart) GeneratePath();
    }
}
#endregion

#region CorridorBuilder
public static class CorridorBuilder
{
    // Build corridor between two socket grid points (inclusive of connection cells along the path). We reconstruct the grid path using Manhattan steps.
    public static void BuildFromGridPath(Vector2Int startSocketGrid, Vector2Int endSocketGrid, GameObject straightPrefab, GameObject cornerPrefab, float cellSize, Transform parent, List<GameObject> spawned)
    {
        // compute Manhattan path with at most one turn (this generator only builds straight or a single L-turn)
        List<Vector2Int> fullPath = ReconstructSimplePath(startSocketGrid, endSocketGrid);
        if (fullPath == null || fullPath.Count == 0) return;

        // instantiate tiles for each path cell except do not place tiles on the socket cells themselves (these are inside rooms)
        // We will place tiles at positions fullPath[1..n-1] if start and end are included in fullPath
        for (int i = 1; i < fullPath.Count - 1; i++)
        {
            Vector2Int cell = fullPath[i];
            Vector2Int prev = fullPath[i - 1];
            Vector2Int next = fullPath[i + 1];
            Vector2Int inDir = cell - prev;
            Vector2Int outDir = next - cell;

            Quaternion rot = Quaternion.identity;
            GameObject prefab = straightPrefab;

            if (inDir == outDir)
            {
                // straight
                Vector2Int dir = inDir;
                float angle = DirVecToAngle(dir);
                rot = Quaternion.Euler(0, angle, 0);
                prefab = straightPrefab;
            }
            else
            {
                // corner
                // determine which corner rotation produces the correct shape
                // we choose rotation so corner's open sides face prev and next
                float angle = CornerRotationFor(prev, cell, next);
                rot = Quaternion.Euler(0, angle, 0);
                prefab = cornerPrefab;
            }

            Vector3 worldPos = new Vector3(cell.x * cellSize, 0f, cell.y * cellSize) + parent.position;
            GameObject inst = GameObject.Instantiate(prefab, worldPos, rot, parent);
            spawned.Add(inst);
        }
    }

    private static List<Vector2Int> ReconstructSimplePath(Vector2Int a, Vector2Int b)
    {
        // returns a list of grid positions from a to b inclusive. Works if path is either straight or L-shaped (one 90deg turn).
        if (a == b) return new List<Vector2Int> { a };
        if (a.x == b.x)
        {
            // vertical straight
            List<Vector2Int> path = new List<Vector2Int>();
            int step = b.y > a.y ? 1 : -1;
            for (int y = a.y; y != b.y + step; y += step) path.Add(new Vector2Int(a.x, y));
            return path;
        }
        if (a.y == b.y)
        {
            // horizontal straight
            List<Vector2Int> path = new List<Vector2Int>();
            int step = b.x > a.x ? 1 : -1;
            for (int x = a.x; x != b.x + step; x += step) path.Add(new Vector2Int(x, a.y));
            return path;
        }

        // L-shaped: we must produce a path with one corner. Choose corner cell as (a.x, b.y) or (b.x, a.y) depending on whichever is unobstructed by design (both are fine for our generator)
        // We will use corner = (a.x, b.y)
        Vector2Int corner1 = new Vector2Int(a.x, b.y);
        List<Vector2Int> path1 = new List<Vector2Int>();
        int stepX = corner1.x > a.x ? 1 : (corner1.x < a.x ? -1 : 0);
        for (int x = a.x; x != corner1.x + stepX; x += stepX) path1.Add(new Vector2Int(x, a.y));
        int stepY = b.y > corner1.y ? 1 : (b.y < corner1.y ? -1 : 0);
        for (int y = a.y + (stepX == 0 ? 1 : 0); y != b.y + stepY; y += stepY) path1.Add(new Vector2Int(corner1.x, y));
        // ensure final point is b
        if (path1[path1.Count - 1] != b) path1.Add(b);
        return path1;
    }

    private static float DirVecToAngle(Vector2Int dir)
    {
        if (dir == Vector2Int.up) return 0f;
        if (dir == Vector2Int.right) return 90f;
        if (dir == Vector2Int.down) return 180f;
        if (dir == Vector2Int.left) return 270f;
        return 0f;
    }

    private static float CornerRotationFor(Vector2Int prev, Vector2Int cell, Vector2Int next)
    {
        // figure rotation so that corner prefab connects prev->cell->next
        Vector2Int inDir = cell - prev;
        Vector2Int outDir = next - cell;
        // pick angle such that the corner's open arms match inDir and outDir; brute force test 0,90,180,270
        Vector2Int[] candidates = new Vector2Int[] { Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left };
        for (int i = 0; i < 4; i++)
        {
            Vector2Int a = candidates[i];
            Vector2Int b = candidates[(i + 1) & 3]; // corner connects a->b in a prefab local orientation
            if ((a == inDir && b == outDir) || (a == outDir && b == inDir))
                return i * 90f;
        }
        // fallback
        return 0f;
    }
}
#endregion
