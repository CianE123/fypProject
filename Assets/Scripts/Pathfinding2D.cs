using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class Pathfinding2D : MonoBehaviour
{
    public Transform target; // The target Transform this agent is seeking
    private Grid2D grid;
    private Node2D seekerNode, targetNode;
    public GameObject GridOwner;

    void Start()
    {
        grid = GridOwner.GetComponent<Grid2D>();
    }

    // New signature with a usePenalty parameter
    public void FindPath(bool usePenalty, int penaltyIncrement, bool expandPenalty, int neighborPenaltyIncrement, bool useTemporalPenalty, int maxTemporalDifference)
    {
        if (target == null)
        {
            Debug.LogWarning($"{gameObject.name} does not have a target assigned.");
            return;
        }

        // Get the seeker and target positions in grid coordinates
        seekerNode = grid.NodeFromWorldPoint(transform.position);
        targetNode = grid.NodeFromWorldPoint(target.position);

        // Initialize starting cost
        seekerNode.gCost = 0;
        seekerNode.hCost = GetDistance(seekerNode, targetNode);

        List<Node2D> openSet = new List<Node2D>();
        HashSet<Node2D> closedSet = new HashSet<Node2D>();
        Dictionary<Node2D, int> nodeStep = new Dictionary<Node2D, int>();

        openSet.Add(seekerNode);
        nodeStep[seekerNode] = 0;

        while (openSet.Count > 0)
        {
            Node2D node = openSet[0];
            for (int i = 1; i < openSet.Count; i++)
            {
                if (openSet[i].FCost < node.FCost || (openSet[i].FCost == node.FCost && openSet[i].hCost < node.hCost))
                {
                    node = openSet[i];
                }
            }

            openSet.Remove(node);
            closedSet.Add(node);

            if (node == targetNode)
            {
                RetracePath(seekerNode, targetNode, usePenalty, penaltyIncrement, expandPenalty, neighborPenaltyIncrement);
                return;
            }

            int currentStep = nodeStep[node];

            foreach (Node2D neighbour in grid.GetNeighbors(node))
            {
                if (neighbour.obstacle || closedSet.Contains(neighbour))
                    continue;

                // Base movement cost
                int baseCost = GetDistance(node, neighbour);
                int nextStep = currentStep + 1;
                int additionalPenalty = 0;

                if (usePenalty)
                {
                    // If temporal penalty is enabled, use the neighbor's step to check the penalty timing.
                    if (useTemporalPenalty)
                        additionalPenalty = grid.GetTemporalPenalty(neighbour, nextStep, maxTemporalDifference);
                    else
                        additionalPenalty = grid.GetPenalty(neighbour);
                }

                int newCostToNeighbour = node.gCost + baseCost + additionalPenalty;

                if (newCostToNeighbour < neighbour.gCost || !openSet.Contains(neighbour))
                {
                    neighbour.gCost = newCostToNeighbour;
                    neighbour.hCost = GetDistance(neighbour, targetNode);
                    neighbour.parent = node;

                    if (!openSet.Contains(neighbour))
                    {
                        openSet.Add(neighbour);
                        nodeStep[neighbour] = nextStep;
                    }
                }
            }
        }
    }

    void RetracePath(Node2D startNode, Node2D endNode, bool usePenalty, int penaltyIncrement,  bool expandPenalty, int neighborPenaltyIncrement)
    {
        List<Node2D> path = new List<Node2D>();
        Node2D currentNode = endNode;

        while (currentNode != startNode)
        {
            path.Add(currentNode);
            currentNode = currentNode.parent;
        }
        path.Reverse();

        // Update the penalty grid along the path if using penalty 
        if (usePenalty)
        {
            grid.AddPenaltyForPath(path, penaltyIncrement, expandPenalty, neighborPenaltyIncrement);
        }
        // Store the path in the Grid2D manager for this seeker.
        grid.SetStandardPath(transform, path);
        grid.currentSolutionType = Grid2D.PathSolutionType.Standard;
    }

    public int GetDistance(Node2D nodeA, Node2D nodeB)
    {
        int dstX = Mathf.Abs(nodeA.GridX - nodeB.GridX);
        int dstY = Mathf.Abs(nodeA.GridY - nodeB.GridY);
        return (dstX > dstY) ? 14 * dstY + 10 * (dstX - dstY) : 14 * dstX + 10 * (dstY - dstX);
    }
}
