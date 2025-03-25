using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public class Grid2D : MonoBehaviour
{
    public Vector3 gridWorldSize;
    public float nodeRadius;
    public Node2D[,] Grid;
    public Tilemap obstaclemap;
    public int[,] penaltyGrid;
    public int[,] penaltyTimeGrid;

    // Dictionaries for the two solution types.
    public Dictionary<Transform, List<Node2D>> paths = new Dictionary<Transform, List<Node2D>>();
    public Dictionary<Transform, List<Node2D>> collisionFreePaths = new Dictionary<Transform, List<Node2D>>();

    // Enum to choose which solution to display.
    public enum PathSolutionType { Standard, CollisionFree }
    public PathSolutionType currentSolutionType = PathSolutionType.Standard;

    Vector3 worldBottomLeft;
    float nodeDiameter;
    int gridSizeX, gridSizeY;

    // Dictionary to store a random color for each agent.
    private Dictionary<Transform, Color> agentColors = new Dictionary<Transform, Color>();
    // Dictionary to store a random offset for each agent.
    private Dictionary<Transform, Vector3> agentOffsets = new Dictionary<Transform, Vector3>();

    void Awake()
    {
        nodeDiameter = nodeRadius * 2;
        gridSizeX = Mathf.RoundToInt(gridWorldSize.x / nodeDiameter);
        gridSizeY = Mathf.RoundToInt(gridWorldSize.y / nodeDiameter);
        CreateGrid();
    }

    void CreateGrid()
    {
        Grid = new Node2D[gridSizeX, gridSizeY];
        penaltyGrid = new int[gridSizeX, gridSizeY];  // initialize penalty grid
        // Initialize the temporal grid (use -100 to denote “no penalty time recorded”)
        penaltyTimeGrid = new int[gridSizeX, gridSizeY];
        for (int x = 0; x < gridSizeX; x++)
        {
            for (int y = 0; y < gridSizeY; y++)
            {
                penaltyTimeGrid[x, y] = -100;
            }
        }
        worldBottomLeft = transform.position - Vector3.right * gridWorldSize.x / 2 - Vector3.up * gridWorldSize.y / 2;

        for (int x = 0; x < gridSizeX; x++)
        {
            for (int y = 0; y < gridSizeY; y++)
            {
                Vector3 worldPoint = worldBottomLeft + Vector3.right * (x * nodeDiameter + nodeRadius) 
                                                  + Vector3.up * (y * nodeDiameter + nodeRadius);
                Grid[x, y] = new Node2D(false, worldPoint, x, y);
                penaltyGrid[x, y] = 0; // start with no penalty

                if (obstaclemap.HasTile(obstaclemap.WorldToCell(Grid[x, y].worldPosition)))
                    Grid[x, y].SetObstacle(true);
                else
                    Grid[x, y].SetObstacle(false);
            }
        }
    }

    //Get the current penalty for a node
    public int GetPenalty(Node2D node)
    {
        return penaltyGrid[node.GridX, node.GridY];
    }

    //Returns the penalty only if the difference between the current step and the recorded penalty time is within the allowed maxDifference; otherwise returns 0.
    public int GetTemporalPenalty(Node2D node, int currentStep, int maxDifference)
    {
        int recordedTime = penaltyTimeGrid[node.GridX, node.GridY];
        if (currentStep - recordedTime <= maxDifference)
            return penaltyGrid[node.GridX, node.GridY];
        else
            return 0;
    }

    //Add a penalty value to each node along a path
    public void AddPenaltyForPath(List<Node2D> path, int penaltyValue, bool expandPenalty, int neighborPenalty)
    {
        for (int i = 0; i < path.Count; i++)
        {
            Node2D node = path[i];
            // Apply penalty to the path cells 
            penaltyGrid[node.GridX, node.GridY] += penaltyValue;
            // record the time as the index in the path
            penaltyTimeGrid[node.GridX, node.GridY] = i; 

            //Expands the penalty to neighboring cells
            if (expandPenalty)
            {
                // Loop over all adjacent offsets (including diagonals)
                for (int dx = -1; dx <= 1; dx++)
                {
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        // Skip the cell itself.
                        if (dx == 0 && dy == 0)
                            continue;
                        int nx = node.GridX + dx;
                        int ny = node.GridY + dy;
                        // Check bounds using the grid dimensions.
                        if (nx >= 0 && nx < gridSizeX && ny >= 0 && ny < gridSizeY)
                        {
                            penaltyGrid[nx, ny] += neighborPenalty;
                            // Record the same time as the parent cell
                            penaltyTimeGrid[nx, ny] = i;
                        }
                    }
                }
            }
        }
    }

    public void ResetPenaltyGrid()
    {
        for (int x = 0; x < penaltyGrid.GetLength(0); x++)
        {
            for (int y = 0; y < penaltyGrid.GetLength(1); y++)
            {
                penaltyGrid[x, y] = 0;
                penaltyTimeGrid[x, y] = -100;
            }
        }
    }

    public List<Node2D> GetNeighbors(Node2D node)
    {
        List<Node2D> neighbors = new List<Node2D>();

        if (node.GridX >= 0 && node.GridX < gridSizeX && node.GridY + 1 >= 0 && node.GridY + 1 < gridSizeY)
            neighbors.Add(Grid[node.GridX, node.GridY + 1]);

        if (node.GridX >= 0 && node.GridX < gridSizeX && node.GridY - 1 >= 0 && node.GridY - 1 < gridSizeY)
            neighbors.Add(Grid[node.GridX, node.GridY - 1]);

        if (node.GridX + 1 >= 0 && node.GridX + 1 < gridSizeX && node.GridY >= 0 && node.GridY < gridSizeY)
            neighbors.Add(Grid[node.GridX + 1, node.GridY]);

        if (node.GridX - 1 >= 0 && node.GridX - 1 < gridSizeX && node.GridY >= 0 && node.GridY < gridSizeY)
            neighbors.Add(Grid[node.GridX - 1, node.GridY]);

        return neighbors;
    }

    public Node2D NodeFromWorldPoint(Vector3 worldPosition)
    {
        float percentX = (worldPosition.x - worldBottomLeft.x) / gridWorldSize.x;
        float percentY = (worldPosition.y - worldBottomLeft.y) / gridWorldSize.y;
        percentX = Mathf.Clamp01(percentX);
        percentY = Mathf.Clamp01(percentY);
        int x = Mathf.RoundToInt((gridSizeX - 1) * percentX);
        int y = Mathf.RoundToInt((gridSizeY - 1) * percentY);
        return Grid[x, y];
    }

    // Methods to set the respective paths.
    public void SetStandardPath(Transform seeker, List<Node2D> path)
    {
        if (paths.ContainsKey(seeker))
            paths[seeker] = path;
        else
            paths.Add(seeker, path);
    }

    public void SetCollisionFreePath(Transform seeker, List<Node2D> path)
    {
        if (collisionFreePaths.ContainsKey(seeker))
            collisionFreePaths[seeker] = path;
        else
            collisionFreePaths.Add(seeker, path);
    }

    // Detect collisions only within the active solution.
    private List<Node2D> DetectCollisions(Dictionary<Transform, List<Node2D>> activePaths)
    {
        // Tracks arrival times for each node.
        Dictionary<Node2D, Dictionary<int, int>> nodeArrivalTimes = new Dictionary<Node2D, Dictionary<int, int>>();
        List<Node2D> collisionNodes = new List<Node2D>();

        foreach (var pathEntry in activePaths)
        {
            List<Node2D> path = pathEntry.Value;
            for (int i = 0; i < path.Count; i++)
            {
                Node2D currentNode = path[i];
                int arrivalTime = i; // time step corresponds to the index in the path

                if (!nodeArrivalTimes.ContainsKey(currentNode))
                    nodeArrivalTimes[currentNode] = new Dictionary<int, int>();

                if (!nodeArrivalTimes[currentNode].ContainsKey(arrivalTime))
                    nodeArrivalTimes[currentNode][arrivalTime] = 0;

                nodeArrivalTimes[currentNode][arrivalTime]++;

                // Mark collision if more than one agent arrives at the same node at the same time.
                if (nodeArrivalTimes[currentNode][arrivalTime] > 1 && !collisionNodes.Contains(currentNode))
                    collisionNodes.Add(currentNode);

                // Check for pass-through (swapping) collisions.
                if (i > 0)
                {
                    Node2D previousNode = path[i - 1];
                    foreach (var otherEntry in activePaths)
                    {
                        if (otherEntry.Value == path)
                            continue; // Skip self

                        List<Node2D> otherPath = otherEntry.Value;
                        if (arrivalTime > 0 && arrivalTime < otherPath.Count)
                        {
                            Node2D otherCurrent = otherPath[arrivalTime];
                            Node2D otherPrevious = otherPath[arrivalTime - 1];

                            if (otherCurrent == previousNode && otherPrevious == currentNode && !collisionNodes.Contains(currentNode))
                                collisionNodes.Add(currentNode);
                        }
                    }
                }
            }
        }
        return collisionNodes;
    }

    // Returns a random color for the agent; stores it for consistent display.
    private Color GetRandomColorForAgent(Transform agent)
    {
        if (!agentColors.ContainsKey(agent))
        {
            agentColors[agent] = Random.ColorHSV();
        }
        return agentColors[agent];
    }

    // Returns a random offset for the agent (in the XY plane) to offset its path drawing.
    private Vector3 GetRandomOffsetForAgent(Transform agent)
    {
        if (!agentOffsets.ContainsKey(agent))
        {
            // A small random offset (adjust the magnitude as needed)
            Vector2 offset2D = Random.insideUnitCircle * (nodeRadius * 0.3f);
            agentOffsets[agent] = new Vector3(offset2D.x, offset2D.y, 0);
        }
        return agentOffsets[agent];
    }

    void OnDrawGizmos()
    {
        Gizmos.DrawWireCube(transform.position, new Vector3(gridWorldSize.x, gridWorldSize.y, 1));

        if (Grid != null)
        {
            // Choose active paths based on the current solution type.
            Dictionary<Transform, List<Node2D>> activePaths = (currentSolutionType == PathSolutionType.Standard) ? paths : collisionFreePaths;
            List<Node2D> collisionNodes = DetectCollisions(activePaths);

            foreach (Node2D n in Grid)
            {
                // Default color based on obstacle state.
                Gizmos.color = n.obstacle ? Color.red : Color.white;

                // If this node is part of a collision, highlight it.
                if (collisionNodes.Contains(n))
                {
                    Gizmos.color = Color.yellow;
                }
                else
                {
                    // If the node belongs to any path in the active solution, use a designated color.
                    foreach (var path in activePaths.Values)
                    {
                        if (path.Contains(n))
                        {   
                            Gizmos.color = Color.black;
                            break;
                        }
                    }
                }

                Gizmos.DrawCube(n.worldPosition, Vector3.one * (nodeRadius));
            }

            // Draw colored lines for each agent's path with an offset.
            foreach (var kvp in activePaths)
            {
                List<Node2D> path = kvp.Value;
                if (path.Count > 1)
                {
                    Gizmos.color = GetRandomColorForAgent(kvp.Key);
                    Vector3 offset = GetRandomOffsetForAgent(kvp.Key);
                    for (int i = 0; i < path.Count - 1; i++)
                    {
                        Vector3 startPos = path[i].worldPosition + offset;
                        Vector3 endPos = path[i + 1].worldPosition + offset;
                        Gizmos.DrawLine(startPos, endPos);
                    }
                }
            }
        }
    }
}
