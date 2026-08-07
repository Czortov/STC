using UnityEngine;

[DisallowMultipleComponent]
public sealed class CrewWalkSway : MonoBehaviour
{
    [Header("Visual")]

    [SerializeField] private Transform visual;

    [Header("Sway")]

    [Min(0f)]
    [SerializeField] private float swayAngle = 6f;

    [Min(0f)]
    [SerializeField] private float swaySpeed = 8f;

    private Quaternion initialLocalRotation;
    private float elapsedTime;
    private float currentAngle;
    private bool isMoving;
    private bool hasExternalRotationControl;
    private bool hasInitialRotation;

    private void Awake()
    {
        CacheInitialRotation();
    }

    private void Update()
    {
        if (visual == null)
        {
            return;
        }

        if (hasExternalRotationControl)
        {
            return;
        }

        if (!hasInitialRotation)
        {
            CacheInitialRotation();
        }

        float targetAngle = 0f;

        if (isMoving)
        {
            elapsedTime += Time.deltaTime;
            targetAngle =
                Mathf.Sin(elapsedTime * swaySpeed) * swayAngle;
        }

        float smoothing = swaySpeed > 0f
            ? 1f - Mathf.Exp(-swaySpeed * Time.deltaTime)
            : 1f;

        currentAngle =
            Mathf.Lerp(currentAngle, targetAngle, smoothing);

        if (!isMoving && Mathf.Abs(currentAngle) < 0.01f)
        {
            currentAngle = 0f;
        }

        visual.localRotation =
            initialLocalRotation *
            Quaternion.Euler(0f, 0f, currentAngle);
    }

    public void SetMoving(bool moving)
    {
        if (isMoving == moving)
        {
            return;
        }

        isMoving = moving;

        if (moving)
        {
            elapsedTime = 0f;
        }
    }

    public void SetExternalRotationControl(bool externallyControlled)
    {
        hasExternalRotationControl = externallyControlled;

        if (externallyControlled)
        {
            isMoving = false;
            elapsedTime = 0f;
            currentAngle = 0f;

            if (visual != null && hasInitialRotation)
            {
                visual.localRotation = initialLocalRotation;
            }
        }
    }

    private void CacheInitialRotation()
    {
        if (visual == null)
        {
            hasInitialRotation = false;
            return;
        }

        initialLocalRotation = visual.localRotation;
        hasInitialRotation = true;
    }

    private void OnDisable()
    {
        RestoreInitialRotation();
    }

    private void OnDestroy()
    {
        RestoreInitialRotation();
    }

    private void RestoreInitialRotation()
    {
        isMoving = false;
        hasExternalRotationControl = false;
        elapsedTime = 0f;
        currentAngle = 0f;

        if (visual != null && hasInitialRotation)
        {
            visual.localRotation = initialLocalRotation;
        }
    }
}
