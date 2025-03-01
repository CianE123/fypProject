using System.Collections.Generic;
using UnityEngine;

public class CollisionFreePathfinding2D : MonoBehaviour
{
    public Transform target; // Target for this agent
    public GameObject GridOwner; // Reference to the Grid2D object

    private Grid2D grid;

    // Helper class for spatiotemporal nodes.
    private class TemporalNode
    {
        public Node2D node;  // Spatial node reference
        public int time;     // Time step when the node is reached
        public int gCost;    // Cost from start to this node
        public int hCost;    // Heuristic cost from this node to target
        public int fCost { get { return gCost + hCost; } } // Total cost
        public TemporalNode parent; // Reference to previous node for path retracing

        public TemporalNode(Node2D node, int time)
        {
            this.node = node;
            this.time = time;
        }
    }

    void Start()
    {
        grid = GridOwner.GetComponent<Grid2D>();
    }

    /// Main entry point for finding a collision-free path using a spatiotemporal A* search.
    public void FindCollisionFreePath()
    {
        if (target == null)
        {
            Debug.LogWarning($"{gameObject.name} does not have a target assigned.");
            return;
        }

        Node2D startNode = grid.NodeFromWorldPoint(transform.position);
        Node2D targetNode = grid.NodeFromWorldPoint(target.position);

        // Initialize the open list and closed set
        List<TemporalNode> openList = new List<TemporalNode>();
        HashSet<(Node2D, int)> closedSet = new HashSet<(Node2D, int)>();

        TemporalNode startTemporal = new TemporalNode(startNode, 0);
        openList.Add(startTemporal);

        TemporalNode endTemporal = RunSearch(openList, closedSet, targetNode);

        if (endTemporal != null)
        {
            List<Node2D> path = RetracePath(startTemporal, endTemporal);
            grid.SetCollisionFreePath(transform, path);
        }
        else
        {
            Debug.LogWarning($"{gameObject.name} could not find a collision-free path.");
        }
    }

    // Runs the main A* search loop using spatiotemporal nodes.
    private TemporalNode RunSearch(List<TemporalNode> openList, HashSet<(Node2D, int)> closedSet, Node2D targetNode)
    {
        while (openList.Count > 0)
        {
            // Retrieve and remove the node with the lowest fCost
            TemporalNode current = GetLowestCostNode(openList);
            openList.Remove(current);
            closedSet.Add((current.node, current.time));

            // If we have reached the target node, return the current temporal node
            if (current.node == targetNode)
                return current;

            // Expand current node's neighbors.]
            ExpandNeighbors(current, openList, closedSet, targetNode);
        }
        return null;
    }

    // Retrieves the node with the lowest fCost from the list
    private TemporalNode GetLowestCostNode(List<TemporalNode> openList)
    {
        TemporalNode lowest = openList[0];
        foreach (TemporalNode node in openList)
        {
            if (node.fCost < lowest.fCost || (node.fCost == lowest.fCost && node.hCost < lowest.hCost))
                lowest = node;
        }
        return lowest;
    }

    // Expands all valid neighbors of the current node and adds them to the open list
    private void ExpandNeighbors(TemporalNode current, List<TemporalNode> openList, HashSet<(Node2D, int)> closedSet, Node2D targetNode)
    {
        foreach (Node2D neighbor in grid.GetNeighbors(current.node))
        {
            if (neighbor.obstacle)
                continue;

            int nextTime = current.time + 1;

            // Skip if the neighbor is reserved or causes a pass-through collision
            if (IsReserved(neighbor, nextTime) || IsPassThroughCollision(current.node, neighbor, current.time, nextTime))
                continue;

            int newCost = current.gCost + GetDistance(current.node, neighbor);
            TemporalNode neighborTemporal = new TemporalNode(neighbor, nextTime)
            {
                gCost = newCost,
                hCost = GetDistance(neighbor, targetNode),
                parent = current
            };

            if (closedSet.Contains((neighbor, nextTime)))
                continue;

            // If a similar node is already in the open list with a lower cost, skip adding this one
            bool skip = false;
            foreach (TemporalNode openNode in openList)
            {
                if (openNode.node == neighbor && openNode.time == nextTime && openNode.gCost <= newCost)
                {
                    skip = true;
                    break;
                }
            }
            if (!skip)
                openList.Add(neighborTemporal);
        }
    }

    // Retraces the path from the end node back to the start node
    private List<Node2D> RetracePath(TemporalNode start, TemporalNode end)
    {
        List<Node2D> path = new List<Node2D>();
        TemporalNode current = end;
        while (current != null && current != start)
        {
            path.Add(current.node);
            current = current.parent;
        }
        path.Reverse();
        return path;
    }

    // Returns the movement cost between two nodes
    private int GetDistance(Node2D nodeA, Node2D nodeB)
    {
        int dstX = Mathf.Abs(nodeA.GridX - nodeB.GridX);
        int dstY = Mathf.Abs(nodeA.GridY - nodeB.GridY);
        return (dstX > dstY) ? 14 * dstY + 10 * (dstX - dstY) : 14 * dstX + 10 * (dstY - dstX);
    }

    // Checks if any collision-free path reserves the given node at a specific time
    private bool IsReserved(Node2D node, int time)
    {
        foreach (var kvp in grid.collisionFreePaths)
        {
            List<Node2D> path = kvp.Value;
            if (time < path.Count && path[time] == node)
                return true;
        }
        return false;
    }


    // Checks for pass-through collisions where two agents might swap positions
    private bool IsPassThroughCollision(Node2D current, Node2D neighbor, int currentTime, int nextTime)
    {
        foreach (var kvp in grid.collisionFreePaths)
        {
            List<Node2D> path = kvp.Value;
            if (currentTime > 0 && nextTime < path.Count)
            {
                if (path[currentTime] == neighbor && path[nextTime] == current)
                    return true;
            }
        }
        return false;
    }
}
