using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class BottomUpGridLayoutGroup : LayoutGroup
{
    [Min(1)] [SerializeField] private int columns = 4;
    [SerializeField] private Vector2 cellSize = new Vector2(80f, 50f);
    [SerializeField] private Vector2 spacing = new Vector2(6f, 6f);

    public int Columns
    {
        get => columns;
        set
        {
            columns = Mathf.Max(1, value);
            SetDirty();
        }
    }

    public Vector2 CellSize
    {
        get => cellSize;
        set
        {
            cellSize = new Vector2(Mathf.Max(1f, value.x), Mathf.Max(1f, value.y));
            SetDirty();
        }
    }

    public Vector2 Spacing
    {
        get => spacing;
        set
        {
            spacing = new Vector2(Mathf.Max(0f, value.x), Mathf.Max(0f, value.y));
            SetDirty();
        }
    }

    public override void CalculateLayoutInputHorizontal()
    {
        base.CalculateLayoutInputHorizontal();
        int usedColumns = Mathf.Min(columns, rectChildren.Count);
        float width = padding.horizontal + usedColumns * cellSize.x +
                      Mathf.Max(0, usedColumns - 1) * spacing.x;
        SetLayoutInputForAxis(width, width, -1f, 0);
    }

    public override void CalculateLayoutInputVertical()
    {
        int rows = Mathf.CeilToInt(rectChildren.Count / (float)columns);
        float height = padding.vertical + rows * cellSize.y +
                       Mathf.Max(0, rows - 1) * spacing.y;
        SetLayoutInputForAxis(height, height, -1f, 1);
    }

    public override void SetLayoutHorizontal() => Arrange();
    public override void SetLayoutVertical() => Arrange();

    private void Arrange()
    {
        for (int index = 0; index < rectChildren.Count; index++)
        {
            int column = index % columns;
            int row = index / columns;
            RectTransform child = rectChildren[index];

            SetChildAlongAxis(child, 0, padding.left + column * (cellSize.x + spacing.x), cellSize.x);

            float yFromTop = rectTransform.rect.height - padding.bottom -
                             (row + 1) * cellSize.y - row * spacing.y;
            SetChildAlongAxis(child, 1, yFromTop, cellSize.y);
        }
    }

#if UNITY_EDITOR
    protected override void OnValidate()
    {
        base.OnValidate();
        columns = Mathf.Max(1, columns);
        cellSize = new Vector2(Mathf.Max(1f, cellSize.x), Mathf.Max(1f, cellSize.y));
        spacing = new Vector2(Mathf.Max(0f, spacing.x), Mathf.Max(0f, spacing.y));
    }
#endif
}
