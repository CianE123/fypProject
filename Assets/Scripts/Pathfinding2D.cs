using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class Pathfinding2D : MonoBehaviour
{
    public Transform target; 
    private Grid2D grid;
    private Node2D seekerNode, targetNode;
    public GameObject GridOwner;

    void Start()
    {
        grid = GridOwner.GetComponent<Grid2D>();
    }

    public void FindPath()
    {
        if (target == null)
        {
            Debug.LogWarning($"{gameObject.name} does not have a target assigned.");
            return;
        }

        // Get the seeker and target positions in grid coordinates
        seekerNode = grid.NodeFromWorldPoint(transform.position);
        targetNode = grid.NodeFromWorldPoint(target.position);

        List<Node2D> openSet = new List<Node2D>();
        HashSet<Node2D> closedSet = new HashSet<Node2D>();
        openSet.Add(seekerNode);

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
                RetracePath(seekerNode, targetNode);
                return;
            }

            foreach (Node2D neighbour in grid.GetNeighbors(node))
            {
                if (neighbour.obstacle || closedSet.Contains(neighbour))
                    continue;

                int newCostToNeighbour = node.gCost + GetDistance(node, neighbour);
                if (newCostToNeighbour < neighbour.gCost || !openSet.Contains(neighbour))
                {
                    neighbour.gCost = newCostToNeighbour;
                    neighbour.hCost = GetDistance(neighbour, targetNode);
                    neighbour.parent = node;

                    if (!openSet.Contains(neighbour))
                        openSet.Add(neighbour);
                }
            }
        }
    }

    void RetracePath(Node2D startNode, Node2D endNode)
    {
        List<Node2D> path = new List<Node2D>();
        Node2D currentNode = endNode;

        while (currentNode != startNode)
        {
            path.Add(currentNode);
            currentNode = currentNode.parent;
        }
        path.Reverse();

        // Store the path in the Grid2D manager for this seeker
        grid.SetPath(transform, path);
    }


    private int GetDistance(Node2D nodeA, Node2D nodeB)
    {
        int dstX = Mathf.Abs(nodeA.GridX - nodeB.GridX);
        int dstY = Mathf.Abs(nodeA.GridY - nodeB.GridY);

        return (dstX > dstY) ? 14 * dstY + 10 * (dstX - dstY) : 14 * dstX + 10 * (dstY - dstX);
    }
}
