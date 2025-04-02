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
                    // would conflict. Check for (to -> from) at the same time step 't'.
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

        // Clears all reservations. Called when starting a new round of pathfinding for all agents.
        public void ClearReservations()
        {
            vertexReservations.Clear();
            edgeReservations.Clear();
        }
    }

    void Start()
    {
        grid = GridOwner.GetComponent<Grid2D>();
    }

    // Finds a collision–free path using a temporal A* that checks a shared reservation table.
    // Penalties (if enabled) are added along the found path also accepts temporal penalty parameters.
    // Added useWaitAction parameter.
    public void FindPath(bool usePenalty, int penaltyIncrement, bool expandPenalty, int neighborPenaltyIncrement, bool useTemporalPenalty, int maxTemporalDifference, bool useWaitAction)
    {
        if (target == null)
        {
            Debug.LogWarning($"{gameObject.name} does not have a target assigned.");
            return;
        }
        if (grid == null)
        {
             grid = GridOwner.GetComponent<Grid2D>();
             if (grid == null)
             {
                Debug.LogError($"{gameObject.name}: Grid2D component not found on GridOwner.");
                return;
             }
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
            // Optimization: Consider using a Min-Heap/Priority Queue for better performance than sorting.
            openList.Sort((a, b) => {
                int fCostComparison = a.fCost.CompareTo(b.fCost);
                if (fCostComparison == 0)
                {
                    // Tie-breaking: prefer nodes with lower hCost (closer to goal)
                    return a.hCost.CompareTo(b.hCost);
                }
                return fCostComparison;
            });
            TemporalNode current = openList[0];
            openList.RemoveAt(0);

            // Avoid re-expanding nodes already processed.
            if (closedSet.Contains((current.node, current.time)))
            {
                continue;
            }
            closedSet.Add((current.node, current.time));


            // If we've reached the goal, finish.
            // Check if current node is goal AND not reserved by another agent ending there
            if (current.node == goalNode)
            {
                endTemporal = current;
                break;
            }

            int nextTime = current.time + 1;

            // Wait Action Logic
            if (useWaitAction)
            {
                Node2D waitNode = current.node; // Stay in the current node

                // Check vertex reservation for waiting in place.
                if (!reservationTable.IsVertexReserved(waitNode, nextTime))
                {
                    // Calculate cost for waiting.
                    int additionalPenalty = 0;
                    if (usePenalty)
                    {
                        if (useTemporalPenalty)
                            additionalPenalty = grid.GetTemporalPenalty(waitNode, nextTime, maxTemporalDifference);
                        else
                            additionalPenalty = grid.GetPenalty(waitNode);
                    }

                    // Cost of waiting is 1 step (like a cardinal move) + penalty.
                    int waitMoveCost = 10; // Cost equivalent to moving one non-diagonal step.
                    int newWaitCost = current.gCost + waitMoveCost + additionalPenalty;

                    TemporalNode waitTemporal = new TemporalNode(waitNode, nextTime)
                    {
                        gCost = newWaitCost,
                        hCost = GetDistance(waitNode, goalNode), // Heuristic remains the same.
                        parent = current
                    };

                    // Check if already closed or a better path exists in open list.
                    if (!closedSet.Contains((waitNode, nextTime)))
                    {
                        bool skip = false;
                        // Check open list for a better or equal cost path to the same state.
                        for (int i = openList.Count - 1; i >= 0; i--) // Iterate backwards for safe removal
                        {
                            TemporalNode openNode = openList[i];
                            if (openNode.node == waitNode && openNode.time == nextTime)
                            {
                                if (openNode.gCost <= newWaitCost)
                                {
                                    skip = true; // Found a better or equal path already
                                    break;
                                }
                                else
                                {
                                    // Found a worse path, remove it to replace with the better one
                                    openList.RemoveAt(i);
                                }
                            }
                        }
                        if (!skip)
                            openList.Add(waitTemporal);
                    }
                }
            }

            // Expand neighbors (Move Actions).
            foreach (Node2D neighbor in grid.GetNeighbors(current.node))
            {
                if (neighbor.obstacle)
                    continue;

                // Check reservations for vertex and edge conflicts for MOVING to the neighbor.
                if (reservationTable.IsVertexReserved(neighbor, nextTime) ||
                    reservationTable.IsEdgeReserved(current.node, neighbor, nextTime))
                    continue;

                // Compute the additional penalty for moving to the neighbor.
                int additionalPenalty = 0;
                if (usePenalty)
                {
                    if (useTemporalPenalty)
                        additionalPenalty = grid.GetTemporalPenalty(neighbor, nextTime, maxTemporalDifference);
                    else
                        additionalPenalty = grid.GetPenalty(neighbor);
                }

                int moveCost = GetDistance(current.node, neighbor); // Cost of the move itself
                int newMoveCost = current.gCost + moveCost + additionalPenalty;

                // Check if already closed. If so, we don't need to consider it again.
                if (closedSet.Contains((neighbor, nextTime)))
                    continue;


                // --- Check Open List ---
                TemporalNode existingOpenNode = null;
                // Find if a node for this state (neighbor, nextTime) already exists in the open list
                foreach (TemporalNode openNode in openList)
                {
                    if (openNode.node == neighbor && openNode.time == nextTime)
                    {
                        existingOpenNode = openNode;
                        break; // Found it
                    }
                }

                // Case 1: State (neighbor, nextTime) is already in the Open List
                if (existingOpenNode != null)
                {
                    // Check if the new path TO this state is better (lower gCost)
                    if (newMoveCost < existingOpenNode.gCost)
                    {
                        // Update the existing node with the better path info
                        existingOpenNode.gCost = newMoveCost;
                        existingOpenNode.parent = current;
                        // (If using a priority queue, you'd update its position here)
                    }
                    // else: The existing path is better or equal, so we do nothing and 'continue' to the next neighbor.
                    continue; // Ensures we don't add a duplicate node below
                }

                // Case 2: State (neighbor, nextTime) is NOT in the Open List (and not in Closed)
                // Create a new temporal node for this valid, unvisited state
                TemporalNode neighborTemporal = new TemporalNode(neighbor, nextTime)
                {
                    gCost = newMoveCost,
                    hCost = GetDistance(neighbor, goalNode),
                    parent = current
                };
                openList.Add(neighborTemporal); // Add the new node to the open list

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
                displayPath.RemoveAt(0); // Keep the start node for reservation but not display/movement? Check AgentMover logic. Usually, the path for mover includes the start node. Let's keep it consistent for now. Maybe AgentMover handles the first node.
            grid.SetCollisionFreePath(transform, path); // Store the full path including start node
            grid.currentSolutionType = Grid2D.PathSolutionType.CollisionFree;
        }
        else
        {
            Debug.LogWarning($"{gameObject.name} could not find a collision–free path.");
             // Optionally clear the path for this agent if none was found
            grid.SetCollisionFreePath(transform, new List<Node2D>());
        }
    }

    // Retraces the path from end to start.
    private List<Node2D> RetracePath(TemporalNode start, TemporalNode end)
    {
        List<Node2D> path = new List<Node2D>();
        TemporalNode current = end;

        while (current != null) // Changed condition from current != start to current != null to include start node
        {
            path.Add(current.node);
            current = current.parent;
        }
        path.Reverse();
        return path;
    }

    // Manhattan distance heuristic (Adjusted for diagonal movement)
    private int GetDistance(Node2D a, Node2D b)
    {
        int dstX = Mathf.Abs(a.GridX - b.GridX);
        int dstY = Mathf.Abs(a.GridY - b.GridY);
        // Using standard diagonal distance costs (14 for diagonal, 10 for cardinal)
        return (dstX > dstY) ? 14 * dstY + 10 * (dstX - dstY) : 14 * dstX + 10 * (dstY - dstX);
    }

    // Static method to reset the shared reservation table.
    public static void ResetReservationTable()
    {
        // Ensure the instance exists before clearing.
        if (reservationTable == null)
        {
            reservationTable = new ReservationTable();
        }
        else
        {
            reservationTable.ClearReservations(); // Use the new clear method
        }
    }
}