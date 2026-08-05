using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class ShipHullHealthUI : MonoBehaviour
{
    [Header("Target")]

    [Tooltip("Компонент общего здоровья отображаемого корабля.")]
    [SerializeField] private ShipHullHealth targetHull;

    [Header("UI")]

    [Tooltip("Цветная полоса здоровья.")]
    [SerializeField] private Image healthFill;

    [Tooltip("Текст текущего и максимального здоровья.")]
    [SerializeField] private TMP_Text healthText;

    [Tooltip("Необязательный текст с названием корабля.")]
    [SerializeField] private TMP_Text shipNameText;

    [Header("Display")]

    [Tooltip(
        "Полоса уменьшается справа налево. " +
        "Удобно для интерфейса в правом углу.")]
    [SerializeField] private bool fillFromRight;

    [Tooltip(
        "Название, которое будет показано над полосой. " +
        "Если пусто, используется имя из ShipIdentity.")]
    [SerializeField] private string customDisplayName;

    private ShipIdentity shipIdentity;

    private void Awake()
    {
        ConfigureFillDirection();

        if (targetHull == null)
        {
            Debug.LogError(
                $"{name}: не назначен Target Hull.",
                this
            );
        }
    }

    private void OnEnable()
    {
        Subscribe();
        Refresh();
    }

    private void Start()
    {
        ConfigureFillDirection();
        Refresh();
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    public void SetTarget(
        ShipHullHealth newTargetHull)
    {
        if (targetHull == newTargetHull)
        {
            Refresh();
            return;
        }

        Unsubscribe();

        targetHull = newTargetHull;

        Subscribe();
        Refresh();
    }

    private void Subscribe()
    {
        if (targetHull == null)
        {
            return;
        }

        targetHull.HealthChanged -= HandleHealthChanged;
        targetHull.HealthChanged += HandleHealthChanged;

        targetHull.Destroyed -= HandleDestroyed;
        targetHull.Destroyed += HandleDestroyed;

        shipIdentity =
            targetHull.GetComponent<ShipIdentity>();
    }

    private void Unsubscribe()
    {
        if (targetHull == null)
        {
            return;
        }

        targetHull.HealthChanged -= HandleHealthChanged;
        targetHull.Destroyed -= HandleDestroyed;
    }

    private void HandleHealthChanged(
        ShipHullHealth hull,
        int currentHealth,
        int maxHealth)
    {
        UpdateHealthDisplay(
            currentHealth,
            maxHealth
        );
    }

    private void HandleDestroyed(
        ShipHullHealth hull)
    {
        UpdateHealthDisplay(
            0,
            hull.MaxHealth
        );
    }

    private void Refresh()
    {
        if (targetHull == null)
        {
            SetEmptyDisplay();
            return;
        }

        shipIdentity =
            targetHull.GetComponent<ShipIdentity>();

        UpdateHealthDisplay(
            targetHull.CurrentHealth,
            targetHull.MaxHealth
        );

        UpdateShipName();
    }

    private void UpdateHealthDisplay(
        int currentHealth,
        int maxHealth)
    {
        int safeMaxHealth =
            Mathf.Max(1, maxHealth);

        int safeCurrentHealth =
            Mathf.Clamp(
                currentHealth,
                0,
                safeMaxHealth
            );

        float healthRatio =
            (float)safeCurrentHealth /
            safeMaxHealth;

        if (healthFill != null)
        {
            healthFill.fillAmount =
                healthRatio;
        }

        if (healthText != null)
        {
            healthText.text =
                $"{safeCurrentHealth} / {safeMaxHealth}";
        }
    }

    private void UpdateShipName()
    {
        if (shipNameText == null)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(
                customDisplayName))
        {
            shipNameText.text =
                customDisplayName;

            return;
        }

        if (shipIdentity != null &&
            !string.IsNullOrWhiteSpace(
                shipIdentity.ShipName))
        {
            shipNameText.text =
                shipIdentity.ShipName;

            return;
        }

        shipNameText.text =
            targetHull != null
                ? targetHull.name
                : "Корабль";
    }

    private void ConfigureFillDirection()
    {
        if (healthFill == null)
        {
            return;
        }

        healthFill.type =
            Image.Type.Filled;

        healthFill.fillMethod =
            Image.FillMethod.Horizontal;

        healthFill.fillOrigin =
            fillFromRight
                ? (int)Image.OriginHorizontal.Right
                : (int)Image.OriginHorizontal.Left;

        healthFill.fillClockwise = true;
    }

    private void SetEmptyDisplay()
    {
        if (healthFill != null)
        {
            healthFill.fillAmount = 0f;
        }

        if (healthText != null)
        {
            healthText.text = "— / —";
        }

        if (shipNameText != null)
        {
            shipNameText.text = "Корабль";
        }
    }

    private void OnValidate()
    {
        ConfigureFillDirection();
    }
}