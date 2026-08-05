using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(Button))]
public sealed class CannonAmmoButtonUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Image background;
    [SerializeField] private Image ammoIcon;
    [SerializeField] private TMP_Text ammoNameText;
    [SerializeField] private TMP_Text ammoCountText;

    [Header("Colors")]
    [SerializeField] private Color normalColor =
        new Color(0.22f, 0.22f, 0.22f, 1f);

    [SerializeField] private Color selectedColor =
        new Color(0.85f, 0.62f, 0.18f, 1f);

    [SerializeField] private Color unavailableColor =
        new Color(0.16f, 0.16f, 0.16f, 0.65f);

    private Button button;
    private CannonSystemRuntime cannon;
    private CannonAmmoDefinition ammo;

    public CannonAmmoDefinition Ammo => ammo;

    private void Awake()
    {
        button = GetComponent<Button>();

        if (background == null)
        {
            background = GetComponent<Image>();
        }

        button.onClick.AddListener(HandleClick);
    }

    private void OnDestroy()
    {
        if (button != null)
        {
            button.onClick.RemoveListener(HandleClick);
        }
    }

    public void Bind(
        CannonSystemRuntime targetCannon,
        CannonAmmoDefinition ammoDefinition)
    {
        cannon = targetCannon;
        ammo = ammoDefinition;

        Refresh();
    }

    public void Refresh()
    {
        if (button == null)
        {
            button = GetComponent<Button>();
        }

        if (cannon == null || ammo == null)
        {
            button.interactable = false;

            if (ammoNameText != null)
            {
                ammoNameText.text = "Нет боеприпаса";
            }

            if (ammoCountText != null)
            {
                ammoCountText.text = "0 / 0";
            }

            if (ammoIcon != null)
            {
                ammoIcon.enabled = false;
            }

            if (background != null)
            {
                background.color = unavailableColor;
            }

            return;
        }

        int currentCount = cannon.GetAmmoCount(ammo);
        int maximumCount = cannon.GetMaximumAmmoCount(ammo);

        bool isSelected = cannon.LoadedAmmo == ammo;
        bool isAvailable = currentCount > 0;

        button.interactable = isAvailable;

        if (ammoNameText != null)
        {
            ammoNameText.text = ammo.DisplayName;
        }

        if (ammoCountText != null)
        {
            ammoCountText.text = $"{currentCount} / {maximumCount}";
        }

        if (ammoIcon != null)
        {
            ammoIcon.sprite = ammo.Icon;
            ammoIcon.enabled = ammo.Icon != null;
        }

        if (background != null)
        {
            background.color = !isAvailable
                ? unavailableColor
                : isSelected
                    ? selectedColor
                    : normalColor;
        }
    }

    private void HandleClick()
    {
        if (cannon == null ||
            ammo == null ||
            cannon.GetAmmoCount(ammo) <= 0)
        {
            return;
        }

        cannon.SetAmmo(ammo);
        Refresh();
    }
}