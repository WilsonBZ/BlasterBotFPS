using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DoorController : MonoBehaviour
{
    private Collider doorCollider;
    private Renderer doorRenderer;

    private void Awake()
    {
        doorCollider = GetComponent<Collider>();
        doorRenderer = GetComponent<Renderer>();
    }

    public void ActivateDoor()
    {
        doorCollider.enabled = true;
        doorRenderer.enabled = true; 
    }

    public void DeactivateDoor()
    {
        doorCollider.enabled = false;
        doorRenderer.enabled = false; 
    }
}
