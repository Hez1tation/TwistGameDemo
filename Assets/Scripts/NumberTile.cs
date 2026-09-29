using UnityEngine;

// A collectible number tile. Walking onto it applies the current pending operator.
public class NumberTile : MonoBehaviour
{
    public int value = 1;
    public Vector2Int Cell => GridUtil.CellOf(transform);
}
