using UnityEngine;

public sealed class RoomOutlineView : MonoBehaviour
{
    private LineRenderer outlineRenderer;
    private Material outlineMaterial;

    private ShipRoomRuntime targetRoom;
    private float padding;

    public bool IsVisible =>
        outlineRenderer != null &&
        outlineRenderer.enabled;

    public void Initialize(int sortingOrder)
    {
        if (outlineRenderer != null)
        {
            return;
        }

        outlineRenderer =
            gameObject.AddComponent<LineRenderer>();

        outlineRenderer.useWorldSpace = true;
        outlineRenderer.loop = true;
        outlineRenderer.positionCount = 4;

        outlineRenderer.numCapVertices = 0;
        outlineRenderer.numCornerVertices = 0;

        outlineRenderer.sortingOrder = sortingOrder;
        outlineRenderer.enabled = false;

        Shader outlineShader =
            Shader.Find("Sprites/Default");

        if (outlineShader == null)
        {
            outlineShader =
                Shader.Find(
                    "Universal Render Pipeline/Unlit"
                );
        }

        if (outlineShader == null)
        {
            outlineShader =
                Shader.Find("Unlit/Color");
        }

        if (outlineShader == null)
        {
            Debug.LogError(
                "RoomOutlineView: не найден подходящий Shader.",
                this
            );

            return;
        }

        outlineMaterial =
            new Material(outlineShader);

        outlineMaterial.name =
            "RuntimeRoomOutlineMaterial";

        outlineRenderer.material =
            outlineMaterial;
    }

    public void Show(
        ShipRoomRuntime room,
        Color color,
        float width,
        float outlinePadding)
    {
        if (outlineRenderer == null)
        {
            Initialize(100);
        }

        if (outlineRenderer == null)
        {
            return;
        }

        targetRoom = room;
        padding = Mathf.Max(0f, outlinePadding);

        outlineRenderer.startWidth =
            Mathf.Max(0.01f, width);

        outlineRenderer.endWidth =
            Mathf.Max(0.01f, width);

        outlineRenderer.startColor = color;
        outlineRenderer.endColor = color;

        outlineRenderer.enabled =
            targetRoom != null;

        RefreshPosition();
    }

    public void Hide()
    {
        targetRoom = null;

        if (outlineRenderer != null)
        {
            outlineRenderer.enabled = false;
        }
    }

    private void LateUpdate()
    {
        if (!IsVisible)
        {
            return;
        }

        RefreshPosition();
    }

    private void RefreshPosition()
    {
        if (targetRoom == null ||
            outlineRenderer == null)
        {
            Hide();
            return;
        }

        Collider2D roomCollider =
            targetRoom.GetComponent<Collider2D>();

        if (roomCollider == null ||
            !roomCollider.enabled)
        {
            Hide();
            return;
        }

        Bounds bounds = roomCollider.bounds;

        float minX = bounds.min.x - padding;
        float maxX = bounds.max.x + padding;
        float minY = bounds.min.y - padding;
        float maxY = bounds.max.y + padding;

        // Камера находится на отрицательном Z,
        // поэтому рамку немного приближаем к ней.
        float z = bounds.center.z - 0.2f;

        outlineRenderer.SetPosition(
            0,
            new Vector3(minX, minY, z)
        );

        outlineRenderer.SetPosition(
            1,
            new Vector3(minX, maxY, z)
        );

        outlineRenderer.SetPosition(
            2,
            new Vector3(maxX, maxY, z)
        );

        outlineRenderer.SetPosition(
            3,
            new Vector3(maxX, minY, z)
        );
    }

    private void OnDestroy()
    {
        if (outlineMaterial != null)
        {
            Destroy(outlineMaterial);
        }
    }
}