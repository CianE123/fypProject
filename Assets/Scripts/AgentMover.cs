using System.Collections.Generic;
using UnityEngine;

public class AgentMover : MonoBehaviour
{
    public float speed = 3f;  // Movement speed in units per second
    private Grid2D grid;
    private List<Node2D> activePath;
    private int currentPathIndex = 0;
    private bool shouldMove = false; // Flag to control movement
    
    private Vector3 originPosition; // Store the original position

    void Start()
    {
        // Store the initial position of the agent.
        originPosition = transform.position;
        // Find the Grid2D manager in the scene.
        grid = FindObjectOfType<Grid2D>();
    }

    void Update()
    {
        // Only move if movement is enabled.
        if (!shouldMove)
            return;

        if (grid == null)
            return;
        
        // Retrieve the active path based on the current solution type.
        if (grid.currentSolutionType == Grid2D.PathSolutionType.Standard)
        {
            if (grid.paths.ContainsKey(transform))
                activePath = grid.paths[transform];
        }
        else // CollisionFree path solution
        {
            if (grid.collisionFreePaths.ContainsKey(transform))
                activePath = grid.collisionFreePaths[transform];
        }
        
        // If no valid path exists, do nothing.
        if (activePath == null || activePath.Count == 0)
            return;
        
        // If the agent has reached the end of its path, stop moving.
        if (currentPathIndex >= activePath.Count)
            return;
        
        // Move the agent toward the current target node.
        Vector3 targetPosition = activePath[currentPathIndex].worldPosition;
        transform.position = Vector3.MoveTowards(transform.position, targetPosition, speed * Time.deltaTime);
        
        // If the agent is close enough to the target node, progress to the next node.
        if (Vector3.Distance(transform.position, targetPosition) < 0.1f)
        {
            currentPathIndex++;
        }
    }
    
    // Resets the path progress index.
    public void ResetPathProgress()
    {
        currentPathIndex = 0;
    }
    
    // Enables movement and resets progress.
    public void StartMovement()
    {
        ResetPathProgress();
        shouldMove = true;
    }
    
    // Resets the agent's position back to its origin and stops movement.
    public void ResetPosition()
    {
        Node2D node = grid.NodeFromWorldPoint(originPosition);
        // Snap to grid cell center
        transform.position = node.worldPosition; 
        ResetPathProgress();
        shouldMove = false;
    }
}
