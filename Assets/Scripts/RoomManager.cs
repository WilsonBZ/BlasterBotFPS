using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

public class RoomManager : MonoBehaviour
{
    [Header("Room Configuration")]
    [Tooltip("Doors that trigger waves when player approaches (entrance doors)")]
    public SlidingDoubleDoor[] entranceDoors;
    
    [Tooltip("Doors that only open when room is cleared (exit doors)")]
    public SlidingDoubleDoor[] exitDoors;
    
    [Tooltip("All wave spawners in this room")]
    public WaveSpawner[] waveSpawners;

    [Header("Room State")]
    [Tooltip("Is this room currently active?")]
    public bool isActive = false;
    
    [Tooltip("Has this room been cleared?")]
    public bool isCleared = false;

    private int activeSpawners = 0;
    private bool hasStarted = false;

    public event Action OnRoomStarted;
    public event Action OnRoomCleared;

    private void Start()
    {
        SetupRoom();
    }

    private void SetupRoom()
    {
        foreach (var exitDoor in exitDoors)
        {
            if (exitDoor != null)
            {
                exitDoor.isLocked = true;
                exitDoor.triggerOnOpen = false;
            }
        }

        foreach (var spawner in waveSpawners)
        {
            if (spawner != null)
            {
                spawner.OnWaveStarted += HandleWaveStarted;
                spawner.OnAllWavesCompleted += HandleSpawnerCompleted;
            }
        }

        foreach (var entranceDoor in entranceDoors)
        {
            if (entranceDoor != null)
            {
                entranceDoor.OnDoorOpened += HandleEntranceDoorOpened;
            }
        }
    }

    private void OnDestroy()
    {
        foreach (var spawner in waveSpawners)
        {
            if (spawner != null)
            {
                spawner.OnWaveStarted -= HandleWaveStarted;
                spawner.OnAllWavesCompleted -= HandleSpawnerCompleted;
            }
        }

        foreach (var entranceDoor in entranceDoors)
        {
            if (entranceDoor != null)
            {
                entranceDoor.OnDoorOpened -= HandleEntranceDoorOpened;
            }
        }
    }

    private void HandleEntranceDoorOpened(SlidingDoubleDoor door)
    {
        if (!hasStarted)
        {
            hasStarted = true;
            isActive = true;
            OnRoomStarted?.Invoke();
            Debug.Log($"Room {gameObject.name} started!");
        }
    }

    private void HandleWaveStarted(int waveIndex)
    {
        activeSpawners++;
    }

    private void HandleSpawnerCompleted()
    {
        activeSpawners--;
        
        if (activeSpawners <= 0 && hasStarted && !isCleared)
        {
            ClearRoom();
        }
    }

    private void ClearRoom()
    {
        isCleared = true;
        isActive = false;
        
        foreach (var exitDoor in exitDoors)
        {
            if (exitDoor != null)
                exitDoor.isLocked = false;
        }
        
        OnRoomCleared?.Invoke();
        Debug.Log($"Room {gameObject.name} cleared! Exit doors unlocked.");

        // Trigger buff selection after every room clear.
        if (NewBuffManager.Instance != null)
            NewBuffManager.Instance.ShowBuffSelection();
    }

    public void ForceStartRoom()
    {
        if (!hasStarted)
        {
            hasStarted = true;
            isActive = true;
            OnRoomStarted?.Invoke();
            
            foreach (var spawner in waveSpawners)
            {
                if (spawner != null)
                {
                    spawner.StartWaves();
                }
            }
        }
    }

    public void ForceClearRoom()
    {
        if (!isCleared)
        {
            ClearRoom();
        }
    }
}
