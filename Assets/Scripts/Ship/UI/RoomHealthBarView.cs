using UnityEngine;

[DisallowMultipleComponent]
public sealed class RoomHealthBarView : MonoBehaviour
{
    private static Material sharedLineMaterial;

    [Header("Visibility")]

    [Tooltip(
        "Полоса скрыта, пока отсек имеет максимальное здоровье.")]
    [SerializeField] private bool showOnlyWhenDamaged = true;

    [Header("Position and Size")]

    [Tooltip(
        "Ширина полосы относительно ширины отсека.")]
    [Range(0.1f, 1.2f)]
    [SerializeField] private float roomWidthMultiplier = 0.85f;

    [Min(0.05f)]
    [SerializeField] private float minimumWidth = 0.3f;

    [Min(0.01f)]
    [SerializeField] private float backgroundHeight = 0.14f;

    [Min(0.01f)]
    [SerializeField] private float fillHeight = 0.08f;

    [Tooltip(
        "Расстояние между верхней границей отсека и полосой.")]
    [SerializeField] private float verticalOffset = 0.17f;

    [Header("Colors")]

    [SerializeField] private Color backgroundColor =
        new Color(0.05f, 0.05f, 0.05f, 0.9f);

    [SerializeField] private Color healthyColor =
        new Color(0.15f, 0.9f, 0.2f, 1f);

    [SerializeField] private Color damagedColor =
        new Color(1f, 0.75f, 0.1f, 1f);

    [SerializeField] private Color criticalColor =
        new Color(1f, 0.15f, 0.1f, 1f);

    [Header("Rendering")]

    [SerializeField] private int backgroundSortingOrder = 110;
    [SerializeField] private int fillSortingOrder = 111;

    private ShipRoomRuntime room;

    private LineRenderer backgroundLine;
    private LineRenderer fillLine;

    private bool isVisible;

    public void Initialize(ShipRoomRuntime targetRoom)
    {
        room = targetRoom;
        Refresh();
    }

    public void Refresh()
    {
        if (room == null || room.MaxHealth <= 0)
        {
            SetVisible(false);
            return;
        }

        bool shouldShow =
            !showOnlyWhenDamaged ||
            room.CurrentHealth < room.MaxHealth;

        if (!shouldShow)
        {
            SetVisible(false);
            return;
        }

        EnsureRenderers();

        if (backgroundLine == null || fillLine == null)
        {
            return;
        }

        SetVisible(true);

        float healthRatio = GetHealthRatio();

        Color fillColor =
            GetHealthColor(healthRatio);

        fillLine.startColor = fillColor;
        fillLine.endColor = fillColor;

        UpdateGeometry(healthRatio);
    }

    private void LateUpdate()
    {
        if (!isVisible || room == null)
        {
            return;
        }

        UpdateGeometry(GetHealthRatio());
    }

    private float GetHealthRatio()
    {
        if (room == null || room.MaxHealth <= 0)
        {
            return 0f;
        }

        return Mathf.Clamp01(
            (float)room.CurrentHealth /
            room.MaxHealth
        );
    }

    private Color GetHealthColor(float healthRatio)
    {
        if (healthRatio > 0.6f)
        {
            return healthyColor;
        }

        if (healthRatio > 0.3f)
        {
            return damagedColor;
        }

        return criticalColor;
    }

    private void EnsureRenderers()
    {
        if (backgroundLine == null)
        {
            backgroundLine = CreateLineRenderer(
                "HealthBarBackground",
                backgroundSortingOrder,
                backgroundHeight,
                backgroundColor
            );
        }

        if (fillLine == null)
        {
            fillLine = CreateLineRenderer(
                "HealthBarFill",
                fillSortingOrder,
                fillHeight,
                healthyColor
            );
        }
    }

    private LineRenderer CreateLineRenderer(
        string objectName,
        int sortingOrder,
        float lineWidth,
        Color color)
    {
        GameObject lineObject =
            new GameObject(objectName);

        lineObject.transform.SetParent(
            transform,
            false
        );

        LineRenderer lineRenderer =
            lineObject.AddComponent<LineRenderer>();

        lineRenderer.useWorldSpace = true;
        lineRenderer.positionCount = 2;

        lineRenderer.startWidth = lineWidth;
        lineRenderer.endWidth = lineWidth;

        lineRenderer.startColor = color;
        lineRenderer.endColor = color;

        lineRenderer.numCapVertices = 2;
        lineRenderer.numCornerVertices = 0;

        lineRenderer.sortingOrder = sortingOrder;
        lineRenderer.enabled = false;

        Material lineMaterial =
            GetSharedLineMaterial();

        if (lineMaterial != null)
        {
            lineRenderer.sharedMaterial =
                lineMaterial;
        }

        return lineRenderer;
    }

    private void UpdateGeometry(float healthRatio)
    {
        if (backgroundLine == null ||
            fillLine == null ||
            room == null)
        {
            return;
        }

        Collider2D roomCollider =
            room.GetComponent<Collider2D>();

        if (roomCollider == null ||
            !roomCollider.enabled)
        {
            SetVisible(false);
            return;
        }

        Bounds bounds = roomCollider.bounds;

        float barWidth = Mathf.Max(
            minimumWidth,
            bounds.size.x * roomWidthMultiplier
        );

        float leftX =
            bounds.center.x - barWidth * 0.5f;

        float rightX =
            bounds.center.x + barWidth * 0.5f;

        float y =
            bounds.max.y + verticalOffset;

        float z =
            bounds.center.z - 0.25f;

        Vector3 leftPosition =
            new Vector3(leftX, y, z);

        Vector3 rightPosition =
            new Vector3(rightX, y, z);

        backgroundLine.SetPosition(
            0,
            leftPosition
        );

        backgroundLine.SetPosition(
            1,
            rightPosition
        );

        // При нулевом здоровье полностью отключаем
        // заполнение, чтобы LineRenderer не оставлял точку.
        if (healthRatio <= 0.0001f)
        {
            fillLine.enabled = false;
            return;
        }

        fillLine.enabled = true;

        float fillRightX =
            Mathf.Lerp(
                leftX,
                rightX,
                healthRatio
            );

        Vector3 fillRightPosition =
            new Vector3(fillRightX, y, z);

        fillLine.SetPosition(
            0,
            leftPosition
        );

        fillLine.SetPosition(
            1,
            fillRightPosition
        );
    }

    private void SetVisible(bool visible)
    {
        isVisible = visible;

        if (backgroundLine != null)
        {
            backgroundLine.enabled = visible;
        }

        if (fillLine != null)
        {
            fillLine.enabled = visible;
        }
    }

    private static Material GetSharedLineMaterial()
    {
        if (sharedLineMaterial != null)
        {
            return sharedLineMaterial;
        }

        Shader lineShader =
            Shader.Find("Sprites/Default");

        if (lineShader == null)
        {
            lineShader =
                Shader.Find(
                    "Universal Render Pipeline/Unlit"
                );
        }

        if (lineShader == null)
        {
            lineShader =
                Shader.Find("Unlit/Color");
        }

        if (lineShader == null)
        {
            Debug.LogError(
                "RoomHealthBarView: " +
                "не найден подходящий Shader."
            );

            return null;
        }

        sharedLineMaterial =
            new Material(lineShader);

        sharedLineMaterial.name =
            "RuntimeRoomHealthBarMaterial";

        sharedLineMaterial.hideFlags =
            HideFlags.HideAndDontSave;

        return sharedLineMaterial;
    }
}