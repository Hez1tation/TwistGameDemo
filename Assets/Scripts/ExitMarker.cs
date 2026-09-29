using UnityEngine;

// Marks the level's exit cell.
public class ExitMarker : MonoBehaviour
{
    public Vector2Int Cell => GridUtil.CellOf(transform);
}
