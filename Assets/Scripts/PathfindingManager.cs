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
    // Toggle for collision-free pathfinding
    public bool collisionFree = false;
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

        // For both standard and collision-free cases, we sort agents based on their distance from target.
        if (!collisionFree)
        {
            // Process standard pathfinding agents.
            Pathfinding2D[] pathfinders = FindObjectsOfType<Pathfinding2D>();

            // Sort agents based on Manhattan distance from their current position to target.
            var sortedAgents = reverseOrder ? 
                pathfinders.OrderByDescending(agent =>
                {
                    Node2D startNode = grid.NodeFromWorldPoint(agent.transform.position);
                    Node2D goalNode = grid.NodeFromWorldPoint(agent.target.position);
                    return GetManhattanDistance(startNode, goalNode);
                }).ToArray() :
                pathfinders.OrderBy(agent =>
                {
                    Node2D startNode = grid.NodeFromWorldPoint(agent.transform.position);
                    Node2D goalNode = grid.NodeFromWorldPoint(agent.target.position);
                    return GetManhattanDistance(startNode, goalNode);
                }).ToArray();

            // Process agents in sorted order.
            foreach (Pathfinding2D agent in sortedAgents)
            {
                if (agent != null)
                {
                    agent.FindPath(usePenalty, penaltyIncrement, expandPenalty, neighborPenaltyIncrement);

                    // Reset the movement progress (if an AgentMover is attached).
                    AgentMover mover = agent.GetComponent<AgentMover>();
                    if (mover != null)
                        mover.ResetPathProgress();
                }
            }
        }
        else
        {
            // Reset reservations 
            CollisionFreePathfinding2D.ResetReservationTable(); 
            // Process collision-free pathfinding agents.
            CollisionFreePathfinding2D[] collisionAgents = FindObjectsOfType<CollisionFreePathfinding2D>();

            // Sort collision-free agents based on Manhattan distance.
            var sortedAgents = reverseOrder ?
                collisionAgents.OrderByDescending(agent =>
                {
                    Node2D startNode = grid.NodeFromWorldPoint(agent.transform.position);
                    Node2D goalNode = grid.NodeFromWorldPoint(agent.target.position);
                    return GetManhattanDistance(startNode, goalNode);
                }).ToArray() :
                collisionAgents.OrderBy(agent =>
                {
                    Node2D startNode = grid.NodeFromWorldPoint(agent.transform.position);
                    Node2D goalNode = grid.NodeFromWorldPoint(agent.target.position);
                    return GetManhattanDistance(startNode, goalNode);
                }).ToArray();

            // Process agents in sorted order.
            foreach (CollisionFreePathfinding2D agent in sortedAgents)
            {
                if (agent != null)
                {
                    agent.FindPath(usePenalty, penaltyIncrement, expandPenalty, neighborPenaltyIncrement);

                    // Reset the movement progress.
                    AgentMover mover = agent.GetComponent<AgentMover>();
                    if (mover != null)
                        mover.ResetPathProgress();
                }
            }
        }
    }

    // Helper method for Manhattan distance calculation.
    private int GetManhattanDistance(Node2D nodeA, Node2D nodeB)
    {
        int dstX = Mathf.Abs(nodeA.GridX - nodeB.GridX);
        int dstY = Mathf.Abs(nodeA.GridY - nodeB.GridY);
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
    }
}
