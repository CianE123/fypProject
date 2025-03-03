using UnityEngine;

public class PathfindingManager : MonoBehaviour
{
    // Toggle for penalty usage
    public bool usePenalty = false;
    //Penalty increment 
    public int penaltyIncrement = 10; 
    // Toggle for neighbor penalty usage
    public bool expandPenalty = false;
    //Neighbor penalty increment 
    public int neighborPenaltyIncrement = 3;

    void Update()
    {
        // Trigger pathfinding for all agents when the Spacebar is pressed
        if (Input.GetKeyDown(KeyCode.Space))
        {
            TriggerPathfindingForAll();
        }
    }

    void TriggerPathfindingForAll()
    {
        // Reset the penalty grid before starting
        Grid2D grid = FindObjectOfType<Grid2D>();
        if (grid != null)
        {
            grid.ResetPenaltyGrid();
        }

        // Process all agents sequentially
        Pathfinding2D[] pathfinders = FindObjectsOfType<Pathfinding2D>();
        foreach (Pathfinding2D pathfinder in pathfinders)
        {
            if (pathfinder != null)
            {
                pathfinder.FindPath(usePenalty, penaltyIncrement, expandPenalty, neighborPenaltyIncrement);
            }
        }
    }
}
