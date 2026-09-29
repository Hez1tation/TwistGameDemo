using UnityEngine;

// Shared helpers for mapping scene objects to grid cells. World X = column, World Z = row.
public static class GridUtil
{
    // Level-relative cell coordinates. Uses localPosition (not world position) so a tile's
    // cell stays the same regardless of where its level root is offset to in the scene.
    public static Vector2Int CellOf(Transform t) =>
        new Vector2Int(Mathf.RoundToInt(t.localPosition.x), Mathf.RoundToInt(t.localPosition.z));

    public static Vector3 WorldOf(Vector3 levelOrigin, Vector2Int cell, float y = 0f) =>
        levelOrigin + new Vector3(cell.x, y, cell.y);
}
