using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GridSnap : MonoBehaviour
{
    private Grid2D grid;

    void Start()
    {
        // Get the Grid2D instance
        grid = FindObjectOfType<Grid2D>(); 
        if (grid != null)
        {
            Node2D node = grid.NodeFromWorldPoint(transform.position);
            // Snap to grid cell center
            transform.position = node.worldPosition; 
        }
        else
        {
            Debug.LogWarning("Grid2D not found");
        }
    }
}
