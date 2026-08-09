using UnityEngine;

[DisallowMultipleComponent]
public sealed class ProjectileHitEffect : MonoBehaviour
{
    [Header("Visual")]

    [SerializeField] private SpriteRenderer spriteRenderer;

    [Min(0.01f)]
    [SerializeField] private float duration = 0.2f;

    [Range(0.01f, 0.99f)]
    [SerializeField] private float expansionPortion = 0.4f;

    [Header("Scale")]

    [Min(0f)]
    [SerializeField] private float startScale = 0.5f;

    [Min(0f)]
    [SerializeField] private float peakScale = 1.1f;

    [Min(0f)]
    [SerializeField] private float endScale = 0.9f;

    [Header("Alpha And Rotation")]

    [Range(0f, 1f)]
    [SerializeField] private float startAlpha = 1f;

    [Min(0f)]
    [SerializeField] private float randomRotationAngle = 20f;

    private Color initialColor;
    private float elapsedTime;

    private void Awake()
    {
        if (spriteRenderer == null)
        {
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        }

        if (spriteRenderer == null)
        {
            Debug.LogError(
                $"{name}: ProjectileHitEffect requires a SpriteRenderer.",
                this
            );
            Destroy(gameObject);
            return;
        }

        initialColor = spriteRenderer.color;
        initialColor.a = startAlpha;
        spriteRenderer.color = initialColor;

        transform.localScale = Vector3.one * startScale;
        transform.Rotate(
            0f,
            0f,
            Random.Range(-randomRotationAngle, randomRotationAngle)
        );
    }

    private void Update()
    {
        if (spriteRenderer == null)
        {
            return;
        }

        elapsedTime += Time.deltaTime;

        float normalizedTime = Mathf.Clamp01(
            elapsedTime / Mathf.Max(0.01f, duration)
        );

        float scale = EvaluateScale(normalizedTime);
        transform.localScale = Vector3.one * scale;

        Color color = initialColor;
        color.a = Mathf.Lerp(startAlpha, 0f, normalizedTime);
        spriteRenderer.color = color;

        if (normalizedTime >= 1f)
        {
            Destroy(gameObject);
        }
    }

    private float EvaluateScale(float normalizedTime)
    {
        float expandUntil = Mathf.Clamp(
            expansionPortion,
            0.01f,
            0.99f
        );

        if (normalizedTime <= expandUntil)
        {
            float expansionTime =
                Mathf.SmoothStep(
                    0f,
                    1f,
                    normalizedTime / expandUntil
                );

            return Mathf.Lerp(
                startScale,
                peakScale,
                expansionTime
            );
        }

        float contractionTime = Mathf.SmoothStep(
            0f,
            1f,
            (normalizedTime - expandUntil) /
            (1f - expandUntil)
        );

        return Mathf.Lerp(
            peakScale,
            endScale,
            contractionTime
        );
    }

    private void OnValidate()
    {
        duration = Mathf.Max(0.01f, duration);
        expansionPortion = Mathf.Clamp(
            expansionPortion,
            0.01f,
            0.99f
        );
        startScale = Mathf.Max(0f, startScale);
        peakScale = Mathf.Max(0f, peakScale);
        endScale = Mathf.Max(0f, endScale);
        randomRotationAngle = Mathf.Max(0f, randomRotationAngle);
    }
}
