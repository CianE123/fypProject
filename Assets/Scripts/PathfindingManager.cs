using UnityEngine;

public class PathfindingManager : MonoBehaviour
{
    void Update()
    {
        // Trigger pathfinding for all agents when the Spacebar is pressed
        if (Input.GetKeyDown(KeyCode.Space))
        {
            TriggerPathfindingForAll();
        }
    }

    void TriggerPathfindingForAll()
    {
        // Find all objects with the Pathfinding2D component
        Pathfinding2D[] pathfinders = FindObjectsOfType<Pathfinding2D>();

        foreach (Pathfinding2D pathfinder in pathfinders)
        {
            if (pathfinder != null)
            {   
                pathfinder.FindPath(); 
            }
        }
    }
}