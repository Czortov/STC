using UnityEngine;

[DisallowMultipleComponent]
public sealed class AreaImpactPreview : MonoBehaviour
{
    private const int CircleSegments = 64;
    private const int TextureSize = 128;

    private SpriteRenderer fillRenderer;
    private LineRenderer outlineRenderer;
    private Texture2D circleTexture;
    private Sprite circleSprite;
    private Material outlineMaterial;

    public void Initialize(int sortingOrder)
    {
        if (fillRenderer != null)
        {
            return;
        }

        fillRenderer = gameObject.AddComponent<SpriteRenderer>();
        fillRenderer.sprite = CreateCircleSprite();
        fillRenderer.sortingOrder = sortingOrder;
        fillRenderer.enabled = false;

        outlineRenderer = gameObject.AddComponent<LineRenderer>();
        outlineRenderer.useWorldSpace = true;
        outlineRenderer.loop = true;
        outlineRenderer.positionCount = CircleSegments;
        outlineRenderer.numCapVertices = 2;
        outlineRenderer.numCornerVertices = 2;
        outlineRenderer.sortingOrder = sortingOrder + 1;
        outlineRenderer.enabled = false;

        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null)
        {
            shader = Shader.Find("Universal Render Pipeline/Unlit");
        }

        if (shader != null)
        {
            outlineMaterial = new Material(shader)
            {
                name = "RuntimeAreaImpactPreviewMaterial"
            };
            outlineRenderer.material = outlineMaterial;
        }
    }

    public void Show(Vector2 center, float radius, Color color)
    {
        if (fillRenderer == null || outlineRenderer == null)
        {
            Initialize(101);
        }

        if (fillRenderer == null || outlineRenderer == null || radius <= 0f)
        {
            Hide();
            return;
        }

        const float z = -0.15f;
        transform.position = new Vector3(center.x, center.y, z);
        transform.localScale = new Vector3(radius * 2f, radius * 2f, 1f);

        Color fillColor = color;
        fillColor.a = Mathf.Min(color.a, 0.2f);
        fillRenderer.color = fillColor;
        fillRenderer.enabled = true;

        Color outlineColor = color;
        outlineColor.a = Mathf.Max(color.a, 0.8f);
        outlineRenderer.startColor = outlineColor;
        outlineRenderer.endColor = outlineColor;
        outlineRenderer.startWidth = Mathf.Max(0.035f, radius * 0.025f);
        outlineRenderer.endWidth = outlineRenderer.startWidth;

        for (int index = 0; index < CircleSegments; index++)
        {
            float angle = index * Mathf.PI * 2f / CircleSegments;
            outlineRenderer.SetPosition(
                index,
                new Vector3(
                    center.x + Mathf.Cos(angle) * radius,
                    center.y + Mathf.Sin(angle) * radius,
                    z - 0.01f
                )
            );
        }

        outlineRenderer.enabled = true;
    }

    public void Hide()
    {
        if (fillRenderer != null)
        {
            fillRenderer.enabled = false;
        }

        if (outlineRenderer != null)
        {
            outlineRenderer.enabled = false;
        }
    }

    private Sprite CreateCircleSprite()
    {
        circleTexture = new Texture2D(TextureSize, TextureSize, TextureFormat.RGBA32, false)
        {
            name = "RuntimeAreaImpactPreviewTexture",
            filterMode = FilterMode.Bilinear,
            hideFlags = HideFlags.HideAndDontSave
        };

        float center = (TextureSize - 1) * 0.5f;
        float radius = TextureSize * 0.49f;
        float radiusSquared = radius * radius;

        for (int y = 0; y < TextureSize; y++)
        {
            for (int x = 0; x < TextureSize; x++)
            {
                float offsetX = x - center;
                float offsetY = y - center;
                bool inside = offsetX * offsetX + offsetY * offsetY <= radiusSquared;
                circleTexture.SetPixel(x, y, inside ? Color.white : Color.clear);
            }
        }

        circleTexture.Apply();

        circleSprite = Sprite.Create(
            circleTexture,
            new Rect(0f, 0f, TextureSize, TextureSize),
            new Vector2(0.5f, 0.5f),
            TextureSize
        );
        circleSprite.name = "RuntimeAreaImpactPreviewSprite";
        circleSprite.hideFlags = HideFlags.HideAndDontSave;
        return circleSprite;
    }

    private void OnDestroy()
    {
        if (outlineMaterial != null)
        {
            Destroy(outlineMaterial);
        }

        if (circleSprite != null)
        {
            Destroy(circleSprite);
        }

        if (circleTexture != null)
        {
            Destroy(circleTexture);
        }
    }
}
