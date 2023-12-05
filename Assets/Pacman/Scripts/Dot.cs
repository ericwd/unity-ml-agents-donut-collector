using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

public class Dot : MonoBehaviour
{
    public Action<Dot> OnDotPickup;
    private void OnTriggerEnter(Collider other) {
        // Can only interact with player
        if(other.CompareTag("Player"))
            Pickup();
    }

    private void Pickup(){ 
        //print(name + " was picked up");
        OnDotPickup?.Invoke(this);
        Destroy(gameObject);
    }

}
