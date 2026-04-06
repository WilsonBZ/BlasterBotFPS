using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Singleton that manages additive scene loading for the room-based level flow.
/// Lives in the persistent boot/core scene. Room scenes are loaded and unloaded
/// additively as the player progresses through them.
/// </summary>
[DisallowMultipleComponent]
public class AdditiveSceneManager : MonoBehaviour
{
    public static AdditiveSceneManager Instance { get; private set; }

    [Header("Room Sequence")]
    [Tooltip("Ordered list of room scene names to load (must be added to Build Settings).")]
    public List<string> roomSceneNames = new List<string>();

    [Header("Settings")]
    [Tooltip("Number of rooms to keep loaded ahead of the current room.")]
    [Range(1, 3)]
    public int preloadAhead = 1;

    /// <summary>Index of the room the player is currently in.</summary>
    public int CurrentRoomIndex { get; private set; } = -1;

    private readonly HashSet<string> loadedRoomScenes = new HashSet<string>();
    private bool isTransitioning = false;

    private bool hasLaunchedOnce = false;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        // Only auto-load on the very first launch. Subsequent game starts go
        // through NewGameFromMenu() which fully resets state before loading rooms.
        if (!hasLaunchedOnce && roomSceneNames.Count > 0)
        {
            hasLaunchedOnce = true;
            LoadRoomAt(0);
        }
    }

    /// <summary>
    /// Called by <see cref="SceneExitTrigger"/> when the player exits a cleared room.
    /// Triggers loading of the next room and unloading of the current one.
    /// </summary>
    /// <param name="clearedRoomSceneName">The scene name of the room that was just cleared.</param>
    public void OnPlayerExitedRoom(string clearedRoomSceneName)
    {
        if (isTransitioning)
        {
            return;
        }

        int clearedIndex = roomSceneNames.IndexOf(clearedRoomSceneName);
        if (clearedIndex < 0)
        {
            Debug.LogWarning($"[AdditiveSceneManager] Scene '{clearedRoomSceneName}' not found in roomSceneNames list.");
            return;
        }

        int nextIndex = clearedIndex + 1;
        if (nextIndex >= roomSceneNames.Count)
        {
            Debug.Log("[AdditiveSceneManager] All rooms completed!");
            return;
        }

        StartCoroutine(TransitionToRoom(nextIndex));
    }

    private IEnumerator TransitionToRoom(int toIndex)
    {
        isTransitioning = true;
        CurrentRoomIndex = toIndex;

        // Load the next room and any preload-ahead rooms.
        for (int i = toIndex; i <= Mathf.Min(toIndex + preloadAhead, roomSceneNames.Count - 1); i++)
        {
            yield return LoadRoomAsync(roomSceneNames[i]);
        }

        isTransitioning = false;
    }

    /// <summary>
    /// Unloads all tracked room scenes and reloads from index 0.
    /// Called by <see cref="FloorProgressManager"/> when the player advances a floor,
    /// or directly on player death / restart.
    /// </summary>
    public IEnumerator ResetToStart()
    {
        isTransitioning = true;
        CurrentRoomIndex = -1;

        // Unload every room scene that is currently tracked or Unity knows about.
        List<string> toUnload = new List<string>(loadedRoomScenes);
        loadedRoomScenes.Clear();

        foreach (string sceneName in toUnload)
        {
            Scene scene = SceneManager.GetSceneByName(sceneName);
            if (scene.isLoaded)
            {
                yield return SceneManager.UnloadSceneAsync(sceneName);
                Debug.Log($"[AdditiveSceneManager] Unloaded for reset: {sceneName}");
            }
        }

        // Also catch any room scenes that were loaded outside of this manager
        // (e.g. open in the editor when Play was pressed) that we never tracked.
        foreach (string sceneName in roomSceneNames)
        {
            Scene scene = SceneManager.GetSceneByName(sceneName);
            if (scene.isLoaded)
            {
                yield return SceneManager.UnloadSceneAsync(sceneName);
                Debug.Log($"[AdditiveSceneManager] Unloaded untracked scene for reset: {sceneName}");
            }
        }

        // Wait a frame so Unity finishes unloading before reloading.
        yield return null;

        CurrentRoomIndex = 0;

        if (roomSceneNames.Count > 0)
        {
            yield return LoadRoomAsync(roomSceneNames[0]);

            for (int i = 1; i <= Mathf.Min(preloadAhead, roomSceneNames.Count - 1); i++)
            {
                yield return LoadRoomAsync(roomSceneNames[i]);
            }
        }

        isTransitioning = false;
        Debug.Log("[AdditiveSceneManager] Reset complete. Rooms reloaded from index 0.");
    }

    /// <summary>
    /// Full restart: unloads all rooms, reloads them from scratch, then re-caches
    /// all persistent manager references. Use this instead of SceneManager.LoadScene
    /// on player death to avoid destroying DontDestroyOnLoad singletons.
    /// </summary>
    public void RestartFromDeath()
    {
        StartCoroutine(RestartFromDeathRoutine());
    }

    private IEnumerator RestartFromDeathRoutine()
    {
        yield return StartCoroutine(ResetToStart());

        // Re-cache player reference in NewBuffManager after rooms have reloaded.
        if (NewBuffManager.Instance != null)
            NewBuffManager.Instance.RecacheReferences();

        // Re-apply buff history and difficulty after a death restart.
        if (FloorProgressManager.Instance != null)
            FloorProgressManager.Instance.OnDeathRestart();

        Time.timeScale = 1f;
    }

    /// <summary>
    /// Called by <see cref="MainMenuUI"/> when starting a new game from the main menu.
    /// Loads the boot scene non-additively (replacing the main menu) then loads
    /// rooms on top, giving a clean slate identical to a first launch.
    /// </summary>
    /// <param name="bootSceneName">The base gameplay scene that hosts the player and HUD.</param>
    public void NewGameFromMenu(string bootSceneName)
    {
        hasLaunchedOnce = true;

        if (FloorProgressManager.Instance != null)
            FloorProgressManager.Instance.FullReset();

        StartCoroutine(NewGameFromMenuRoutine(bootSceneName));
    }

    private IEnumerator NewGameFromMenuRoutine(string bootSceneName)
    {
        // Unload any currently tracked room scenes so loadedRoomScenes is empty
        // before the non-additive load wipes everything else.
        List<string> toUnload = new List<string>(loadedRoomScenes);
        loadedRoomScenes.Clear();
        foreach (string s in toUnload)
        {
            Scene sc = SceneManager.GetSceneByName(s);
            if (sc.isLoaded)
                yield return SceneManager.UnloadSceneAsync(s);
        }

        isTransitioning = true;
        CurrentRoomIndex = -1;

        // Replace the main menu scene with the boot/gameplay base scene.
        // AdditiveSceneManager survives because it is DontDestroyOnLoad.
        AsyncOperation baseLoad = SceneManager.LoadSceneAsync(bootSceneName, LoadSceneMode.Single);
        while (!baseLoad.isDone)
            yield return null;

        // Wait one extra frame for Awake/Start to run on freshly spawned objects.
        yield return null;
        yield return null;

        // Re-cache references now that the new scene's objects exist.
        if (NewBuffManager.Instance != null)
            NewBuffManager.Instance.RecacheReferences();

        // Load room scenes additively on top of the clean boot scene.
        CurrentRoomIndex = 0;
        if (roomSceneNames.Count > 0)
        {
            yield return LoadRoomAsync(roomSceneNames[0]);

            for (int i = 1; i <= Mathf.Min(preloadAhead, roomSceneNames.Count - 1); i++)
                yield return LoadRoomAsync(roomSceneNames[i]);
        }

        isTransitioning = false;
        Time.timeScale = 1f;
        Debug.Log("[AdditiveSceneManager] New game started from menu.");
    }

    /// <summary>Loads a room scene by its index in <see cref="roomSceneNames"/>.</summary>
    public void LoadRoomAt(int index)
    {
        if (index < 0 || index >= roomSceneNames.Count)
        {
            return;
        }

        CurrentRoomIndex = index;
        StartCoroutine(LoadRoomAsync(roomSceneNames[index]));

        // Preload ahead.
        for (int i = index + 1; i <= Mathf.Min(index + preloadAhead, roomSceneNames.Count - 1); i++)
        {
            StartCoroutine(LoadRoomAsync(roomSceneNames[i]));
        }
    }

    private IEnumerator LoadRoomAsync(string sceneName)
    {
        if (loadedRoomScenes.Contains(sceneName))
        {
            yield break;
        }

        // Also check if Unity already has this scene loaded (e.g. open in the editor when Play is pressed).
        Scene existingScene = SceneManager.GetSceneByName(sceneName);
        if (existingScene.isLoaded)
        {
            loadedRoomScenes.Add(sceneName);
            Debug.Log($"[AdditiveSceneManager] Scene '{sceneName}' already loaded, skipping duplicate load.");
            yield break;
        }

        if (!Application.CanStreamedLevelBeLoaded(sceneName))
        {
            Debug.LogError($"[AdditiveSceneManager] Scene '{sceneName}' is not in Build Settings.");
            yield break;
        }

        loadedRoomScenes.Add(sceneName);

        AsyncOperation operation = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
        operation.allowSceneActivation = true;

        while (!operation.isDone)
        {
            yield return null;
        }

        Debug.Log($"[AdditiveSceneManager] Loaded room: {sceneName}");
    }
}
