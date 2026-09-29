using UnityEngine;

// A collectible operator tile. Walking onto it sets the pending operator applied to the next
// number tile collected.
public class OperatorTile : MonoBehaviour
{
    public MathOp op = MathOp.Plus;
    public Vector2Int Cell => GridUtil.CellOf(transform);
}
