using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Camera))]
public sealed class BattleOverviewCameraController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Camera controlledCamera;
    [SerializeField] private Transform playerShipTarget;
    [SerializeField] private Transform enemyShipTarget;
    [SerializeField] private RectTransform overviewFrame;

    [Header("Viewport")]
    [SerializeField] private Rect viewport =
        new Rect(0.68f, 0.03f, 0.30f, 0.12f);

    [Header("Framing")]
    [SerializeField, Min(0f)] private float horizontalPadding = 4f;
    [SerializeField, Min(0f)] private float verticalPadding = 2f;
    [SerializeField, Min(0.1f)] private float minimumOrthographicSize = 3f;
    [SerializeField, Min(0.1f)] private float maximumOrthographicSize = 60f;
    [SerializeField] private bool updateContinuously;

    private Renderer[] playerRenderers = new Renderer[0];
    private Renderer[] enemyRenderers = new Renderer[0];
    private float cameraZ;
    private bool initialized;
    private Vector2Int lastScreenSize;

    public Rect Viewport => viewport;

    private void Awake()
    {
        if (controlledCamera == null)
        {
            controlledCamera = GetComponent<Camera>();
        }

        cameraZ = transform.position.z;
        ApplyViewport();
    }

    private IEnumerator Start()
    {
        if (playerShipTarget == null || enemyShipTarget == null)
        {
            Debug.LogError(
                "BattleOverviewCameraController: both ship targets must be assigned.",
                this
            );
            enabled = false;
            yield break;
        }

        // ShipGenerator creates the visible rooms in Start, so cache them one frame later.
        yield return null;

        CacheRenderers();
        RefreshFraming();
        lastScreenSize = new Vector2Int(Screen.width, Screen.height);
        initialized = true;
    }

    private void LateUpdate()
    {
        if (!initialized)
        {
            return;
        }

        Vector2Int currentScreenSize =
            new Vector2Int(Screen.width, Screen.height);

        if (updateContinuously || currentScreenSize != lastScreenSize)
        {
            RefreshFraming();
            lastScreenSize = currentScreenSize;
        }
    }

    public void RefreshFraming()
    {
        if (controlledCamera == null ||
            playerShipTarget == null ||
            enemyShipTarget == null)
        {
            return;
        }

        Bounds battleBounds = CreateBattleBounds();
        Vector3 center = battleBounds.center;

        // Keep the explicit midpoint behaviour when renderer bounds are unavailable.
        if (playerRenderers.Length == 0 && enemyRenderers.Length == 0)
        {
            center =
                (playerShipTarget.position + enemyShipTarget.position) * 0.5f;
        }

        transform.position = new Vector3(center.x, center.y, cameraZ);

        float safeAspect = Mathf.Max(0.01f, controlledCamera.aspect);
        float requiredVerticalHalfSize =
            battleBounds.extents.y + verticalPadding;
        float requiredHorizontalHalfSize =
            (battleBounds.extents.x + horizontalPadding) / safeAspect;

        controlledCamera.orthographic = true;
        controlledCamera.orthographicSize = Mathf.Clamp(
            Mathf.Max(requiredVerticalHalfSize, requiredHorizontalHalfSize),
            minimumOrthographicSize,
            maximumOrthographicSize
        );
    }

    public bool IsPointerInsideOverviewViewport(Vector2 screenPosition)
    {
        if (Screen.width <= 0 || Screen.height <= 0)
        {
            return false;
        }

        Vector2 normalizedPosition = new Vector2(
            screenPosition.x / Screen.width,
            screenPosition.y / Screen.height
        );

        return viewport.Contains(normalizedPosition);
    }

    private void CacheRenderers()
    {
        playerRenderers =
            playerShipTarget.GetComponentsInChildren<Renderer>(true);
        enemyRenderers =
            enemyShipTarget.GetComponentsInChildren<Renderer>(true);
    }

    private Bounds CreateBattleBounds()
    {
        Bounds bounds = new Bounds(
            (playerShipTarget.position + enemyShipTarget.position) * 0.5f,
            Vector3.zero
        );

        bool hasBounds = false;
        EncapsulateRenderers(playerRenderers, ref bounds, ref hasBounds);
        EncapsulateRenderers(enemyRenderers, ref bounds, ref hasBounds);

        if (!hasBounds)
        {
            bounds = new Bounds(playerShipTarget.position, Vector3.zero);
            bounds.Encapsulate(enemyShipTarget.position);
        }

        return bounds;
    }

    private static void EncapsulateRenderers(
        Renderer[] renderers,
        ref Bounds bounds,
        ref bool hasBounds)
    {
        foreach (Renderer shipRenderer in renderers)
        {
            if (shipRenderer == null || !shipRenderer.enabled)
            {
                continue;
            }

            if (!hasBounds)
            {
                bounds = shipRenderer.bounds;
                hasBounds = true;
                continue;
            }

            bounds.Encapsulate(shipRenderer.bounds);
        }
    }

    private void ApplyViewport()
    {
        if (controlledCamera != null)
        {
            controlledCamera.rect = viewport;
        }

        if (overviewFrame == null)
        {
            return;
        }

        overviewFrame.anchorMin = viewport.min;
        overviewFrame.anchorMax = viewport.max;
        overviewFrame.anchoredPosition = Vector2.zero;
        overviewFrame.sizeDelta = Vector2.zero;
    }

    private void OnValidate()
    {
        viewport.x = Mathf.Clamp01(viewport.x);
        viewport.y = Mathf.Clamp01(viewport.y);
        viewport.width = Mathf.Clamp(viewport.width, 0.01f, 1f - viewport.x);
        viewport.height = Mathf.Clamp(viewport.height, 0.01f, 1f - viewport.y);
        horizontalPadding = Mathf.Max(0f, horizontalPadding);
        verticalPadding = Mathf.Max(0f, verticalPadding);
        minimumOrthographicSize = Mathf.Max(0.1f, minimumOrthographicSize);
        maximumOrthographicSize = Mathf.Max(
            minimumOrthographicSize,
            maximumOrthographicSize
        );

        if (controlledCamera == null)
        {
            controlledCamera = GetComponent<Camera>();
        }

        ApplyViewport();
    }
}
