using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

public class PacmanTrainingManager : MonoBehaviour
{
    [Header("Environment Settings")]
    [SerializeField] int numDots;
    [SerializeField] int numHazards;
    [SerializeField] float minSpawnRadius;


    [Header("Object References")]
    [SerializeField] PacmanAgent pacmanAgent;
    [Tooltip("Objects spawned between these bounds.")]
    [SerializeField] Transform upperLeftBound;
    [Tooltip("Objects spawned between these bounds.")]
    [SerializeField] Transform lowerRightBounds;
    [SerializeField] Dot dotPrefab;
    [SerializeField] Transform dotContainer;
    [SerializeField] Hazard hazardPrefab;
    [SerializeField] Transform hazardContainer;


    // Holds the position of each dot. Gets erased when picked up.
    private Dictionary<Dot, Vector3> dots = new Dictionary<Dot, Vector3>();
    // Hold position of each hazard. No erasure at the moment.
    private Dictionary<Hazard, Vector3> hazards = new Dictionary<Hazard, Vector3>();
    // Used in initialize to check existing positions of instantiated objects
    private List<Vector3> positions = new List<Vector3>();
    // The max distance two things can be appart, the diagonal between the bounds
    private float maxDistance;
    private string worldName;

    private void Start() {
        // Calc and set the max distance on the agent. Should not change during a game sessions
        maxDistance = Vector3.Distance(upperLeftBound.position, lowerRightBounds.position);
        pacmanAgent.maxDistance = maxDistance;
        // Get the name of the parent world, so events from parallel worlds can be distinguished.
        if(transform.parent != null)
            worldName = transform.parent.name;
        else
            worldName = "ROOT";
        // Tell the agent which world they're in
        pacmanAgent.worldName = worldName;

        // Initialize the first game. Not needed, reset --> initialized called when agent's OnEpisodeBegin happens
        // InitializeGame();
    }

    private void OnEnable() {
        pacmanAgent.OnAgentDeath += ResetGame;
        pacmanAgent.OnEpisodeBeginSignal += ResetGame;
    }

    private void OnDisable() {
        pacmanAgent.OnAgentDeath -= ResetGame;
        pacmanAgent.OnEpisodeBeginSignal -= ResetGame;
    }

    // Finds a random location between bounds for a new object to be spawned.
    // Must be at least minSpawnRadius away from all other objects.
    private Vector3 GenerateXZCoordinates(){
        int maxTries = 1000;
        int tries = 0;
        bool valid;
        Vector3 v = Vector3.zero;
        do
        {
            tries++;
            valid = true; // assume this position will work
            // Pick a random coordinate in the X,Z plane. Y defaults to zero always (should be "on the floor")
            float x = UnityEngine.Random.Range(upperLeftBound.position.x, lowerRightBounds.position.x);
            float z = UnityEngine.Random.Range(upperLeftBound.position.z, lowerRightBounds.position.z);
            v = new Vector3(x, 0f, z);

            // Check to see if v is within minSpawnRadius of anything that already exists (should have been added to positions list)
            foreach(Vector3 p in positions)
            {
                // If the proposed location v is too close to an existing location p
                if(Vector3.Distance(p, v) < minSpawnRadius)
                {
                    valid = false;
                    break;
                }
            }

        } while(!valid && (tries <= maxTries));

        //print("Used " + tries + " tries");

        if(tries > maxTries)
            print("Warning, max tries of " + maxTries + " was exceeded. Location " + v + " may not be valid!");

        return v;
    }

    private void InitializeGame(){
        //print("GM: Initialize");
        // Start a new episode for the agent, not needed. Now OnEpisodeBegin calls Restart-->Initialize.
        //pacmanAgent.OnEpisodeBegin();
        // Give the agent (already in the scene) a random location and y rotation
        pacmanAgent.transform.position = GenerateXZCoordinates();
        pacmanAgent.transform.rotation = Quaternion.Euler(0f,UnityEngine.Random.Range(-180f, 180f), 0f);
        positions.Add(pacmanAgent.transform.position); // save to list for position checking

        // Place the hazards
        for(int j = 0; j < numHazards; j++)
        {
            Hazard h = Instantiate<Hazard>(hazardPrefab, GenerateXZCoordinates(), Quaternion.identity);
            h.transform.SetParent(hazardContainer); // move to the container to keep things neat
            hazards.Add(h, h.transform.position); // add to list for future searches -- maybe don't even need this??
            positions.Add(h.transform.position); // this is what's used in GenerateXZCoordinates

        }

        // Place the dots (donuts)
        for(int j = 0; j < numDots; j++)
        {
            Dot d = Instantiate<Dot>(dotPrefab, GenerateXZCoordinates(), Quaternion.identity);
            d.transform.SetParent(dotContainer); // move to the container to keep things neat
            dots.Add(d, d.transform.position); // add to list for future searches, remove dot from dict when it's picked up.
            positions.Add(d.transform.position);
        }

        // Find and set the agent's nearest dot
        pacmanAgent.SetNearestDot(GetNearestDot());

        // Subscribe to instance events (just dots)
        SubscribeToAllDotEvents(dots.Keys.ToList<Dot>());

        // Tell the agent to subscribe to it's events (pass in dots and hazard lists)
        // Function just needs lists of the dots/hazards. Use Linq's ToList function.
        pacmanAgent.SubscribeToInstanceEvents(dots.Keys.ToList<Dot>(), hazards.Keys.ToList<Hazard>());
        

    }

    private void SubscribeToAllDotEvents(List<Dot> dots){
        // Dot events
        foreach(Dot d in dots)
        {
            d.OnDotPickup += CheckDotsRemaining;
        }
    }
    private void UnsubscribeFromAllDotEvents(List<Dot> dots){
        // Dot events
        foreach(Dot d in dots)
        {
            d.OnDotPickup -= CheckDotsRemaining;
        }
    }

    

    private Dot GetNearestDot()
    {
        if(dots.Count == 0)
        {
            print("Can't get nearest dot, none in list.");
            return null;
        }

        Dot nearest = null;
        float minDistance = -1f; // initialize
        foreach(KeyValuePair<Dot, Vector3> pair in dots)
        {
            // Calculate distance from agent to each dot
            float distance = Vector3.Distance(pacmanAgent.transform.position, pair.Value);

            // first calc
            if(minDistance == -1f)
            {
                minDistance = distance;
                nearest = pair.Key;
            }

            // check if the current computation is less than min so far
            // if yes, this is the closest (so far)
            if(distance < minDistance)
            {
                minDistance = distance;
                nearest = pair.Key;
            }

        }

        return nearest;
    }

    private void UpdateHazard()
    {
        //print("Update hazard event");
        pacmanAgent.SetNearestHazard(GetNearestHazard());
    }

    private Hazard GetNearestHazard()
    {
        if(hazards.Count == 0)
        {
            print("Can't get nearest hazard, none in list.");
            return null;
        }

        Hazard nearest = null;
        float minDistance = -1f; // initialize
        foreach(KeyValuePair<Hazard, Vector3> pair in hazards)
        {
            // Calculate distance from agent to each dot
            float distance = Vector3.Distance(pacmanAgent.transform.position, pair.Value);

            // first calc
            if(minDistance == -1f)
            {
                minDistance = distance;
                nearest = pair.Key;
            }

            // check if the current computation is less than min so far
            // if yes, this is the closest (so far)
            if(distance < minDistance)
            {
                minDistance = distance;
                nearest = pair.Key;
            }

        }
        return nearest;
    }


    private void ResetGame(PacmanAgent agent){
        // input agent is only needed to catch event, but it will be the same as the pacmanAgent in the inspector. 
        print(worldName + " GM: Reset/Init");
        // Unsubscribe from events (just dots for GM)
        UnsubscribeFromAllDotEvents(dots.Keys.ToList<Dot>());
        // Tell agent to do so too (remaining dots and hazards)
        pacmanAgent.UnsubscribeFromInstanceEvents(dots.Keys.ToList<Dot>(), hazards.Keys.ToList<Hazard>());

        // Delete all the old positions and dictionaries
        positions.Clear();
        dots.Clear();
        hazards.Clear();
        // Delete all the dots and hazards.
        // First find the children of the hazards node. Lists may have changed in game
        Hazard[] haz = hazardContainer.GetComponentsInChildren<Hazard>();
        foreach(Hazard h in haz)
            Destroy(h.gameObject);

        Dot[] dot = dotContainer.GetComponentsInChildren<Dot>();
        foreach(Dot d in dot)
            Destroy(d.gameObject);

        // Restore players health
        // It's possible that I have multiple games and agents running at a time, so I passed in the PacmanAgent
        // itself. Therefore, reset that specific one.
        pacmanAgent.ResetAgent();

        // Start a new game
        InitializeGame();
    }

    private void CheckDotsRemaining(Dot d){
        // Unsubscribe from this dot's events
        d.OnDotPickup -= CheckDotsRemaining;
        pacmanAgent.UnsubscribeFromDotEvents(d);
        // Remove this dot from the dictionary.
        dots.Remove(d);
        //print("Dict contains " + dots.Count + " more dots.");
        
        // Check for win.
        if(dots.Count == 0)
        {
            print(worldName + ": WIN!");
            pacmanAgent.GiveReward(pacmanAgent.largeReward * 10f);
            // Tells agent episode is over, resets step count
            pacmanAgent.EndEpisode();
            ResetGame(pacmanAgent);
            return;
        }

        // Not over yet, find the next closest dot
        pacmanAgent.SetNearestDot(GetNearestDot());
    }

    private void Update() {
        // Debug the object placement
        if(Input.GetKeyDown(KeyCode.Space)) ResetGame(pacmanAgent);
    }

}
