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
        if (roomSceneNames.Count > 0)
        {
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
