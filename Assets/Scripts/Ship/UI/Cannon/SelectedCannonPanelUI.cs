using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(CanvasGroup))]
public sealed class SelectedCannonPanelUI : MonoBehaviour
{
    [Header("Source")]
    [Tooltip("Контроллер выбора пушек игрока.")]
    [SerializeField] private CannonTargetingController targetingController;

    [Header("Title")]
    [SerializeField] private TMP_Text cannonNameText;

    [Header("Reload")]
    [SerializeField] private Image reloadFill;
    [SerializeField] private TMP_Text reloadText;

    [Header("Ammo")]
    [Tooltip("Объект с Horizontal Layout Group, внутрь которого создаются кнопки.")]
    [SerializeField] private Transform ammoButtonsContainer;

    [Tooltip("Неактивный шаблон одной кнопки боеприпаса.")]
    [SerializeField] private CannonAmmoButtonUI ammoButtonTemplate;

    [Header("Runtime UI Theme")]
    [SerializeField] private Sprite buttonSprite;

    private readonly List<CannonAmmoButtonUI> ammoButtons =
        new List<CannonAmmoButtonUI>();

    private CanvasGroup canvasGroup;
    private CannonSystemRuntime currentCannon;

    private void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();

        ConfigureReloadFill();

        if (ammoButtonTemplate != null)
        {
            ammoButtonTemplate.gameObject.SetActive(false);
        }

        SetVisible(false);
    }

    private void Start()
    {
        if (targetingController == null)
        {
            targetingController =
                FindAnyObjectByType<CannonTargetingController>();
        }

        RefreshSelectedCannon();
    }

    private void Update()
    {
        RefreshSelectedCannon();

        if (currentCannon != null)
        {
            RefreshRuntimeState();
        }
    }

    private void RefreshSelectedCannon()
    {
        CannonSystemRuntime selectedCannon =
            targetingController != null
                ? targetingController.SelectedCannon
                : null;

        if (selectedCannon == currentCannon)
        {
            return;
        }

        SetCannon(selectedCannon);
    }

    private void SetCannon(CannonSystemRuntime cannon)
    {
        currentCannon = cannon;

        ClearAmmoButtons();

        if (currentCannon == null)
        {
            SetVisible(false);
            return;
        }

        BuildAmmoButtons();
        RefreshRuntimeState();
        SetVisible(true);
    }

    private void BuildAmmoButtons()
    {
        if (currentCannon == null ||
            ammoButtonsContainer == null ||
            ammoButtonTemplate == null)
        {
            return;
        }

        foreach (CannonAmmoDefinition ammo in currentCannon.AvailableAmmo)
        {
            if (ammo == null)
            {
                continue;
            }

            CannonAmmoButtonUI button =
                Instantiate(ammoButtonTemplate, ammoButtonsContainer);

            button.ApplyButtonSprite(buttonSprite);
            button.gameObject.SetActive(true);
            button.Bind(currentCannon, ammo);

            ammoButtons.Add(button);
        }
    }

    private void ClearAmmoButtons()
    {
        foreach (CannonAmmoButtonUI button in ammoButtons)
        {
            if (button != null)
            {
                Destroy(button.gameObject);
            }
        }

        ammoButtons.Clear();
    }

    private void RefreshRuntimeState()
    {
        if (currentCannon == null)
        {
            return;
        }

        RefreshTitle();
        RefreshReload();

        foreach (CannonAmmoButtonUI button in ammoButtons)
        {
            if (button != null)
            {
                button.Refresh();
            }
        }
    }

    private void RefreshTitle()
{
    if (cannonNameText == null)
    {
        return;
    }

    string roomId =
        currentCannon.Room != null
            ? currentCannon.Room.Id.ToString()
            : "?";

    cannonNameText.text =
        $"Пушка — отсек {roomId}";
}

    private void RefreshReload()
    {
        if (reloadFill != null)
        {
            reloadFill.fillAmount =
                currentCannon.ReloadProgress01;
        }

        if (reloadText == null)
        {
            return;
        }

        if (currentCannon.Room == null ||
            !currentCannon.Room.IsOperational)
        {
            reloadText.text =
                "Перезарядка остановлена";
            return;
        }

        if (!currentCannon.HasCurrentAmmo)
        {
            reloadText.text =
                "Нет выбранных боеприпасов";
            return;
        }

        if (currentCannon.IsLoaded)
        {
            reloadText.text =
                "Готова к выстрелу";
            return;
        }

        reloadText.text =
            $"Перезарядка: {currentCannon.ReloadRemaining:0.0} сек.";
    }

    private void ConfigureReloadFill()
    {
        if (reloadFill == null)
        {
            return;
        }

        reloadFill.type = Image.Type.Filled;
        reloadFill.fillMethod = Image.FillMethod.Horizontal;
        reloadFill.fillOrigin =
            (int)Image.OriginHorizontal.Left;
        reloadFill.fillClockwise = true;
    }

    private void SetVisible(bool visible)
    {
        if (canvasGroup == null)
        {
            return;
        }

        canvasGroup.alpha = visible ? 1f : 0f;
        canvasGroup.interactable = visible;
        canvasGroup.blocksRaycasts = visible;
    }

    private void OnValidate()
    {
        ConfigureReloadFill();
    }
}
