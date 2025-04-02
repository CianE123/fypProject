using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

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
    //Toggle for temporal penalty option
    public bool useTemporalPenalty = false;
    // Maximum allowed difference between the recorded penalty time and the current agent step
    public int maxTemporalDifference = 5;
    // Toggle for collision-free pathfinding
    public bool collisionFree = false;
    // Toggle to allow agents to wait in place in collision-free mode
    public bool useWaitAction = false; // Added toggle, default to true
    // Toggle to reverse the scheduling order (largest distance first if true)
    public bool reverseOrder = false;

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
        else
        {
            Debug.LogError("PathfindingManager: Grid2D not found in the scene!");
            return;
        }

        // For both standard and collision-free cases, we sort agents based on their distance from target.
        if (!collisionFree)
        {
            // Process standard pathfinding agents.
            Pathfinding2D[] pathfinders = FindObjectsOfType<Pathfinding2D>();

            // Sort agents based on Manhattan distance from their current position to target.
            var sortedAgents = reverseOrder ?
                pathfinders.OrderByDescending(agent =>
                {
                    if (agent.target == null) return 0; // Handle null target case
                    Node2D startNode = grid.NodeFromWorldPoint(agent.transform.position);
                    Node2D goalNode = grid.NodeFromWorldPoint(agent.target.position);
                    return GetManhattanDistance(startNode, goalNode);
                }).ToArray() :
                pathfinders.OrderBy(agent =>
                {
                     if (agent.target == null) return int.MaxValue; // Handle null target case
                    Node2D startNode = grid.NodeFromWorldPoint(agent.transform.position);
                    Node2D goalNode = grid.NodeFromWorldPoint(agent.target.position);
                    return GetManhattanDistance(startNode, goalNode);
                }).ToArray();

            // Process agents in sorted order.
            foreach (Pathfinding2D agent in sortedAgents)
            {
                if (agent != null && agent.target != null) // Check target exists
                {
                    // Note: Standard pathfinding doesn't use the wait action toggle
                    agent.FindPath(usePenalty, penaltyIncrement, expandPenalty, neighborPenaltyIncrement, useTemporalPenalty, maxTemporalDifference);

                    // Reset the movement progress (if an AgentMover is attached).
                    AgentMover mover = agent.GetComponent<AgentMover>();
                    if (mover != null)
                        mover.ResetPathProgress();
                }
            }
        }
        else // Collision-Free Pathfinding
        {
            // Reset reservations before processing any agent
            CollisionFreePathfinding2D.ResetReservationTable();
            // Process collision-free pathfinding agents.
            CollisionFreePathfinding2D[] collisionAgents = FindObjectsOfType<CollisionFreePathfinding2D>();

            // Sort collision-free agents based on Manhattan distance.
             var sortedAgents = reverseOrder ?
                collisionAgents.OrderByDescending(agent =>
                {
                    if (agent.target == null) return 0; // Handle null target case
                    Node2D startNode = grid.NodeFromWorldPoint(agent.transform.position);
                    Node2D goalNode = grid.NodeFromWorldPoint(agent.target.position);
                    return GetManhattanDistance(startNode, goalNode);
                }).ToArray() :
                collisionAgents.OrderBy(agent =>
                {
                     if (agent.target == null) return int.MaxValue; // Handle null target case
                    Node2D startNode = grid.NodeFromWorldPoint(agent.transform.position);
                    Node2D goalNode = grid.NodeFromWorldPoint(agent.target.position);
                    return GetManhattanDistance(startNode, goalNode);
                }).ToArray();

            // Process agents in sorted order.
            foreach (CollisionFreePathfinding2D agent in sortedAgents)
            {
                if (agent != null && agent.target != null) // Check target exists
                {
                    // Pass the useWaitAction toggle here
                    agent.FindPath(usePenalty, penaltyIncrement, expandPenalty, neighborPenaltyIncrement, useTemporalPenalty, maxTemporalDifference, useWaitAction);

                    // Reset the movement progress.
                    AgentMover mover = agent.GetComponent<AgentMover>();
                    if (mover != null)
                        mover.ResetPathProgress();
                }
            }
        }
    }

    // Helper method for Manhattan distance calculation (using diagonal costs).
    private int GetManhattanDistance(Node2D nodeA, Node2D nodeB)
    {
        int dstX = Mathf.Abs(nodeA.GridX - nodeB.GridX);
        int dstY = Mathf.Abs(nodeA.GridY - nodeB.GridY);
        // Consistent with the heuristic used in pathfinding
        return (dstX > dstY) ? 14 * dstY + 10 * (dstX - dstY) : 14 * dstX + 10 * (dstY - dstX);
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
        // Also clear any existing drawn paths and reservations if needed
         Grid2D grid = FindObjectOfType<Grid2D>();
        if (grid != null)
        {
             grid.paths.Clear();
             grid.collisionFreePaths.Clear();
             grid.ResetPenaltyGrid(); // Reset penalties too
        }
        CollisionFreePathfinding2D.ResetReservationTable(); // Reset reservations
    }
}