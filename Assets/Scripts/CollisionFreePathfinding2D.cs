using UnityEngine;
using System.Collections;
using System.Collections.Generic;


public class CollisionFreePathfinding2D : MonoBehaviour
{
    public Transform target;          // Target for this agent.
    public GameObject GridOwner;        // Reference to the Grid2D object.
    public int maxLowLevelExpansions = 10000;  // Safety limit on A* expansions.

    private Grid2D grid;

    // A shared reservation table to record planned paths.
    private static ReservationTable reservationTable = new ReservationTable();

    // Temporal node holds spatiotemporal information for A*.
    private class TemporalNode
    {
        public Node2D node;
        public int time;
        public int gCost;
        public int hCost;
        public int fCost { get { return gCost + hCost; } }
        public TemporalNode parent;

        public TemporalNode(Node2D node, int time)
        {
            this.node = node;
            this.time = time;
        }
    }

    // The reservation table stores vertex and edge reservations by time step.
    private class ReservationTable
    {
        // For each time, store reserved nodes (with a flag marking if it’s the goal).
        private Dictionary<int, List<(Node2D, bool)>> vertexReservations = new Dictionary<int, List<(Node2D, bool)>>();
        // For each time, store reserved edges (from, to).
        private Dictionary<int, List<(Node2D, Node2D)>> edgeReservations = new Dictionary<int, List<(Node2D, Node2D)>>();

        public bool IsVertexReserved(Node2D node, int time)
        {
            if (vertexReservations.ContainsKey(time))
            {
                foreach (var res in vertexReservations[time])
                {
                    if (res.Item1 == node)
                        return true;
                }
            }
            return false;
        }

        public bool IsEdgeReserved(Node2D from, Node2D to, int time)
        {
            if (edgeReservations.ContainsKey(time))
            {
                foreach (var edge in edgeReservations[time])
                {
                    // A reserved edge from 'from' to 'to' means a move in the opposite direction (swapping)
                    // would conflict.
                    if (edge.Item1 == to && edge.Item2 == from)
                        return true;
                }
            }
            return false;
        }

        public void ReservePath(List<Node2D> path)
        {
            // Reserve each vertex along the path.
            for (int t = 0; t < path.Count; t++)
            {
                Node2D node = path[t];
                if (!vertexReservations.ContainsKey(t))
                    vertexReservations[t] = new List<(Node2D, bool)>();
                // Mark the final cell as a goal reservation.
                bool isGoal = (t == path.Count - 1);
                vertexReservations[t].Add((node, isGoal));
            }
            // Reserve the edges (for pass–through collision checks).
            for (int t = 1; t < path.Count; t++)
            {
                Node2D from = path[t - 1];
                Node2D to = path[t];
                if (!edgeReservations.ContainsKey(t))
                    edgeReservations[t] = new List<(Node2D, Node2D)>();
                edgeReservations[t].Add((from, to));
            }
        }
    }

    void Start()
    {
        grid = GridOwner.GetComponent<Grid2D>();
    }

    // Finds a collision–free path using a temporal A* that checks a shared reservation table.
    // Penalties (if enabled) are added along the found path also accepts temporal penalty parameters.
    public void FindPath(bool usePenalty, int penaltyIncrement, bool expandPenalty, int neighborPenaltyIncrement, bool useTemporalPenalty, int maxTemporalDifference)
    {
        if (target == null)
        {
            Debug.LogWarning($"{gameObject.name} does not have a target assigned.");
            return;
        }

        Node2D startNode = grid.NodeFromWorldPoint(transform.position);
        Node2D goalNode = grid.NodeFromWorldPoint(target.position);

        List<TemporalNode> openList = new List<TemporalNode>();
        HashSet<(Node2D, int)> closedSet = new HashSet<(Node2D, int)>();

        TemporalNode startTemporal = new TemporalNode(startNode, 0)
        {
            gCost = 0,
            hCost = GetDistance(startNode, goalNode)
        };
        openList.Add(startTemporal);

        TemporalNode endTemporal = null;
        int expansions = 0;

        // Main A* search loop.
        while (openList.Count > 0)
        {
            expansions++;
            if (expansions > maxLowLevelExpansions)
            {
                Debug.LogWarning($"{gameObject.name}: Low-level search exceeded expansion limit.");
                break;
            }

            // Get the node with the lowest fCost.
            openList.Sort((a, b) => a.fCost.CompareTo(b.fCost));
            TemporalNode current = openList[0];
            openList.RemoveAt(0);
            closedSet.Add((current.node, current.time));

            // If we've reached the goal, finish.
            if (current.node == goalNode)
            {
                endTemporal = current;
                break;
            }

            // Expand neighbors.
            foreach (Node2D neighbor in grid.GetNeighbors(current.node))
            {
                if (neighbor.obstacle)
                    continue;

                int nextTime = current.time + 1;

                // Check reservations for vertex and edge conflicts.
                if (reservationTable.IsVertexReserved(neighbor, nextTime) ||
                    reservationTable.IsEdgeReserved(current.node, neighbor, nextTime))
                    continue;

                // Compute the additional penalty.
                int additionalPenalty = 0;
                if (usePenalty)
                {
                    if (useTemporalPenalty)
                        additionalPenalty = grid.GetTemporalPenalty(neighbor, nextTime, maxTemporalDifference);
                    else
                        additionalPenalty = grid.GetPenalty(neighbor);
                }

                int newCost = current.gCost + GetDistance(current.node, neighbor) + additionalPenalty;

                TemporalNode neighborTemporal = new TemporalNode(neighbor, nextTime)
                {
                    gCost = newCost,
                    hCost = GetDistance(neighbor, goalNode),
                    parent = current
                };

                if (closedSet.Contains((neighbor, nextTime)))
                    continue;

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

        // If a path was found, retrace it, apply penalties, reserve it, and store it for drawing.
        if (endTemporal != null)
        {
            List<Node2D> path = RetracePath(startTemporal, endTemporal);
            if (usePenalty)
            {
                grid.AddPenaltyForPath(path, penaltyIncrement, expandPenalty, neighborPenaltyIncrement);
            }
            reservationTable.ReservePath(path);

            // For drawing, remove the starting node to match Pathfinding2D.
            List<Node2D> displayPath = new List<Node2D>(path);
            if (displayPath.Count > 0)
                displayPath.RemoveAt(0);
            grid.SetCollisionFreePath(transform, displayPath);
            grid.currentSolutionType = Grid2D.PathSolutionType.CollisionFree;
        }
        else
        {
            Debug.LogWarning($"{gameObject.name} could not find a collision–free path.");
        }
    }

    // Retraces the path from end to start.
    private List<Node2D> RetracePath(TemporalNode start, TemporalNode end)
    {
        List<Node2D> path = new List<Node2D>();
        TemporalNode current = end;
        
        while (current != null)
        {
            path.Add(current.node);
            current = current.parent;
        }
        path.Reverse();
        return path;
    }

    // Manhattan distance 
    private int GetDistance(Node2D a, Node2D b)
    {
        int dstX = Mathf.Abs(a.GridX - b.GridX);
        int dstY = Mathf.Abs(a.GridY - b.GridY);
        return (dstX > dstY) ? 14 * dstY + 10 * (dstX - dstY) : 14 * dstX + 10 * (dstY - dstX);
    }

    public static void ResetReservationTable()
    {
        reservationTable = new ReservationTable();
    }
}