using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

public class Hazard : MonoBehaviour
{
    [Tooltip("The damage delt on collision with this hazard.")]
    [SerializeField] float damage = 1f;
    public Action<float> OnHazardCollision;
    private void OnCollisionEnter(Collision other) {
        // Can only interact with player
        if(other.collider.CompareTag("Player"))
            HandleCollision();
        
    }

    private void HandleCollision(){ 
        //print("Ouch!");
        OnHazardCollision?.Invoke(damage);
    }

}
