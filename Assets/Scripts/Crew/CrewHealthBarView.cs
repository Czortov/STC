using UnityEngine;

[DisallowMultipleComponent]
public sealed class CrewHealthBarView : MonoBehaviour
{
    private static Material sharedLineMaterial;

    [Header("Visibility")]

    [SerializeField] private bool showOnlyWhenDamaged = true;

    [Header("Size")]

    [Min(0.1f)]
    [SerializeField] private float barWidth = 0.55f;

    [Min(0.01f)]
    [SerializeField] private float backgroundHeight = 0.09f;

    [Min(0.01f)]
    [SerializeField] private float fillHeight = 0.055f;

    [SerializeField] private float verticalOffset = 0.08f;

    [Header("Colors")]

    [SerializeField] private Color backgroundColor =
        new Color(0.05f, 0.05f, 0.05f, 0.9f);

    [SerializeField] private Color healthyColor =
        new Color(0.15f, 0.9f, 0.2f, 1f);

    [SerializeField] private Color damagedColor =
        new Color(1f, 0.7f, 0.1f, 1f);

    [SerializeField] private Color criticalColor =
        new Color(1f, 0.15f, 0.1f, 1f);

    [Header("Rendering")]

    [SerializeField] private int backgroundSortingOrder = 120;
    [SerializeField] private int fillSortingOrder = 121;

    private CrewHealth health;
    private SpriteRenderer unitRenderer;

    private LineRenderer backgroundLine;
    private LineRenderer fillLine;

    private bool isVisible;

    public void Initialize(CrewHealth targetHealth)
    {
        health = targetHealth;
        unitRenderer = GetComponent<SpriteRenderer>();

        Refresh();
    }

    public void Refresh()
    {
        if (health == null)
        {
            SetVisible(false);
            return;
        }

        bool shouldShow =
            !showOnlyWhenDamaged ||
            health.IsDamaged;

        if (!shouldShow)
        {
            SetVisible(false);
            return;
        }

        EnsureRenderers();

        if (backgroundLine == null ||
            fillLine == null)
        {
            return;
        }

        SetVisible(true);

        float healthRatio =
            health.HealthRatio;

        Color fillColor =
            GetHealthColor(healthRatio);

        fillLine.startColor = fillColor;
        fillLine.endColor = fillColor;

        UpdateGeometry(healthRatio);
    }

    private void LateUpdate()
    {
        if (!isVisible || health == null)
        {
            return;
        }

        UpdateGeometry(health.HealthRatio);
    }

    private void EnsureRenderers()
    {
        if (backgroundLine == null)
        {
            backgroundLine = CreateLine(
                "CrewHealthBackground",
                backgroundHeight,
                backgroundColor,
                backgroundSortingOrder
            );
        }

        if (fillLine == null)
        {
            fillLine = CreateLine(
                "CrewHealthFill",
                fillHeight,
                healthyColor,
                fillSortingOrder
            );
        }
    }

    private LineRenderer CreateLine(
        string objectName,
        float width,
        Color color,
        int sortingOrder)
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

        lineRenderer.startWidth = width;
        lineRenderer.endWidth = width;

        lineRenderer.startColor = color;
        lineRenderer.endColor = color;

        lineRenderer.numCapVertices = 2;
        lineRenderer.sortingOrder = sortingOrder;
        lineRenderer.enabled = false;

        Material material =
            GetSharedLineMaterial();

        if (material != null)
        {
            lineRenderer.sharedMaterial = material;
        }

        return lineRenderer;
    }

    private void UpdateGeometry(float healthRatio)
    {
        if (backgroundLine == null ||
            fillLine == null)
        {
            return;
        }

        Bounds unitBounds;

        if (unitRenderer != null &&
            unitRenderer.enabled)
        {
            unitBounds = unitRenderer.bounds;
        }
        else
        {
            unitBounds = new Bounds(
                transform.position,
                Vector3.one * 0.5f
            );
        }

        float leftX =
            unitBounds.center.x - barWidth * 0.5f;

        float rightX =
            unitBounds.center.x + barWidth * 0.5f;

        float y =
            unitBounds.max.y + verticalOffset;

        float z =
            unitBounds.center.z - 0.2f;

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

        /*
         * При нулевом здоровье отключаем заполнение,
         * чтобы LineRenderer не оставлял красную точку.
         */
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

        fillLine.SetPosition(
            0,
            leftPosition
        );

        fillLine.SetPosition(
            1,
            new Vector3(fillRightX, y, z)
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

    private void SetVisible(bool visible)
    {
        isVisible = visible;

        if (backgroundLine != null)
        {
            backgroundLine.enabled = visible;
        }

        if (fillLine != null)
        {
            fillLine.enabled =
                visible &&
                health != null &&
                health.HealthRatio > 0f;
        }
    }

    private static Material GetSharedLineMaterial()
    {
        if (sharedLineMaterial != null)
        {
            return sharedLineMaterial;
        }

        Shader shader =
            Shader.Find("Sprites/Default");

        if (shader == null)
        {
            shader = Shader.Find(
                "Universal Render Pipeline/Unlit"
            );
        }

        if (shader == null)
        {
            shader =
                Shader.Find("Unlit/Color");
        }

        if (shader == null)
        {
            Debug.LogError(
                "CrewHealthBarView: не найден подходящий Shader."
            );

            return null;
        }

        sharedLineMaterial =
            new Material(shader);

        sharedLineMaterial.name =
            "RuntimeCrewHealthBarMaterial";

        sharedLineMaterial.hideFlags =
            HideFlags.HideAndDontSave;

        return sharedLineMaterial;
    }
}