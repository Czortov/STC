using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(Button))]
public sealed class CannonAmmoButtonUI : MonoBehaviour
{
    private static readonly Color SelectedTextColor =
        new Color(1f, 0.78f, 0.25f, 1f);

    private static readonly Color UnavailableTextColor =
        new Color(0.55f, 0.55f, 0.55f, 1f);

    [Header("References")]
    [SerializeField] private Image background;
    [SerializeField] private Image ammoIcon;
    [SerializeField] private TMP_Text ammoNameText;
    [SerializeField] private TMP_Text ammoCountText;

    private Button button;
    private CannonSystemRuntime cannon;
    private CannonAmmoDefinition ammo;

    public CannonAmmoDefinition Ammo => ammo;

    internal void ApplyButtonSprite(Sprite sprite)
    {
        if (sprite == null)
        {
            return;
        }

        if (background == null)
        {
            background = GetComponent<Image>();
        }

        if (background == null)
        {
            background = gameObject.AddComponent<Image>();
        }

        if (button == null)
        {
            button = GetComponent<Button>();
        }

        RuntimeUiVisuals.ApplyUndimmedButton(button, background, sprite);
    }

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
                background.color = Color.white;
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
            ammoNameText.color = !isAvailable
                ? UnavailableTextColor
                : isSelected
                    ? SelectedTextColor
                    : Color.white;
        }

        if (ammoCountText != null)
        {
            ammoCountText.text = $"{currentCount} / {maximumCount}";
            ammoCountText.color = !isAvailable
                ? UnavailableTextColor
                : isSelected
                    ? SelectedTextColor
                    : Color.white;
        }

        if (ammoIcon != null)
        {
            ammoIcon.sprite = ammo.Icon;
            ammoIcon.enabled = ammo.Icon != null;
            ammoIcon.color = Color.white;
            ammoIcon.preserveAspect = true;
        }

        if (background != null)
        {
            background.color = Color.white;
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
