using UnityEngine;

// Marks a scene primitive as a solid wall cell. Placed as a real Unity primitive so designers
// can select and move it in the Scene view.
public class WallTile : MonoBehaviour
{
    public Vector2Int Cell => GridUtil.CellOf(transform);
}
