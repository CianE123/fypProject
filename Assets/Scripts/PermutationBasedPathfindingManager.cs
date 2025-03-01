using System.Collections.Generic;
using UnityEngine;

public class PermutationBasedPathfindingManager : MonoBehaviour
{
    public Grid2D grid; // Assign your Grid2D instance in the inspector
    private Pathfinding2D[] agents;
    public int maxLowLevelExpansions = 10000; // max for low-level search

    void Start()
    {
        // Find all GameObjects tagged as "Agent" and get their Pathfinding2D component.
        GameObject[] agentObjects = GameObject.FindGameObjectsWithTag("Agent");
        List<Pathfinding2D> agentList = new List<Pathfinding2D>();

        foreach (GameObject go in agentObjects)
        {
            Pathfinding2D agentComponent = go.GetComponent<Pathfinding2D>();
            if (agentComponent != null)
            {
                agentList.Add(agentComponent);
            }
        }
        agents = agentList.ToArray();
    }

    void Update()
    {
        // Press 'P' to run the permutation-based planning.
        if (Input.GetKeyDown(KeyCode.P))
        {
            ComputeBestSolution();
        }
    }

    // Tries every ordering (permutation) of agents and selects the lowest total cost solution.
    private void ComputeBestSolution()
    {
        List<int> agentIndices = new List<int>();
        for (int i = 0; i < agents.Length; i++)
            agentIndices.Add(i);

        List<List<int>> allPermutations = GeneratePermutations(agentIndices);
        float bestCost = float.MaxValue;
        Dictionary<int, List<Node2D>> bestSolution = null;
        List<int> bestOrder = null;

        // Try each permutation.
        foreach (var order in allPermutations)
        {
            // Create a fresh reservation table for this ordering.
            ReservationTable reservation = new ReservationTable();
            Dictionary<int, List<Node2D>> solutionPaths = new Dictionary<int, List<Node2D>>();
            bool validOrdering = true;
            float totalCost = 0f;

            // For each agent in the ordering, plan a path while avoiding conflicts.
            foreach (int agentIndex in order)
            {
                Node2D startNode = grid.NodeFromWorldPoint(agents[agentIndex].transform.position);
                Node2D goalNode = grid.NodeFromWorldPoint(agents[agentIndex].target.position);
                // The low-level search returns the full path (starting cell included)
                List<Node2D> fullPath = PlanPathForAgent(agentIndex, startNode, goalNode, reservation);
                if (fullPath == null)
                {
                    validOrdering = false;
                    break; // Discard this ordering if one agent fails to find a path.
                }
                // Reserve the entire full path.
                reservation.ReservePath(fullPath);
                // For display, remove the starting cell so the path begins at the first move.
                List<Node2D> displayPath = new List<Node2D>(fullPath);
                if (displayPath.Count > 0)
                    displayPath.RemoveAt(0);
                solutionPaths[agentIndex] = displayPath;
                totalCost += fullPath.Count;
            }

            if (validOrdering && totalCost < bestCost)
            {
                bestCost = totalCost;
                bestSolution = solutionPaths;
                bestOrder = order;
            }
        }

        if (bestSolution != null)
        {   
            // Set the active solution type to CollisionFree (or Standard, depending on your visual setup).
            grid.currentSolutionType = Grid2D.PathSolutionType.CollisionFree;
    
            // Assign the best solution to the agents.
            foreach (var kvp in bestSolution)
            {
                int agentIndex = kvp.Key;
                grid.SetCollisionFreePath(agents[agentIndex].transform, kvp.Value);
            }
            Debug.Log("Best ordering: " + string.Join(", ", bestOrder) + " with total cost: " + bestCost);
        }
        else
        {
            Debug.LogWarning("No valid solution found for any ordering.");
        }
    }

    // Generate all permutations of a list of integers.
    private List<List<int>> GeneratePermutations(List<int> list)
    {
        List<List<int>> result = new List<List<int>>();
        Permute(list, 0, result);
        return result;
    }

    private void Permute(List<int> list, int start, List<List<int>> result)
    {
        if (start >= list.Count)
        {
            result.Add(new List<int>(list));
        }
        else
        {
            for (int i = start; i < list.Count; i++)
            {
                Swap(list, start, i);
                Permute(list, start + 1, result);
                Swap(list, start, i); // backtrack
            }
        }
    }

    private void Swap(List<int> list, int i, int j)
    {
        int temp = list[i];
        list[i] = list[j];
        list[j] = temp;
    }

    // Low-level A* search that avoids reserved nodes and edge transitions.
    // Returns the full path (starting cell included) to allow proper reservation.
    private List<Node2D> PlanPathForAgent(int agentID, Node2D start, Node2D goal, ReservationTable reservation)
    {
        List<TemporalNode> openList = new List<TemporalNode>();
        HashSet<(Node2D, int)> closedSet = new HashSet<(Node2D, int)>();
        int expansions = 0;

        TemporalNode startNode = new TemporalNode(start, 0);
        openList.Add(startNode);

        while (openList.Count > 0)
        {
            expansions++;
            if(expansions > maxLowLevelExpansions)
            {
                Debug.LogWarning("Low-level search exceeded expansion limit for agent " + agentID);
                return null;
            }

            // Sort by fCost.
            openList.Sort((a, b) => a.fCost.CompareTo(b.fCost));
            TemporalNode current = openList[0];
            openList.RemoveAt(0);

            if(current.node == goal)
            {
                // Retrace the full path.
                return RetracePath(current);
            }

            closedSet.Add((current.node, current.time));

            foreach (Node2D neighbor in grid.GetNeighbors(current.node))
            {
                if(neighbor.obstacle) continue;

                int nextTime = current.time + 1;

                // Check reservations normally; even if neighbor is the goal, we want to disallow same-time arrival.
                if (reservation.IsVertexReserved(neighbor, nextTime))
                    continue;

                // Check for pass-through (edge) collisions.
                if (reservation.IsEdgeReserved(current.node, neighbor, nextTime))
                    continue;

                if (closedSet.Contains((neighbor, nextTime)))
                    continue;

                int newCost = current.gCost + GetDistance(current.node, neighbor);
                TemporalNode neighborNode = new TemporalNode(neighbor, nextTime)
                {
                    gCost = newCost,
                    hCost = GetDistance(neighbor, goal),
                    parent = current
                };
                openList.Add(neighborNode);
            }
        }
        return null;
    }

    // Retrace the full path from the temporal node to the start.
    private List<Node2D> RetracePath(TemporalNode node)
    {
        List<Node2D> path = new List<Node2D>();
        while(node != null)
        {
            path.Add(node.node);
            node = node.parent;
        }
        path.Reverse();
        return path;
    }

    // Manhattan-like heuristic.
    private int GetDistance(Node2D a, Node2D b)
    {
        int dstX = Mathf.Abs(a.GridX - b.GridX);
        int dstY = Mathf.Abs(a.GridY - b.GridY);
        return (dstX > dstY) ? 14 * dstY + 10 * (dstX - dstY) : 14 * dstX + 10 * (dstY - dstX);
    }

    // Temporal node for A* search.
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

    // Reservation table stores vertex and edge reservations per time step.
    // Vertex reservations now track whether a reservation is for a goal cell.
    private class ReservationTable
    {
        // Maps time step to a list of reserved nodes along with a flag indicating if it is a goal reservation.
        private Dictionary<int, List<(Node2D node, bool isGoal)>> vertexReservations = new Dictionary<int, List<(Node2D, bool)>>();
        // Maps time step to a list of reserved edges (from, to).
        private Dictionary<int, List<(Node2D, Node2D)>> edgeReservations = new Dictionary<int, List<(Node2D, Node2D)>>();

        // Returns true if the given node is reserved at the specified time.
        public bool IsVertexReserved(Node2D node, int time)
        {
            if(vertexReservations.ContainsKey(time))
            {
                foreach (var reservation in vertexReservations[time])
                {
                    if (reservation.node == node)
                        return true;
                }
            }
            return false;
        }

        // Check for an edge conflict (pass-through): if an agent is moving from A to B at a given time,
        // another agent’s reserved move from B to A at the same time indicates a conflict.
        public bool IsEdgeReserved(Node2D from, Node2D to, int time)
        {
            if(edgeReservations.ContainsKey(time))
            {
                foreach(var edge in edgeReservations[time])
                {
                    if(edge.Item1 == to && edge.Item2 == from)
                        return true;
                }
            }
            return false;
        }

        // Reserve the full path (with starting cell) for vertex and edge reservations
        // For the goal cell (last cell in the path), mark the reservation as a goal reservation
        public void ReservePath(List<Node2D> path)
        {
            // Reserve each node along the path
            for (int t = 0; t < path.Count; t++)
            {
                Node2D node = path[t];
                if(!vertexReservations.ContainsKey(t))
                    vertexReservations[t] = new List<(Node2D, bool)>();
                // Mark the final cell as a goal reservation
                bool isGoal = (t == path.Count - 1);
                vertexReservations[t].Add((node, isGoal));
            }
            // Reserve edges for moves
            for (int t = 1; t < path.Count; t++)
            {
                Node2D from = path[t - 1];
                Node2D to = path[t];
                if(!edgeReservations.ContainsKey(t))
                    edgeReservations[t] = new List<(Node2D, Node2D)>();
                edgeReservations[t].Add((from, to));
            }
        }
    }
}
