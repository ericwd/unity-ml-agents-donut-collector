using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators; // for ActionBuffers 
using System;

public class PacmanAgent : Agent
{
    // PUBLIC
    [Header("Training Settings")]
    [Tooltip("Check if training a NN. Uncheck if using inference or heuristic.")]
    public bool LimitToMaxStep = true;
    public float smallReward = 0.01f;
    public float largeReward = 1f;
    public float smallPunishment = -0.01f;
    public float largePunishment = -1f;
    public bool drawLineToNearestDot = false;
    public bool drawLineToNearestHazard = false;

    [Header("Player Settings")]
    [Tooltip("Game over when hp reaches zero.")]
    [SerializeField] float start_hp = 3f;
    
    [Header("Motion")]
    [Tooltip("Controls move speed forwards and backwards.")]
    public float moveForce;
    [Tooltip("Set from 0 for frictionless to 100 for no friction.\nIt will damp the speed too though.")]
    public float moveFriction;
    [Tooltip("Multiplies MoveForce by this factor when walking backwards.")]
    public float backwardsScalingFactor = 0.25f;
    [Tooltip("How quickly character can turn.")]
    public float yawSpeed; 
    [Tooltip("Smoothes out turning.")]
    public float yawDamp; 

    // Accessors
    public float DistanceToNearestDot
    {
        get
        {
            // No dot set yet
            if(nearestDot == null) return 0f;
            // Distance to it
            return Vector3.Distance(transform.position, nearestDot.transform.position);
        }
        set
        {
            distanceToNearestDot = value;
        }
    }
    private float distanceToNearestDot;

    private Vector3 DirectionToNeatestDot
    {
        get
        {
            // If nearest isn't set, return (0,0,0)
            if(nearestDot == null) return Vector3.zero;
            // Normed vector pointing from agent to nearest dot
            return (nearestDot.transform.position - transform.position).normalized;
        }
        set
        {
            directionToNearestDot = value;
        }
    }
    private Vector3 directionToNearestDot;

    public float DistanceToNearestHazard
    {
        get
        {
            // No dot set yet
            if(nearestHazard == null) return 0f;
            // Distance to it
            return Vector3.Distance(transform.position, nearestHazard.transform.position);
        }
        set
        {
            distanceToNearestHazard = value;
        }
    }
    private float distanceToNearestHazard;

    // Set by GM if present. Used to normalize distance observations
    [HideInInspector] public float maxDistance = 1f;

    public Action<PacmanAgent> OnAgentDeath;
    // public static Action GetNearestHazard; // this event removed altogether for pm_13
    public Action<PacmanAgent> OnEpisodeBeginSignal;
    public string worldName = "world_not_set"; // /set by GM


    // OBJECT REFS
    [Header("Object Refs")]
    [SerializeField] Rigidbody rb;
    [SerializeField] Transform raycastOrigin;
    


    // PRIVATE
    private bool frozen = false; // prevents actions from happening
    private float smoothYawChange = 0f; // used to smoothly change the rotation
    private float hp; // curret hp, changes with damage

    // Set by GM, do not modify.
    private Dot nearestDot; 
    private Hazard nearestHazard;

    // A 20 position array to hold player position on the last 20 fixed frames
    private Vector3[] positionBuffer = new Vector3[20];
    


  

    protected override void OnEnable() {
        // world name not set at time of enable
        // print(worldName + " " + name + " agent says: OnEnable()");
        // subscribe to each instance's event instead -- done in SubscribeToInstanceEvents method, called by GM during initialize game
        //Dot.OnDotPickup += GiveDotReward;
        //Hazard.OnHazardCollision += GiveLargePunishment;
        //Hazard.OnHazardCollision += TakeDamage;

        // Initialize player's health
        hp = start_hp;

        // need to call this, otherwise nothing starts
        base.OnEnable(); 
    }

    protected override void OnDisable() {
        print(worldName + " " + name + " agent says: OnDisable()");
        // unsubscribe from each instance's event intead -- done in GM reset
        //Dot.OnDotPickup -= GiveDotReward;
        //Hazard.OnHazardCollision -= GiveLargePunishment;
        //Hazard.OnHazardCollision -= TakeDamage;

        // assuming also necessary
        base.OnDisable(); 
    }

    public void SubscribeToInstanceEvents(List<Dot> dots, List<Hazard> hazards){
        // Dot events
        foreach(Dot d in dots)
        {
            d.OnDotPickup += GiveDotReward;
        }

        // Hazard events
        foreach(Hazard h in hazards)
        {
            h.OnHazardCollision += GiveLargePunishment;
            h.OnHazardCollision += TakeDamage;
        }
    }
    public void UnsubscribeFromInstanceEvents(List<Dot> dots, List<Hazard> hazards){
        // Dot events
        foreach(Dot d in dots)
        {
            d.OnDotPickup -= GiveDotReward;
        }

        // Hazard events
        foreach(Hazard h in hazards)
        {
            h.OnHazardCollision -= GiveLargePunishment;
            h.OnHazardCollision -= TakeDamage;
        }
    }

    // Happens when dot is picked up. Called from GM's CheckRemainingDots
    public void UnsubscribeFromDotEvents(Dot d){
        d.OnDotPickup -= GiveDotReward;
    }
        

    public override void Initialize(){
        //print(worldName + " " + name + " agent says: Initialize()");

        // member of Agent class. This allows simulation to run forever when not in training mode.
        if(!LimitToMaxStep) MaxStep = 0; 
    }

    public override void OnEpisodeBegin(){
        // Reset game here too? If training
        print(worldName + " " + name + " agent says: OnEpisodeBegin()");

        OnEpisodeBeginSignal?.Invoke(this);

    }


    /// Gather input data for the NN to process or train with.
    public override void CollectObservations(VectorSensor sensor){

        // AGENT DIRECTION (2 obs)
        // Observations 1 and 2, forwards facing direction, i.e. sin/cos of y-euler angle
        Vector3 dir = transform.forward;
        // print(dir);
        // dir.y will always be zero in this game
        sensor.AddObservation(dir.x);
        sensor.AddObservation(dir.z);

        // NEAREST DONUT (3 obs)
        // Observations 3 and 4, x,z of unit vector pointing towards nearest donut
        if(nearestDot != null)
        {

            //print("Direction towards nearst dot: " + toDot);
            if(drawLineToNearestDot)
                Debug.DrawRay(transform.position, DirectionToNeatestDot * DistanceToNearestDot, Color.green);
            sensor.AddObservation(DirectionToNeatestDot.x);
            sensor.AddObservation(DirectionToNeatestDot.z);        

            // Observation 5, normalized distance to nearest donut
            float normDistanceToDot = DistanceToNearestDot / maxDistance;
            //print("Distance to nearest dot: " + normDistanceToDot);
            sensor.AddObservation(normDistanceToDot);
        }
        else
        {
            // No dot is set, send empty observations
            sensor.AddObservation(new float[3]);
        }

        // NOTE: Removed at pm_13
        # region Nearest Hazard Observations
        /*
        // NEAREST HAZARD (3 obs)
        // Nearst hazard can change each frame, request an upadte here.
        GetNearestHazard?.Invoke();
        // Observations 6 and 7, unit vector pointing towards nearest hazard
        if(nearestHazard != null)
        {
            Vector3 toHaz = (nearestHazard.transform.position - transform.position).normalized;
           //print("Direction towards nearst hazard: " + toHaz);
            if(drawLineToNearestHazard)
                Debug.DrawRay(transform.position, toHaz * DistanceToNearestHazard, Color.yellow);
            sensor.AddObservation(toHaz.x);
            sensor.AddObservation(toHaz.z);

            // Observation 8, normalized distance to nearest hazard
            float normDistanceToHaz = DistanceToNearestHazard / maxDistance;
            //print("Distance to nearest hazard: " + normDistanceToHaz);
            sensor.AddObservation(normDistanceToHaz);
        }
        else
        {
            // No hazard is set, send empty observations
            sensor.AddObservation(new float[3]);
        }
        */
        # endregion
    }

    // Perform these actions, i.e. movement, jumping, shooting, etc.
    // Actions can be defined from player input (via Heuristic) or NN output
    // Continous Actions
    // actions.ContinuousActions[0] = Move forwards (+1) and move backwards (-1) (agent's forward direction)
    // actions.ContinuousActions[1] = Euler rotate around y-axis CCQ (-1) or CQ (+1)
    public override void OnActionReceived(ActionBuffers actions)
    {
        if(frozen) return;
        
        // Scaling factor for force, from -1 to +1
        float f = actions.ContinuousActions[0];
        float backwards = 1f;
        // If walking backwards, i.e. when f < 0, apply another scaling factor to make walking backwards slower
        if(f < 0f)
            backwards = backwardsScalingFactor;
        // Apply as a force to agent's forward direction
        rb.AddForce(f * backwards * moveForce * transform.forward);
        // Add friction by scaling velocity vector.
        // Move friction is a largeish positive number, e.g. if 10 will multiply velocity by 0.9 each frame
        rb.velocity = rb.velocity * (1 - moveFriction * 0.01f);
        



        // Rotation about y-axis, yaw
        // Calculate change in yaw (based on output of NN or heuristic which is -1 to +1)
        // e.g. +1 means turn at maximum speed
        float targetYaw = actions.ContinuousActions[1];
        smoothYawChange = Mathf.MoveTowards(smoothYawChange, targetYaw, yawDamp * Time.fixedDeltaTime);
        // Get current Euler angle of the agent
        Vector3 r = transform.rotation.eulerAngles;
        // Set new yaw value
        float yaw = r.y + smoothYawChange * yawSpeed * Time.fixedDeltaTime;
        transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        
    }

    // When not using a NN, define a function or get player input
    // to be used as the actions. These need to match the actions in
    // OnActionsReceived
    public override void Heuristic(in ActionBuffers actionsOut)
    {
        // https://github.com/Unity-Technologies/ml-agents/blob/main/Project/Assets/ML-Agents/Examples/3DBall/Scripts/Ball3DAgent.cs
        var continuousActionsOut = actionsOut.ContinuousActions;

        // Initialize vars to be set with WASD/Arrow keys
        float forward = 0f;
        float backward = 0f;
        // Turning input
        float left = 0f;
        float right = 0f;

        // Forwards
        if(Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow))
            forward = 1f;

        // -Z movement
        if(Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow))
            backward = -1f;

        // Turn left
        if(Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow))
            left = -1f;

        // Turn right
        if(Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow))
            right = 1f;

        // Pressing e.g., left and right will cancel input to zero this way
        float translate = (forward + backward);
        float turn = (left + right);

        //print("Translate = " + translate + ", Turn = " + turn);

        // Set the actions
        continuousActionsOut[0] = translate;
        continuousActionsOut[1] = turn;
        
    }

    public void FreezeAgent()
    {
        Debug.Assert(LimitToMaxStep == false, "Freeze/Unfreeze not supported in training.");
        frozen = true;
        rb.Sleep(); // turns off physics for this rb

    }

    public void UnfreezeAgent()
    {
        Debug.Assert(LimitToMaxStep == false, "Freeze/Unfreeze not supported in training.");
        frozen = false;
        rb.WakeUp(); // turns off physics for this rb

    }

    public void ResetAgent(){
        hp = start_hp;
    }

    public void SetNearestDot(Dot d){
        nearestDot = d;
    }

    public void SetNearestHazard(Hazard h){
        nearestHazard = h;
    }

    public void GiveReward(float reward){
        //print("Reward: " + reward);
        AddReward(reward);
    }

    private void GiveDotReward(Dot d){
        // Dot not currently uses here, but param needed for dictionary elsewhere
        //print("Reward: " + largeReward);
        AddReward(largeReward);
    }

    private void GiveLargePunishment(float damage){
        // damage amount not needed for rewards at the moment
        //print("Reward: " + largePunishment);
        AddReward(largePunishment);
    }

    private void TakeDamage(float damage){
        hp -= damage;
        print(worldName + ": HP = " + hp);
        // Check for death, end of game
        if(hp <= Mathf.Epsilon)
        {
            GiveReward(largePunishment * 10f);
            print(worldName + ": Game over");
            // Ends this agent's episide, step number should go back to zero
            EndEpisode();
            OnAgentDeath?.Invoke(this);
        }
    }

    private void OnCollisionEnter(Collision other) {
        //print(name + " collided with " + other.collider.name);
        // If you collide with a wall, bonk!
        if(other.collider.CompareTag("boundary"))
            GiveReward(largePunishment);
    }

    private void FixedUpdate() {
        // CALC REWARD FACTORS
        // If facing towards nearest donut, give a small reward each step to encourage forwards walking
        // -1 when facing away from donut, +1 when facing it, 0 when perpendicular
        float orientationFactor = Vector3.Dot(DirectionToNeatestDot, transform.forward);
        // Punish distance from donut: 0 right next to it, +1 for for away (it will be a punishment)
        float donutDistanceFactor = DistanceToNearestDot / maxDistance;
        // Use agent position from 20 frames ago and reward for movement, punish for "getting stuck"
        // 1.52 is how far you can run in a stright line in 20 frames (will change with speed)
        // Returns 0 for standing still and +1 for moving
        float distanceToPast = TrackPosition(20, 1.52f, false, true);
        // shift to -1 to 0
        float hustleFactor = distanceToPast - 1f;
        // If facing a hazard within 2 meters, factor = 1, else = 0 --> negative reward for getting stuck
        float hazardProximity = 0f;
        if(CheckForwardRaycastTag("hazard", raycastOrigin, 2f, true))
            hazardProximity = 1f;

        // APPLY REWARDS EACH FIXED UPDATE FRAME
        // Can be + (facing it) or - (looking away), 1/10 to discourage camping/staring.
        float orientationReward = orientationFactor * smallReward * 0.1f;
        AddReward(orientationReward);
        // Reward is always negative, factor is +1 (far away) or 0 (touching)
        float donutDistanceReward = donutDistanceFactor * smallPunishment;
        AddReward(donutDistanceReward);
        // -1 for standing still, 0 for full steam ahead (factor always <= 0 --> always negative reward)
        float hustleReward = hustleFactor * smallReward;
        AddReward(hustleReward);
        // -0.1 each frame if in proximity and facing a hazard;
        float hazardProximityReward = hazardProximity * -0.1f;
        AddReward(hazardProximityReward);


        // Round and print these factors for debugging
        bool verbose = false;
        if(verbose)
        {
            orientationFactor = Mathf.Round(orientationFactor * 100f) * 0.01f;
            donutDistanceFactor = Mathf.Round(donutDistanceFactor * 100f) *0.01f;
            hustleFactor = Mathf.Round(hustleFactor * 100f) * 0.01f;
            print("Factors: O = " + orientationFactor + ", D = " + donutDistanceFactor + ", H = " + hustleFactor + ", P = " + hazardProximity);
            print("Rewards: O = " + orientationReward + ", D = " + donutDistanceReward + ", H = " + hustleReward + ", P = " + hazardProximityReward);
            float netReward = orientationReward + donutDistanceReward + hustleReward + hazardProximityReward;
            print("Net reward: " +  netReward);
        }

    }

    // Returns true if raycast hits object with tag. 
    private bool CheckForwardRaycastTag(string tag, Transform origin, float maxRaycastDistance = 10f, bool drawRays = false){
        RaycastHit hit;
        Vector3 originVector = origin.position;
        Vector3 direction = origin.forward;
        if(Physics.Raycast(originVector, direction, out hit, maxRaycastDistance))
        {
            if(drawRays)
                Debug.DrawRay(originVector, direction * maxRaycastDistance, Color.red);

            return (hit.collider.CompareTag(tag));            
        }
        else
        {
            if(drawRays)
                Debug.DrawRay(originVector, direction * maxRaycastDistance, Color.white);

            return false;
        }
    }


    // Tracks and returns position from n frames ago. Buffer size is currently 20
    // Frames is clamped between 1 and the length of the position buffer.
    // Optionally specify norm distance to return normalized results (default is 1)
    private float TrackPosition(int frames, float norm = 1f, bool verbose = false, bool drawLine = false){
        // Shift buffer to right by 1
        positionBuffer = Shift(positionBuffer);
        // Record current position
        positionBuffer[0] = transform.position;
        // warning message for bad input
        if(frames > positionBuffer.Length)
            print("Requestion position " + frames + "frames ago, but buffer size only " + positionBuffer.Length + ". Using max size.");
        // frames must be within the bounds of array, and > 0
        frames = Mathf.Clamp(frames, 1, positionBuffer.Length);
        // calc the distance and normalize
        float d = Vector3.Distance(positionBuffer[0], positionBuffer[frames - 1]) / norm;
        // print/draw stuff for testing
        if(verbose)
            print("D" + frames + ": " + d);
        if(drawLine)
            Debug.DrawLine(positionBuffer[0], positionBuffer[frames - 1], Color.green);
        return d;
    }

    // Returns array shifted n positions. Defaults to +1 (right shift)
    private T[] Shift<T>(T[] array, int n = 1){
        var newArray = new T[array.Length];
        int k; // position in new array
        // Mod input to remove multiple wrap arounds, e.g. shifting by array.Length doesn't do anything
        n = n % array.Length;
        // Loop over input array positions and calculate new ones
        for(int j = 0; j < array.Length; j++)
        {
            // shift to new position
            k = j + n;
            // right shifts may be out of upper bounds
            if(k >= array.Length) 
                k -= array.Length;
            // left shifts may be out of lower bounds
            if(k < 0) 
                k += array.Length;
            // Construct new array
            //print("j:" + j + " k:" + k);
            newArray[k] = array[j]; 
        }
        return newArray;
    }


}
