using System.Collections.Generic;
using UnityEngine;

public class AgentMover : MonoBehaviour
{
    public float speed = 3f;           // Movement speed (units per second) - used for interpolation smoothness
    public float timePerStep = 0.5f;   // Time allocated for each step/wait in the path (in seconds)

    private Grid2D grid;
    private List<Node2D> activePath;
    private int currentPathIndex = 0;
    private bool shouldMove = false;     // Flag to control movement
    private float stepTimer = 0f;        // Timer to track time spent in the current step
    private Vector3 startPositionForStep; // Position at the beginning of the current step interpolation
    private Vector3 targetPositionForStep; // Target position for the current step

    private Vector3 originPosition;      // Store the original position

    void Start()
    {
        // Store the initial position of the agent.
        originPosition = transform.position;
        // Find the Grid2D manager in the scene.
        grid = FindObjectOfType<Grid2D>();
        if (grid == null)
        {
            Debug.LogError("AgentMover could not find Grid2D!", this);
        }
    }

    void Update()
    {
        // Only move if movement is enabled and grid exists.
        if (!shouldMove || grid == null)
            return;

        // Ensure we have a valid path and are within its bounds.
        // Re-check path validity each frame in case it changes or finishes.
        if (activePath == null || currentPathIndex >= activePath.Count)
        {
            // Attempt to retrieve the path if we don't have one but should be moving
            RetrieveActivePath();
            if (activePath == null || currentPathIndex >= activePath.Count)
            {
                 // Still no valid path, stop movement.
                shouldMove = false;
                return;
            }
             // If path retrieved successfully, initialize the first step
            InitializeStep();
        }


        // --- Time-based Step Progression ---

        // Increment the timer for the current step
        stepTimer += Time.deltaTime;

        // Calculate interpolation factor (clamp between 0 and 1)
        // This determines how far along the movement between start/target we should be.
        float interpolationFactor = Mathf.Clamp01(stepTimer / timePerStep);

        // Interpolate position smoothly between the start and target node of the CURRENT step
        transform.position = Vector3.Lerp(startPositionForStep, targetPositionForStep, interpolationFactor);

        // Check if the time allocated for this step has elapsed
        if (stepTimer >= timePerStep)
        {
            // Ensure agent is exactly at the target node position before proceeding
            transform.position = targetPositionForStep;

            // Advance path index to prepare for the next step
            currentPathIndex++;

            // Reset the timer for the new step
            stepTimer = 0f;

            // Check if we've completed the path
            if (currentPathIndex >= activePath.Count)
            {
                shouldMove = false; // Stop movement
            }
            else
            {
                 // Initialize positions for the next step's interpolation
                 InitializeStep();
            }
        }
    }

    // Retrieves the correct path from Grid2D based on the current solution type
    void RetrieveActivePath()
    {
        if (grid == null) return;

        activePath = null; // Reset previous path
        Dictionary<Transform, List<Node2D>> sourcePaths = null;

        if (grid.currentSolutionType == Grid2D.PathSolutionType.Standard)
        {
            sourcePaths = grid.paths;
        }
        else // CollisionFree path solution
        {
             sourcePaths = grid.collisionFreePaths;
        }

        if (sourcePaths != null && sourcePaths.ContainsKey(transform))
        {
            activePath = sourcePaths[transform];
             // Validate path has nodes
            if (activePath != null && activePath.Count == 0)
            {
                activePath = null; // Treat empty path as invalid
            }
        }
    }

     // Sets up the start and target positions for the current step's interpolation
    void InitializeStep()
    {
        if (activePath == null || currentPathIndex >= activePath.Count) return;

        // Start position is the node from the *previous* step (or current transform if it's the very first step)
        // Note: The path from CollisionFree includes the start node at index 0.
        if (currentPathIndex == 0)
        {
            startPositionForStep = activePath[0].worldPosition; // Start at the first node's position
             // Ensure agent starts exactly at the first node when movement begins
             // Use transform.position only if path is empty or index is wrong somehow, otherwise trust the path.
             transform.position = startPositionForStep;
        }
        else
        {
            startPositionForStep = activePath[currentPathIndex - 1].worldPosition;
        }


        // Target position is the node for the *current* time step
        targetPositionForStep = activePath[currentPathIndex].worldPosition;
    }

    // Resets the path progress index and timer.
    public void ResetPathProgress()
    {
        currentPathIndex = 0;
        stepTimer = 0f;
        activePath = null; // Clear cached path
    }

    // Enables movement, resets progress, and retrieves the initial path.
    public void StartMovement()
    {
        ResetPathProgress(); // Resets index and timer
        shouldMove = true;
        RetrieveActivePath(); // Get the path immediately

        // Initialize the first step if path is valid
        if (activePath != null && activePath.Count > 0)
        {
            InitializeStep();
             // Snap to start position immediately
            transform.position = startPositionForStep;
        }
        else
        {
            Debug.LogWarning($"{gameObject.name}: No valid path found to start movement.", this);
            shouldMove = false; // Cannot move without a path
        }
    }

    // Resets the agent's position back to its origin and stops movement.
    public void ResetPosition()
    {
         if (grid != null)
         {
             Node2D originNode = grid.NodeFromWorldPoint(originPosition);
             // Snap to grid cell center of the original node
             transform.position = originNode.worldPosition;
         }
         else
         {
             // Fallback if grid is somehow null
             transform.position = originPosition;
         }
        ResetPathProgress();
        shouldMove = false;
    }
}