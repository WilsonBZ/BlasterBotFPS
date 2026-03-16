using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Persistent singleton that tracks floor progression, enemy difficulty scaling,
/// and buff history. Handles resetting all room scenes at the end of a floor
/// while preserving player buffs and increasing enemy spawn counts.
/// </summary>
[DisallowMultipleComponent]
public class FloorProgressManager : MonoBehaviour
{
    public static FloorProgressManager Instance
    {
        get; private set;
    }

    [Header("Difficulty Scaling")]
    [Tooltip("Enemy count multiplier added per completed floor. e.g. 0.25 = +25% enemies each floor.")]
    public float enemyCountScalePerFloor = 0.25f;

    [Tooltip("Delay in seconds before scenes reset after buff selection, giving time for any transition.")]
    public float resetDelay = 1.0f;

    /// <summary>Current floor number, starting at 1.</summary>
    public int CurrentFloor { get; private set; } = 1;

    /// <summary>Cumulative enemy count multiplier applied to all WaveSpawners on reset.</summary>
    public float EnemyMultiplier => 1f + (CurrentFloor - 1) * enemyCountScalePerFloor;

    private readonly List<BuffData> appliedBuffHistory = new List<BuffData>();

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
        SubscribeToBuffManager();
    }

    /// <summary>
    /// Subscribes to the current NewBuffManager instance.
    /// Called on Start and again after every scene reset so the persistent
    /// FloorProgressManager stays connected to the freshly reloaded NewBuffManager.
    /// </summary>
    private void SubscribeToBuffManager()
    {
        if (NewBuffManager.Instance != null)
        {
            // Guard against double-subscription.
            NewBuffManager.Instance.OnBuffApplied -= RecordBuff;
            NewBuffManager.Instance.OnBuffApplied += RecordBuff;
        }
        else
        {
            Debug.LogWarning("[FloorProgressManager] NewBuffManager.Instance not found.");
        }
    }

    private void RecordBuff(BuffData buff)
    {
        appliedBuffHistory.Add(buff);
    }

    /// <summary>
    /// Called by <see cref="ElevatorRoomTrigger"/> after the player selects a buff.
    /// Increments the floor, resets all room scenes, then re-applies buffs and difficulty.
    /// </summary>
    public void AdvanceFloor()
    {
        CurrentFloor++;
        StartCoroutine(ResetFloorRoutine());
    }

    private IEnumerator ResetFloorRoutine()
    {
        yield return new WaitForSeconds(resetDelay);

        // Reset all additive room scenes back to room 0.
        if (AdditiveSceneManager.Instance != null)
        {
            yield return StartCoroutine(AdditiveSceneManager.Instance.ResetToStart());
        }

        // Wait a frame for scenes to finish activating.
        yield return null;
        yield return null;

        // Re-subscribe to the freshly reloaded NewBuffManager.
        SubscribeToBuffManager();

        // Re-apply all previously chosen buffs to the new player instance.
        ApplyBuffHistoryToNewPlayer();

        // Scale enemy counts on all WaveSpawners in the newly loaded scenes.
        ApplyDifficultyToSpawners();
    }

    private void ApplyBuffHistoryToNewPlayer()
    {
        PlayerManager player = FindFirstObjectByType<PlayerManager>();
        if (player == null)
        {
            Debug.LogWarning("[FloorProgressManager] No PlayerManager found after scene reset. Buffs not re-applied.");
            return;
        }

        foreach (BuffData buff in appliedBuffHistory)
        {
            if (buff != null)
            {
                buff.ApplyBuff(player);
            }
        }

        Debug.Log($"[FloorProgressManager] Re-applied {appliedBuffHistory.Count} buff(s) to player on floor {CurrentFloor}.");
    }

    private void ApplyDifficultyToSpawners()
    {
        float multiplier = EnemyMultiplier;
        WaveSpawner[] spawners = FindObjectsByType<WaveSpawner>(FindObjectsSortMode.None);

        foreach (WaveSpawner spawner in spawners)
        {
            spawner.ApplyEnemyCountMultiplier(multiplier);
        }

        Debug.Log($"[FloorProgressManager] Floor {CurrentFloor}: applied {multiplier:F2}x enemy multiplier to {spawners.Length} spawner(s).");
    }
}
