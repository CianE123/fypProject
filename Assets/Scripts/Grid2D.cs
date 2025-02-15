using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public class Grid2D : MonoBehaviour
{
    public Vector3 gridWorldSize;
    public float nodeRadius;
    public Node2D[,] Grid;
    public Tilemap obstaclemap;

    public Dictionary<Transform, List<Node2D>> paths = new Dictionary<Transform, List<Node2D>>();

    Vector3 worldBottomLeft;
    float nodeDiameter;
    int gridSizeX, gridSizeY;

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
        worldBottomLeft = transform.position - Vector3.right * gridWorldSize.x / 2 - Vector3.up * gridWorldSize.y / 2;

        for (int x = 0; x < gridSizeX; x++)
        {
            for (int y = 0; y < gridSizeY; y++)
            {
                Vector3 worldPoint = worldBottomLeft + Vector3.right * (x * nodeDiameter + nodeRadius) + Vector3.up * (y * nodeDiameter + nodeRadius);
                Grid[x, y] = new Node2D(false, worldPoint, x, y);

                if (obstaclemap.HasTile(obstaclemap.WorldToCell(Grid[x, y].worldPosition)))
                    Grid[x, y].SetObstacle(true);
                else
                    Grid[x, y].SetObstacle(false);
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


    public void SetPath(Transform seeker, List<Node2D> path)
    {
        if (paths.ContainsKey(seeker))
        {
            paths[seeker] = path;
        }
        else
        {
            paths.Add(seeker, path);
        }
    }

    private List<Node2D> DetectCollisions()
    {
        // Tracks arrival times for each node
        Dictionary<Node2D, Dictionary<int, int>> nodeArrivalTimes = new Dictionary<Node2D, Dictionary<int, int>>();
        // Tracks pass-through scenarios
        HashSet<(Node2D, Node2D, int)> passThroughChecks = new HashSet<(Node2D, Node2D, int)>();

        List<Node2D> collisionNodes = new List<Node2D>();

        foreach (var pathEntry in paths)
        {
            Transform seeker = pathEntry.Key;
            List<Node2D> path = pathEntry.Value;

            for (int i = 0; i < path.Count; i++)
            {
                Node2D currentNode = path[i];
                int arrivalTime = i; // Time step corresponds to the index in the path

                // Record arrival times
                if (!nodeArrivalTimes.ContainsKey(currentNode))
                {
                    nodeArrivalTimes[currentNode] = new Dictionary<int, int>();
                }

                if (!nodeArrivalTimes[currentNode].ContainsKey(arrivalTime))
                {
                    nodeArrivalTimes[currentNode][arrivalTime] = 0;
                }

                nodeArrivalTimes[currentNode][arrivalTime]++;

                // If multiple agents arrive at the same node at the same time
                if (nodeArrivalTimes[currentNode][arrivalTime] > 1 && !collisionNodes.Contains(currentNode))
                {
                    collisionNodes.Add(currentNode);
                }

                // Check for pass-through collisions
                if (i > 0)
                {
                    Node2D previousNode = path[i - 1];
                    var passThroughKey = (currentNode, previousNode, arrivalTime);

                    // Check if another agent is moving in the opposite direction at the same time
                    foreach (var otherEntry in paths)
                    {
                        if (otherEntry.Key == seeker) continue; // Skip the same agent

                        List<Node2D> otherPath = otherEntry.Value;
                        if (arrivalTime > 0 && arrivalTime < otherPath.Count)
                        {
                            Node2D otherCurrent = otherPath[arrivalTime];
                            Node2D otherPrevious = otherPath[arrivalTime - 1];

                            if (otherCurrent == previousNode && otherPrevious == currentNode)
                            {
                                passThroughChecks.Add(passThroughKey);
                                collisionNodes.Add(currentNode); // Add the node as a collision
                            }
                        }
                    }
                }
            }
        }

        return collisionNodes;
    }

    void OnDrawGizmos()
    {
        Gizmos.DrawWireCube(transform.position, new Vector3(gridWorldSize.x, gridWorldSize.y, 1));

        if (Grid != null)
        {
            List<Node2D> collisionNodes = DetectCollisions();

            foreach (Node2D n in Grid)
            {
                if (n.obstacle)
                    Gizmos.color = Color.red;
                else
                    Gizmos.color = Color.white;

                // Highlight collision nodes in yellow
                if (collisionNodes.Contains(n))
                {
                    Gizmos.color = Color.yellow;
                }
                else
                {
                    // Highlight nodes in paths
                    foreach (var path in paths.Values)
                    {
                        if (path.Contains(n))
                        {
                            Gizmos.color = Color.black;
                        }
                    }
                }

                Gizmos.DrawCube(n.worldPosition, Vector3.one * (nodeRadius));
            }
        }
    }
}
