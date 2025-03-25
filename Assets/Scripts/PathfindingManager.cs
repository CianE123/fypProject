using UnityEngine;

public class PathfindingManager : MonoBehaviour
{
    // Toggle for penalty usage
    public bool usePenalty = false;
    // Penalty increment 
    public int penaltyIncrement = 10; 
    // Toggle for neighbor penalty usage
    public bool expandPenalty = false;
    // Neighbor penalty increment 
    public int neighborPenaltyIncrement = 3;
    // Toggle for collision-free pathfinding
    public bool collisionFree = false;

    void Update()
    {
        // Compute or update paths when Space is pressed.
        if (Input.GetKeyDown(KeyCode.Space))
        {
            TriggerPathfindingForAll();
        }
        
        // Start agent movement when R is pressed.
        if (Input.GetKeyDown(KeyCode.R))
        {
            StartMovementForAll();
        }
        
        // Reset all agents to their original positions when T is pressed.
        if (Input.GetKeyDown(KeyCode.T))
        {
            ResetPositionsForAll();
        }
    }

    void TriggerPathfindingForAll()
    {
        // Reset the penalty grid before starting pathfinding.
        Grid2D grid = FindObjectOfType<Grid2D>();
        if (grid != null)
        {
            grid.ResetPenaltyGrid();
        }

        if (!collisionFree)
        {
            // Process standard pathfinding agents.
            Pathfinding2D[] pathfinders = FindObjectsOfType<Pathfinding2D>();
            foreach (Pathfinding2D pathfinder in pathfinders)
            {
                if (pathfinder != null)
                {
                    pathfinder.FindPath(usePenalty, penaltyIncrement, expandPenalty, neighborPenaltyIncrement);
                    
                    // Reset the movement progress (if an AgentMover is attached).
                    AgentMover mover = pathfinder.GetComponent<AgentMover>();
                    if (mover != null)
                        mover.ResetPathProgress();
                }
            }
        }
        else
        {
            // Process collision-free pathfinding agents.
            CollisionFreePathfinding2D[] collisionAgents = FindObjectsOfType<CollisionFreePathfinding2D>();
            foreach (CollisionFreePathfinding2D agent in collisionAgents)
            {
                if (agent != null)
                {
                    agent.FindPath(usePenalty, penaltyIncrement, expandPenalty, neighborPenaltyIncrement);
                    
                    // Reset the movement progress 
                    AgentMover mover = agent.GetComponent<AgentMover>();
                    if (mover != null)
                        mover.ResetPathProgress();
                }
            }
        }
    }

    void StartMovementForAll()
    {
        // Enable movement on all agents with an AgentMover component.
        AgentMover[] movers = FindObjectsOfType<AgentMover>();
        foreach (AgentMover mover in movers)
        {
            mover.StartMovement();
        }
    }
    
    void ResetPositionsForAll()
    {
        // Reset positions for all agents with an AgentMover component.
        AgentMover[] movers = FindObjectsOfType<AgentMover>();
        foreach (AgentMover mover in movers)
        {
            mover.ResetPosition();
        }
    }
}
