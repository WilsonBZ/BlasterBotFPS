using System;
using UnityEngine;

public abstract class BaseEnemy : MonoBehaviour
{
    public event Action OnDeath;

    protected bool isDead = false;
    public bool IsDead => isDead;

    //protected void HandleDeath()
    //{
    //    if (isDead) return;
    //    //isDead = true;
    //    Debug.Log($"{gameObject.name} -> BaseEnemy.HandleDeath() invoked");
    //    //OnDeath?.Invoke();
    //}
}

