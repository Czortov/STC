using UnityEngine;

[DisallowMultipleComponent]
public sealed class CrewRepairAnimation : MonoBehaviour
{
    [Header("Visual References")]

    [SerializeField] private Transform visual;
    [SerializeField] private GameObject repairVisual;
    [SerializeField] private Transform hammer;
    [SerializeField] private GameObject sparks;

    [Header("Body Sway")]

    [Min(0f)]
    [SerializeField] private float bodySwayAngle = 3f;

    [Min(0f)]
    [SerializeField] private float bodySwaySpeed = 8f;

    [Header("Hammer")]

    [SerializeField] private float hammerStartAngle = -25f;
    [SerializeField] private float hammerHitAngle = 35f;

    [Min(0.01f)]
    [SerializeField] private float repairCycleDuration = 0.7f;

    [Header("Sparks")]

    [Min(0f)]
    [SerializeField] private float sparksDuration = 0.1f;

    private CrewWalkSway walkSway;
    private Quaternion initialVisualRotation;
    private Quaternion initialHammerRotation;
    private float bodyTime;
    private float cycleTime;
    private float sparksTimeRemaining;
    private bool impactTriggered;
    private bool isRepairing;
    private bool hasCachedRotations;

    private void Awake()
    {
        walkSway = GetComponent<CrewWalkSway>();
        CacheInitialRotations();
        HideRepairEffects();
        ValidateReferences();
    }

    private void Update()
    {
        if (!isRepairing)
        {
            return;
        }

        AnimateBody();
        AnimateHammer();
        UpdateSparks();
    }

    public void SetRepairing(bool repairing)
    {
        if (isRepairing == repairing)
        {
            if (!repairing)
            {
                StopAndRestore();
            }

            return;
        }

        if (repairing)
        {
            if (!HasRequiredReferences())
            {
                ValidateReferences();
                return;
            }

            walkSway?.SetExternalRotationControl(true);
            CacheInitialRotations();
            isRepairing = true;
            bodyTime = 0f;
            cycleTime = 0f;
            sparksTimeRemaining = 0f;
            impactTriggered = false;

            repairVisual.SetActive(true);
            sparks.SetActive(false);
            ApplyHammerAngle(hammerStartAngle);
            return;
        }

        StopAndRestore();
    }

    private void AnimateBody()
    {
        if (visual == null || !hasCachedRotations)
        {
            return;
        }

        bodyTime += Time.deltaTime;
        float angle = Mathf.Sin(bodyTime * bodySwaySpeed) * bodySwayAngle;

        visual.localRotation =
            initialVisualRotation * Quaternion.Euler(0f, 0f, angle);
    }

    private void AnimateHammer()
    {
        float duration = Mathf.Max(0.01f, repairCycleDuration);
        cycleTime += Time.deltaTime;

        while (cycleTime >= duration)
        {
            cycleTime -= duration;
            impactTriggered = false;
        }

        float phase = cycleTime / duration;
        float strikeAmount = Mathf.Sin(phase * Mathf.PI);
        float angle = Mathf.Lerp(hammerStartAngle, hammerHitAngle, strikeAmount);

        ApplyHammerAngle(angle);

        if (!impactTriggered && phase >= 0.5f)
        {
            impactTriggered = true;
            sparksTimeRemaining = sparksDuration;
            sparks.SetActive(sparksDuration > 0f);
        }
    }

    private void UpdateSparks()
    {
        if (sparksTimeRemaining <= 0f)
        {
            return;
        }

        sparksTimeRemaining -= Time.deltaTime;

        if (sparksTimeRemaining <= 0f)
        {
            sparks.SetActive(false);
        }
    }

    private void ApplyHammerAngle(float angle)
    {
        if (hammer == null || !hasCachedRotations)
        {
            return;
        }

        hammer.localRotation =
            initialHammerRotation * Quaternion.Euler(0f, 0f, angle);
    }

    private void CacheInitialRotations()
    {
        hasCachedRotations = visual != null && hammer != null;

        if (!hasCachedRotations)
        {
            return;
        }

        initialVisualRotation = visual.localRotation;
        initialHammerRotation = hammer.localRotation;
    }

    private bool HasRequiredReferences()
    {
        return visual != null &&
               repairVisual != null &&
               hammer != null &&
               sparks != null;
    }

    private void ValidateReferences()
    {
        if (HasRequiredReferences())
        {
            return;
        }

        Debug.LogError(
            $"{name}: CrewRepairAnimation requires Visual, RepairVisual, Hammer and Sparks references.",
            this
        );
    }

    private void OnDisable()
    {
        StopAndRestore();
    }

    private void OnDestroy()
    {
        StopAndRestore();
    }

    private void StopAndRestore()
    {
        isRepairing = false;
        bodyTime = 0f;
        cycleTime = 0f;
        sparksTimeRemaining = 0f;
        impactTriggered = false;

        HideRepairEffects();

        if (hasCachedRotations)
        {
            if (visual != null)
            {
                visual.localRotation = initialVisualRotation;
            }

            if (hammer != null)
            {
                hammer.localRotation = initialHammerRotation;
            }
        }

        walkSway?.SetExternalRotationControl(false);
    }

    private void HideRepairEffects()
    {
        if (sparks != null)
        {
            sparks.SetActive(false);
        }

        if (repairVisual != null)
        {
            repairVisual.SetActive(false);
        }
    }
}
