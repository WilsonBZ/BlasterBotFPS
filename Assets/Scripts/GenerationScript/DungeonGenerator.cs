//using System;
//using System.Collections.Generic;
//using UnityEngine;

//#region DirectionHelpers
//public enum Direction { North = 0, East = 1, South = 2, West = 3 }

//public static class DirectionExtensions
//{
//    public static Vector2Int ToVec2Int(this Direction d)
//    {
//        switch (d)
//        {
//            case Direction.North: return new Vector2Int(0, 1);
//            case Direction.East: return new Vector2Int(1, 0);
//            case Direction.South: return new Vector2Int(0, -1);
//            case Direction.West: return new Vector2Int(-1, 0);
//            default: return Vector2Int.zero;
//        }
//    }

//    public static Direction RotateClockwise(this Direction d) => (Direction)(((int)d + 1) & 3);
//    public static Direction RotateCounterClockwise(this Direction d) => (Direction)(((int)d + 3) & 3);
//    public static Direction Opposite(this Direction d) => (Direction)(((int)d + 2) & 3);

//    // convert direction to a Y-axis rotation (Unity degrees)
//    public static float ToYAngle(this Direction d)
//    {
//        switch (d)
//        {
//            case Direction.North: return 0f;
//            case Direction.East: return 90f;
//            case Direction.South: return 180f;
//            case Direction.West: return 270f;
//            default: return 0f;
//        }
//    }
//}
//#endregion

//#region RuntimeHelpers
//internal class PlacedRoom
//{
//    public GameObject instance;
//    public Vector2Int originGrid; // bottom-left grid cell where this room is placed
//    public Vector2Int size; // rotated size
//    public int rotationSteps; // number of 90deg clockwise rotations applied (0..3)
//    public RoomAuthoring authoring;
//    public int incomingSocketIndex; // index into authoring.sockets used to connect from previous room (or -1 for start)
//    public int outgoingSocketIndex; // index into authoring.sockets chosen for the next connection or -1 for end
//}

//#endregion

//#region DungeonGenerator
//[DisallowMultipleComponent]
//public class DungeonGenerator : MonoBehaviour
//{
//    [Header("Prefabs")]
//    [Tooltip("Room prefabs: prefab root must contain a RoomAuthoring component describing size + sockets.")]
//    public List<GameObject> roomPrefabs = new List<GameObject>();

//    [Tooltip("1x1 straight corridor tile prefab. Should be created so its local forward (z) aligns with grid +Y (North)")]
//    public GameObject corridorStraightPrefab;

//    [Tooltip("1x1 corner corridor tile prefab. Should have geometry that fits one grid cell and turns a 90deg corner.")]
//    public GameObject corridorCornerPrefab;

//    [Header("Generation Settings")]
//    public int minRooms = 4;
//    public int maxRooms = 8;
//    public int seed = 0;
//    public float cellSize = 4f; // world units per grid cell
//    public int maxCorridorStraightLen = 3; // maximum straight segment length
//    public int maxCorridorTurnFirstLen = 2; // L-turn: number of straight cells before corner
//    public int maxCorridorTurnSecondLen = 2; // L-turn: number of straight cells after corner

//    [Tooltip("How many attempts to try placing a single next room before giving up and restarting the whole generation.")]
//    public int maxAttemptsPerRoom = 200;

//    [Tooltip("How many times to restart generation if stuck (increase if you frequently fail)")]
//    public int maxRestarts = 5;

//    [Tooltip("If true, generator runs at Start(). Otherwise call GeneratePath() manually.")]
//    public bool generateOnStart = true;

//    // internals
//    private System.Random rng;
//    private HashSet<Vector2Int> occupied = new HashSet<Vector2Int>();
//    private List<GameObject> spawned = new List<GameObject>();

//    [ContextMenu("GeneratePath")]
//    public void GeneratePath()
//    {
//        ClearGenerated();
//        rng = new System.Random(seed == 0 ? Environment.TickCount : seed);

//        if (roomPrefabs == null || roomPrefabs.Count == 0)
//        {
//            Debug.LogError("No room prefabs assigned to DungeonGenerator.");
//            return;
//        }
//        if (corridorStraightPrefab == null || corridorCornerPrefab == null)
//        {
//            Debug.LogError("Assign corridorStraightPrefab and corridorCornerPrefab.");
//            return;
//        }

//        bool success = false;
//        for (int restart = 0; restart <= maxRestarts && !success; restart++)
//        {
//            occupied.Clear();
//            DestroySpawnedAndClearList();
//            success = TryGenerateOnce();
//            if (!success) Debug.LogWarning($"Generation attempt {restart + 1} failed; restarting...");
//        }

//        if (!success)
//        {
//            Debug.LogError("Dungeon generation failed after multiple restarts. Try more restarts or increase available room prefabs or grid space.");
//        }
//    }

//    private bool TryGenerateOnce()
//    {
//        int targetRooms = rng.Next(minRooms, maxRooms + 1);
//        // place start room at origin (0,0) on grid
//        // pick random room that can be a start (must have at least 1 socket)
//        int startPrefabIndex = rng.Next(roomPrefabs.Count);
//        GameObject startPrefab = roomPrefabs[startPrefabIndex];
//        RoomAuthoring startAuth = startPrefab.GetComponent<RoomAuthoring>();
//        if (startAuth == null || startAuth.sockets.Count == 0)
//        {
//            Debug.LogError($"Room prefab {startPrefab.name} missing RoomAuthoring or sockets.");
//            return false;
//        }

//        // instantiate start with a random outgoing socket and a random rotation
//        int chosenStartSocketIndex = rng.Next(startAuth.sockets.Count);
//        // choose rotation such that socket points to a random direction (we can choose any rot since outgoing will define d_prev)
//        int startRotation = rng.Next(0, 4);
//        Direction startOutgoingWorldDir = RotateDirection(startAuth.sockets[chosenStartSocketIndex].orientation, startRotation);

//        // compute rotated size and ensure start origin at (0,0)
//        Vector2Int startSizeRot = RotatedSize(startAuth.size, startRotation);
//        Vector2Int startOriginGrid = Vector2Int.zero; // by convention place start at origin

//        // check occupancy for start room
//        if (!IsAreaFreeForRoom(startOriginGrid, startSizeRot))
//        {
//            Debug.LogError("Start room cannot be placed at origin. Please ensure your room prefabs are authored with the root at the bottom-left and not overlapping origin.");
//            return false;
//        }

//        PlacedRoom prev = new PlacedRoom();
//        prev.authoring = startAuth;
//        prev.originGrid = startOriginGrid;
//        prev.size = startSizeRot;
//        prev.rotationSteps = startRotation;
//        prev.incomingSocketIndex = -1; // no incoming socket for start
//        prev.outgoingSocketIndex = chosenStartSocketIndex;

//        // instantiate start in world
//        GameObject startInst = Instantiate(startPrefab, GridToWorld(startOriginGrid), Quaternion.Euler(0, startRotation * 90f, 0), this.transform);
//        spawned.Add(startInst);
//        prev.instance = startInst;
//        MarkAreaOccupied(prev.originGrid, prev.size);

//        // compute prev socket world grid position (the socket's global grid cell)
//        Vector2Int prevSocketGrid = GetWorldGridForSocket(prev);
//        Direction prevOutgoingDir = startOutgoingWorldDir;

//        List<Vector2Int> corridorCellsThisStep = new List<Vector2Int>();

//        // place subsequent rooms
//        List<PlacedRoom> placedRooms = new List<PlacedRoom>() { prev };

//        for (int placedCount = 1; placedCount < targetRooms; placedCount++)
//        {
//            bool placed = false;
//            for (int attempt = 0; attempt < maxAttemptsPerRoom && !placed; attempt++)
//            {
//                // pick whether this will be the last room
//                bool willBeEnd = (placedCount == targetRooms - 1);

//                // choose straight or L-turn randomly
//                int turnChoice = rng.Next(0, 3); // 0=straight, 1=left, 2=right
//                if (turnChoice == 0)
//                {
//                    // straight: choose straight length
//                    int L = rng.Next(1, maxCorridorStraightLen + 1);
//                    Vector2Int targetSocketGrid = prevSocketGrid + prevOutgoingDir.ToVec2Int() * (L + 1);

//                    // corridor cells are prevSocketGrid + prevOutgoingDir*i for i=1..L
//                    corridorCellsThisStep.Clear();
//                    for (int i = 1; i <= L; i++) corridorCellsThisStep.Add(prevSocketGrid + prevOutgoingDir.ToVec2Int() * i);

//                    // final corridor dir = prevOutgoingDir (no turn)
//                    Direction finalDir = prevOutgoingDir;
//                    Direction requiredIncomingSocketOrientation = finalDir.Opposite();

//                    // try to find a room prefab that has a socket with requiredIncomingSocketOrientation
//                    if (TryPlaceRoomAtSocket(targetSocketGrid, requiredIncomingSocketOrientation, willBeEnd, corridorCellsThisStep, out PlacedRoom newPlaced, out Vector2Int newSocketGrid))
//                    {
//                        // successful placement
//                        placed = true;
//                        spawned.Add(newPlaced.instance);
//                        placedRooms.Add(newPlaced);

//                        // mark corridor cells occupied and optionally instantiate corridor visuals now
//                        foreach (var c in corridorCellsThisStep) occupied.Add(c);
//                        CorridorBuilder.BuildFromGridPath(prevSocketGrid, newSocketGrid, corridorStraightPrefab, corridorCornerPrefab, cellSize, this.transform, spawned);

//                        // prepare for next iteration
//                        prev = newPlaced;
//                        prevSocketGrid = newSocketGrid; // socket we connected on new room
//                        prevOutgoingDir = RotateDirection(prev.authoring.sockets[prev.outgoingSocketIndex].orientation, prev.rotationSteps);
//                    }
//                }
//                else
//                {
//                    // L-turn: choose lengths L1 (before corner) and L2 (after corner). We'll place corner as described in algorithm notes.
//                    int L1 = rng.Next(0, maxCorridorTurnFirstLen + 1);
//                    int L2 = rng.Next(0, maxCorridorTurnSecondLen + 1);
//                    Direction turnDir = (turnChoice == 1) ? prevOutgoingDir.RotateCounterClockwise() : prevOutgoingDir.RotateClockwise();
//                    // target socket grid formula derived in design notes: prev + prevOutgoingDir*L1 + turnDir*(2+L2)
//                    Vector2Int targetSocketGrid = prevSocketGrid + prevOutgoingDir.ToVec2Int() * L1 + turnDir.ToVec2Int() * (2 + L2);

//                    // compute corridor cells: straight L1, corner, then L2 after corner
//                    corridorCellsThisStep.Clear();
//                    for (int i = 1; i <= L1; i++) corridorCellsThisStep.Add(prevSocketGrid + prevOutgoingDir.ToVec2Int() * i);
//                    // corner
//                    corridorCellsThisStep.Add(prevSocketGrid + prevOutgoingDir.ToVec2Int() * L1 + turnDir.ToVec2Int() * 1);
//                    for (int j = 1; j <= L2; j++) corridorCellsThisStep.Add(prevSocketGrid + prevOutgoingDir.ToVec2Int() * L1 + turnDir.ToVec2Int() * (1 + j));

//                    Direction finalDir = turnDir; // last corridor direction
//                    Direction requiredIncomingSocketOrientation = finalDir.Opposite();

//                    if (TryPlaceRoomAtSocket(targetSocketGrid, requiredIncomingSocketOrientation, willBeEnd, corridorCellsThisStep, out PlacedRoom newPlaced, out Vector2Int newSocketGrid))
//                    {
//                        placed = true;
//                        spawned.Add(newPlaced.instance);
//                        placedRooms.Add(newPlaced);

//                        foreach (var c in corridorCellsThisStep) occupied.Add(c);
//                        CorridorBuilder.BuildFromGridPath(prevSocketGrid, newSocketGrid, corridorStraightPrefab, corridorCornerPrefab, cellSize, this.transform, spawned);

//                        prev = newPlaced;
//                        prevSocketGrid = newSocketGrid;
//                        prevOutgoingDir = RotateDirection(prev.authoring.sockets[prev.outgoingSocketIndex].orientation, prev.rotationSteps);
//                    }
//                }
//            }

//            if (!placed)
//            {
//                // failed to place the next room after many attempts - abort this generation
//                Debug.LogWarning("Failed to place a room after many attempts. Aborting this run and will restart if restarts remain.");
//                // cleanup spawned for this attempt
//                DestroySpawnedAndClearList();
//                return false;
//            }
//        }

//        // success
//        Debug.Log($"Succesfully generated path with {targetRooms} rooms.");
//        return true;
//    }

//    private bool TryPlaceRoomAtSocket(Vector2Int targetSocketGrid, Direction requiredIncomingSocketOrientation, bool willBeEnd, List<Vector2Int> corridorCells, out PlacedRoom placedRoom, out Vector2Int placedSocketGrid)
//    {
//        placedRoom = null;
//        placedSocketGrid = Vector2Int.zero;

//        // quick fail: if any corridor cell collides with occupied space => fail
//        foreach (var c in corridorCells) if (occupied.Contains(c)) return false;

//        // try randomly selected room prefabs many times
//        int prefabCount = roomPrefabs.Count;
//        int startIndex = rng.Next(0, prefabCount);
//        for (int prefOffset = 0; prefOffset < prefabCount; prefOffset++)
//        {
//            int idx = (startIndex + prefOffset) % prefabCount;
//            GameObject pref = roomPrefabs[idx];
//            RoomAuthoring auth = pref.GetComponent<RoomAuthoring>();
//            if (auth == null) continue;
//            // if willBeEnd, we only require the room to have a socket matching incoming. If not end, require at least 2 sockets
//            if (!willBeEnd && auth.sockets.Count < 2) continue;
//            if (auth.sockets.Count == 0) continue;

//            // shuffle sockets order
//            int[] order = Permutation(auth.sockets.Count);
//            foreach (int sIndex in order)
//            {
//                var sock = auth.sockets[sIndex];
//                // we need to rotate room so that sock's orientation becomes requiredIncomingSocketOrientation
//                int rotSteps = ((int)requiredIncomingSocketOrientation - (int)sock.orientation + 4) % 4;

//                // compute rotated local socket grid pos
//                Vector2Int rotatedSocketLocal = RotateLocalGridPos(sock.localGridPos, auth.size, rotSteps);
//                // compute rotated room footprint size
//                Vector2Int rotatedSize = RotatedSize(auth.size, rotSteps);

//                // determine origin grid so that rotatedSocketLocal maps to targetSocketGrid
//                Vector2Int originGrid = targetSocketGrid - rotatedSocketLocal;

//                // ensure the full room area is free
//                if (!IsAreaFreeForRoom(originGrid, rotatedSize)) continue;

//                // choose outgoing socket for non-end rooms: pick another socket index (not sIndex) randomly
//                int outgoingIndex = -1;
//                if (!willBeEnd)
//                {
//                    // find sockets other than incoming
//                    List<int> otherSockets = new List<int>();
//                    for (int k = 0; k < auth.sockets.Count; k++) if (k != sIndex) otherSockets.Add(k);
//                    if (otherSockets.Count == 0) continue;
//                    // shuffle
//                    ShuffleList(otherSockets);
//                    // pick the first that doesn't immediately point back at the previous incoming (optional constraint)
//                    outgoingIndex = otherSockets[0];
//                }

//                // final checks: ensure none of the room cells overlap with corridor cells (they shouldn't, but double-check)
//                bool collision = false;
//                for (int rx = 0; rx < rotatedSize.x && !collision; rx++)
//                {
//                    for (int ry = 0; ry < rotatedSize.y && !collision; ry++)
//                    {
//                        Vector2Int cell = originGrid + new Vector2Int(rx, ry);
//                        if (occupied.Contains(cell)) collision = true;
//                    }
//                }
//                if (collision) continue;

//                // passed checks: instantiate room
//                Quaternion rot = Quaternion.Euler(0, rotSteps * 90f, 0);
//                Vector3 worldPos = GridToWorld(originGrid);
//                GameObject inst = Instantiate(pref, worldPos, rot, this.transform);
//                // store placed info
//                placedRoom = new PlacedRoom
//                {
//                    instance = inst,
//                    authoring = auth,
//                    originGrid = originGrid,
//                    size = rotatedSize,
//                    rotationSteps = rotSteps,
//                    incomingSocketIndex = sIndex,
//                    outgoingSocketIndex = outgoingIndex
//                };

//                // mark room cells as occupied
//                for (int rx = 0; rx < rotatedSize.x; rx++)
//                    for (int ry = 0; ry < rotatedSize.y; ry++)
//                        occupied.Add(originGrid + new Vector2Int(rx, ry));

//                // output the world grid position of the socket we connected to (this will be used to chain corridors)
//                placedSocketGrid = targetSocketGrid;
//                return true;
//            }
//        }

//        return false;
//    }

//    private int[] Permutation(int n)
//    {
//        int[] arr = new int[n];
//        for (int i = 0; i < n; i++) arr[i] = i;
//        for (int i = 0; i < n; i++)
//        {
//            int j = rng.Next(i, n);
//            int tmp = arr[i]; arr[i] = arr[j]; arr[j] = tmp;
//        }
//        return arr;
//    }

//    private void ShuffleList<T>(List<T> list)
//    {
//        for (int i = 0; i < list.Count; i++)
//        {
//            int j = rng.Next(i, list.Count);
//            var tmp = list[i]; list[i] = list[j]; list[j] = tmp;
//        }
//    }

//    private Vector2Int GetWorldGridForSocket(PlacedRoom placed)
//    {
//        // incomingSocketIndex -1 for start means use outgoing instead
//        int sockIndex = (placed.incomingSocketIndex >= 0) ? placed.incomingSocketIndex : placed.outgoingSocketIndex;
//        if (sockIndex < 0)
//        {
//            Debug.LogError("Placed room has no socket to compute world grid for.");
//            return placed.originGrid;
//        }
//        RoomSocket sock = placed.authoring.sockets[sockIndex];
//        Vector2Int rotatedLocal = RotateLocalGridPos(sock.localGridPos, placed.authoring.size, placed.rotationSteps);
//        return placed.originGrid + rotatedLocal;
//    }

//    private bool IsAreaFreeForRoom(Vector2Int originGrid, Vector2Int roomSize)
//    {
//        for (int x = 0; x < roomSize.x; x++)
//            for (int y = 0; y < roomSize.y; y++)
//                if (occupied.Contains(originGrid + new Vector2Int(x, y))) return false;
//        return true;
//    }

//    private void MarkAreaOccupied(Vector2Int originGrid, Vector2Int roomSize)
//    {
//        for (int x = 0; x < roomSize.x; x++)
//            for (int y = 0; y < roomSize.y; y++)
//                occupied.Add(originGrid + new Vector2Int(x, y));
//    }

//    private static Vector2Int RotatedSize(Vector2Int size, int rotSteps)
//    {
//        if ((rotSteps & 1) == 1) return new Vector2Int(size.y, size.x);
//        return size;
//    }

//    private static Vector2Int RotateLocalGridPos(Vector2Int pos, Vector2Int size, int rotSteps)
//    {
//        // rotation around bottom-left origin
//        // 0: (x,y)
//        // 1 (90 CW): (y, size.x - 1 - x)
//        // 2 (180): (size.x - 1 - x, size.y - 1 - y)
//        // 3 (270 CW / 90 CCW): (size.y - 1 - y, x)
//        switch (rotSteps & 3)
//        {
//            default:
//            case 0: return new Vector2Int(pos.x, pos.y);
//            case 1: return new Vector2Int(pos.y, size.x - 1 - pos.x);
//            case 2: return new Vector2Int(size.x - 1 - pos.x, size.y - 1 - pos.y);
//            case 3: return new Vector2Int(size.y - 1 - pos.y, pos.x);
//        }
//    }

//    private static Direction RotateDirection(Direction local, int rotSteps)
//    {
//        return (Direction)(((int)local + rotSteps) & 3);
//    }

//    private Vector3 GridToWorld(Vector2Int grid)
//    {
//        return new Vector3(grid.x * cellSize, 0f, grid.y * cellSize) + this.transform.position;
//    }

//    private void DestroySpawnedAndClearList()
//    {
//        foreach (var go in spawned) if (go != null) DestroyImmediate(go);
//        spawned.Clear();
//    }

//    private void ClearGenerated()
//    {
//        // clears only objects parented under generator
//        List<GameObject> children = new List<GameObject>();
//        for (int i = 0; i < transform.childCount; i++) children.Add(transform.GetChild(i).gameObject);
//        foreach (var c in children) DestroyImmediate(c);
//        spawned.Clear();
//        occupied.Clear();
//    }

//    private void OnDrawGizmosSelected()
//    {
//        // show some basic info in editor when selected
//        Gizmos.color = Color.yellow;
//        Gizmos.DrawWireSphere(transform.position, 0.2f);
//    }

//    private void Awake()
//    {
//        if (generateOnStart) GeneratePath();
//    }
//}
//#endregion

//#region CorridorBuilder
//public static class CorridorBuilder
//{
//    public static void BuildFromGridPath(Vector2Int startSocketGrid, Vector2Int endSocketGrid,
//        GameObject straightPrefab, GameObject cornerPrefab, float cellSize, Transform parent,
//        List<GameObject> spawned)
//    {
//        // Safety check - if start and end are the same, skip corridor building
//        if (startSocketGrid == endSocketGrid)
//        {
//            Debug.LogWarning($"Start and end socket positions are the same: {startSocketGrid}");
//            return;
//        }

//        // Compute Manhattan path with at most one turn
//        List<Vector2Int> fullPath = ReconstructSimplePath(startSocketGrid, endSocketGrid);
//        if (fullPath == null || fullPath.Count < 2)
//        {
//            Debug.LogError($"Failed to reconstruct path from {startSocketGrid} to {endSocketGrid}");
//            return;
//        }

//        // Instantiate tiles for each path cell except the socket cells themselves
//        for (int i = 1; i < fullPath.Count - 1; i++)
//        {
//            Vector2Int cell = fullPath[i];
//            Vector2Int prev = fullPath[i - 1];
//            Vector2Int next = (i < fullPath.Count - 1) ? fullPath[i + 1] : cell;

//            // Skip if we're at the start or end of path
//            if (cell == startSocketGrid || cell == endSocketGrid)
//                continue;

//            Quaternion rot = Quaternion.identity;
//            GameObject prefab = straightPrefab;

//            Vector2Int inDir = cell - prev;
//            Vector2Int outDir = next - cell;

//            // Normalize directions to unit vectors
//            inDir = NormalizeDirection(inDir);
//            outDir = NormalizeDirection(outDir);

//            if (inDir == outDir)
//            {
//                // Straight corridor
//                float angle = DirVecToAngle(inDir);
//                rot = Quaternion.Euler(0, angle, 0);
//                prefab = straightPrefab;

//                Vector3 worldPos = new Vector3(cell.x * cellSize, 0f, cell.y * cellSize) + parent.position;
//                GameObject inst = GameObject.Instantiate(prefab, worldPos, rot, parent);
//                if (inst != null) spawned.Add(inst);
//            }
//            else if (AreDirectionsPerpendicular(inDir, outDir)) // Perpendicular directions = corner
//            {
//                // Corner piece
//                float angle = CalculateCornerRotation(inDir, outDir);
//                rot = Quaternion.Euler(0, angle, 0);
//                prefab = cornerPrefab;

//                Vector3 worldPos = new Vector3(cell.x * cellSize, 0f, cell.y * cellSize) + parent.position;
//                GameObject inst = GameObject.Instantiate(prefab, worldPos, rot, parent);
//                if (inst != null) spawned.Add(inst);
//            }
//            else
//            {
//                Debug.LogWarning($"Invalid direction change at cell {cell}: {inDir} -> {outDir}");
//            }
//        }
//    }

//    private static bool AreDirectionsPerpendicular(Vector2Int inDir, Vector2Int outDir)
//    {
//        // Manual dot product calculation for Vector2Int
//        return (inDir.x * outDir.x + inDir.y * outDir.y) == 0;
//    }

//    private static Vector2Int NormalizeDirection(Vector2Int dir)
//    {
//        if (dir.x != 0) return new Vector2Int(MathHelper.Sign(dir.x), 0);
//        if (dir.y != 0) return new Vector2Int(0, MathHelper.Sign(dir.y));
//        return Vector2Int.zero;
//    }

//    private static List<Vector2Int> ReconstructSimplePath(Vector2Int a, Vector2Int b)
//    {
//        List<Vector2Int> path = new List<Vector2Int>();

//        // Add start point
//        path.Add(a);

//        // Handle straight paths (same x or same y)
//        if (a.x == b.x) // Vertical path
//        {
//            int step = (b.y > a.y) ? 1 : -1;
//            for (int y = a.y + step; y != b.y; y += step)
//            {
//                path.Add(new Vector2Int(a.x, y));
//            }
//        }
//        else if (a.y == b.y) // Horizontal path
//        {
//            int step = (b.x > a.x) ? 1 : -1;
//            for (int x = a.x + step; x != b.x; x += step)
//            {
//                path.Add(new Vector2Int(x, a.y));
//            }
//        }
//        else // L-shaped path (one turn)
//        {
//            // Choose corner point - use (a.x, b.y) as the corner
//            Vector2Int corner = new Vector2Int(a.x, b.y);

//            // First segment: horizontal from a to corner
//            int stepX = (corner.x > a.x) ? 1 : -1;
//            for (int x = a.x + stepX; x != corner.x; x += stepX)
//            {
//                path.Add(new Vector2Int(x, a.y));
//            }

//            // Add corner point
//            path.Add(corner);

//            // Second segment: vertical from corner to b
//            int stepY = (b.y > corner.y) ? 1 : -1;
//            for (int y = corner.y + stepY; y != b.y; y += stepY)
//            {
//                path.Add(new Vector2Int(corner.x, y));
//            }
//        }

//        // Add end point
//        path.Add(b);

//        return path;
//    }

//    private static float DirVecToAngle(Vector2Int dir)
//    {
//        if (dir == Vector2Int.up) return 0f;      // North
//        if (dir == Vector2Int.right) return 90f;  // East  
//        if (dir == Vector2Int.down) return 180f;  // South
//        if (dir == Vector2Int.left) return 270f;  // West
//        return 0f;
//    }

//    private static float CalculateCornerRotation(Vector2Int inDir, Vector2Int outDir)
//    {
//        // Map corner rotations based on incoming and outgoing directions
//        if (inDir == Vector2Int.up && outDir == Vector2Int.right) return 0f;    // N → E
//        if (inDir == Vector2Int.up && outDir == Vector2Int.left) return 270f;   // N → W
//        if (inDir == Vector2Int.right && outDir == Vector2Int.up) return 270f;  // E → N
//        if (inDir == Vector2Int.right && outDir == Vector2Int.down) return 0f;  // E → S
//        if (inDir == Vector2Int.down && outDir == Vector2Int.right) return 90f; // S → E  
//        if (inDir == Vector2Int.down && outDir == Vector2Int.left) return 0f;   // S → W
//        if (inDir == Vector2Int.left && outDir == Vector2Int.up) return 0f;     // W → N
//        if (inDir == Vector2Int.left && outDir == Vector2Int.down) return 90f;  // W → S

//        Debug.LogWarning($"Unexpected corner directions: {inDir} -> {outDir}");
//        return 0f;
//    }
//}
//#endregion

//// Add this helper class at the top of your file (outside any regions)
//public static class MathHelper
//{
//    public static int Sign(int value)
//    {
//        if (value > 0) return 1;
//        if (value < 0) return -1;
//        return 0;
//    }
//}