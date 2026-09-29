using UnityEngine;

// Marks the level's start cell. Its own Transform doubles as the player token at runtime.
public class StartMarker : MonoBehaviour
{
    public Vector2Int Cell => GridUtil.CellOf(transform);
}
