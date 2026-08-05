using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Camera))]
public sealed class PlayerShipCameraController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Camera controlledCamera;
    [SerializeField] private Transform playerShipTarget;
    [SerializeField] private BattleOverviewCameraController overviewCamera;

    [Header("Movement")]
    [SerializeField] private float panSpeed = 1f;
    [SerializeField] private Vector2 maxCameraOffset = new Vector2(5f, 3f);
    [SerializeField] private Vector2 initialOffset;

    [Header("Zoom")]
    [SerializeField, Min(0.1f)] private float minOrthographicSize = 3f;
    [SerializeField, Min(0.1f)] private float maxOrthographicSize = 8f;
    [SerializeField, Min(0.1f)] private float zoomSpeed = 1f;

    [Header("Input Bounds")]
    [SerializeField] private bool restrictInputToLeftHalf = true;
    [SerializeField] private bool ignoreInputOverUI = true;

    private Vector3 dragStartWorldPosition;
    private bool isDragging;
    private float cameraZ;

    private void Awake()
    {
        if (controlledCamera == null)
        {
            controlledCamera = GetComponent<Camera>();
        }

        cameraZ = transform.position.z;
    }

    private void Start()
    {
        if (playerShipTarget == null)
        {
            Debug.LogError(
                "PlayerShipCameraController: Player Ship Target is not assigned.",
                this
            );
            enabled = false;
            return;
        }

        Vector3 targetPosition = playerShipTarget.position;
        transform.position = new Vector3(
            targetPosition.x + initialOffset.x,
            targetPosition.y + initialOffset.y,
            cameraZ
        );

        ClampPosition();
        ClampZoom();
    }

    private void Update()
    {
        if (Mouse.current == null ||
            controlledCamera == null ||
            playerShipTarget == null)
        {
            return;
        }

        Vector2 mousePosition = Mouse.current.position.ReadValue();
        bool pointerInInputArea = IsPointerInInputArea(mousePosition);

        if (!pointerInInputArea)
        {
            isDragging = false;
            return;
        }

        HandleDrag(mousePosition);
        HandleZoom();
    }

    private void HandleDrag(Vector2 mousePosition)
    {
        if (Mouse.current.middleButton.wasPressedThisFrame)
        {
            if (IsPointerBlockedByUI())
            {
                return;
            }

            isDragging = true;
            dragStartWorldPosition =
                controlledCamera.ScreenToWorldPoint(mousePosition);
        }

        if (Mouse.current.middleButton.wasReleasedThisFrame)
        {
            isDragging = false;
        }

        if (!isDragging || !Mouse.current.middleButton.isPressed)
        {
            return;
        }

        Vector3 currentWorldPosition =
            controlledCamera.ScreenToWorldPoint(mousePosition);

        Vector3 movement =
            (dragStartWorldPosition - currentWorldPosition) * panSpeed;

        transform.position += new Vector3(movement.x, movement.y, 0f);
        ClampPosition();
    }

    private void HandleZoom()
    {
        if (IsPointerBlockedByUI())
        {
            return;
        }

        float scroll = Mouse.current.scroll.ReadValue().y;

        if (Mathf.Approximately(scroll, 0f))
        {
            return;
        }

        controlledCamera.orthographicSize -=
            scroll / 120f * zoomSpeed;

        ClampZoom();
    }

    private bool IsPointerInInputArea(Vector2 mousePosition)
    {
        if (overviewCamera != null &&
            overviewCamera.IsPointerInsideOverviewViewport(mousePosition))
        {
            return false;
        }

        return !restrictInputToLeftHalf ||
               mousePosition.x < Screen.width * 0.5f;
    }

    private bool IsPointerBlockedByUI()
    {
        return ignoreInputOverUI &&
               EventSystem.current != null &&
               EventSystem.current.IsPointerOverGameObject();
    }

    private void ClampPosition()
    {
        Vector3 targetPosition = playerShipTarget.position;
        Vector3 position = transform.position;

        position.x = Mathf.Clamp(
            position.x,
            targetPosition.x - maxCameraOffset.x,
            targetPosition.x + maxCameraOffset.x
        );
        position.y = Mathf.Clamp(
            position.y,
            targetPosition.y - maxCameraOffset.y,
            targetPosition.y + maxCameraOffset.y
        );
        position.z = cameraZ;

        transform.position = position;
    }

    private void ClampZoom()
    {
        controlledCamera.orthographicSize = Mathf.Clamp(
            controlledCamera.orthographicSize,
            minOrthographicSize,
            maxOrthographicSize
        );
    }

    private void OnValidate()
    {
        panSpeed = Mathf.Max(0f, panSpeed);
        maxCameraOffset.x = Mathf.Max(0f, maxCameraOffset.x);
        maxCameraOffset.y = Mathf.Max(0f, maxCameraOffset.y);
        minOrthographicSize = Mathf.Max(0.1f, minOrthographicSize);
        maxOrthographicSize = Mathf.Max(
            minOrthographicSize + 0.1f,
            maxOrthographicSize
        );
        zoomSpeed = Mathf.Max(0.1f, zoomSpeed);
    }
}
