using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SmoothFollow : MonoBehaviour
{
    [Tooltip("The object to follow.")]
    [SerializeField] Transform target;
    [SerializeField] Vector3 followOffset;
    [SerializeField] Vector3 lookAtOffset;
    //[SerializeField] float followSpeed = 0.5f;
    [SerializeField] bool drawRay = true;



    private Vector3 current;
    private Vector3 goal; // the position to move towards
    //private float t = 0f; // The "time" variable, from 0 - 1, used for curve/lerp/gradient evaluation
    // Update is called once per frame
    void Update()
    {
        // Do nothing if no target is set
        if(target == null)
        {
            print("Warning: " + name + "'s SmoothFollow has no target set.");
            return;
        }

        //////// NOTE ////////
        // Haven't actually done any smoothing yet! Looks fine for now.

        // Get current position of this gameobject
        current = transform.position;
        // Set goal position: relative to target's position, set back from target transform, and displaced on y axis of target
        goal = target.position + (-target.forward * followOffset.z) + (target.up * followOffset.y) + (target.right * followOffset.x);
        // Move this object's position to the goal
        transform.position = goal;
        //transform.position = target.position + (-target.forward * followDistance) + (target.up * yOffset);

        // Visualize the direction this object is pointing
        if(drawRay)
            Debug.DrawRay(transform.position, transform.forward * 10f, Color.red);
        // Need to rotate so that camera is facing wherever the target is facing
        transform.LookAt(target.position + lookAtOffset, Vector3.up);

     
    }

    
}
