using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class EnemyCard : MonoBehaviour
{
    [Header("Card role")]
    [SerializeField] private bool isPlayerCard;
    [SerializeField] private EnemyId enemyId = EnemyId.MerchantSloop;
    [SerializeField] private string battleSceneName = "BattleScene";

    [Header("Ship catalog")]
    [SerializeField] private ShipCatalogDefinition shipCatalog;

    private readonly List<ShipHullDefinition> hulls =
        new List<ShipHullDefinition>();
    private readonly List<ShipLayoutDefinition> currentLayouts =
        new List<ShipLayoutDefinition>();

    private TMP_Dropdown hullChoose;
    private TMP_Text chosenSections;
    private ShipCardPreview sectionsPreview;
    private Button nextButton;
    private Button previousButton;
    private Button attackButton;

    private ShipHullDefinition selectedHull;
    private ShipLayoutDefinition selectedLayout;
    private int layoutIndex;

    public ShipHullDefinition SelectedHull => selectedHull;
    public ShipLayoutDefinition SelectedLayout => selectedLayout;

    private void Awake()
    {
        CacheReferences();
        BuildHullList();
        BindControls();
        SelectInitialHull();
    }

    private void OnDestroy()
    {
        if (hullChoose != null)
        {
            hullChoose.onValueChanged.RemoveListener(SelectHullByIndex);
        }

        if (attackButton != null)
        {
            attackButton.onClick.RemoveListener(StartBattle);
        }
    }

    public void SelectNextLayout()
    {
        SelectLayout(layoutIndex + 1);
    }

    public void SelectPreviousLayout()
    {
        SelectLayout(layoutIndex - 1);
    }

    public void StartBattle()
    {
        if (isPlayerCard)
        {
            return;
        }

        if (enemyId == EnemyId.None)
        {
            Debug.LogError($"У карточки {gameObject.name} не выбран противник.", this);
            return;
        }

        if (!GameSession.HasCompleteShipSelection)
        {
            Debug.LogError(
                "Нельзя начать бой: не выбраны корпус и комплектация " +
                "для обоих кораблей.",
                this
            );
            return;
        }

        GameSession.SelectEnemy(enemyId);
        SceneManager.LoadScene(battleSceneName);
    }

    private void CacheReferences()
    {
        hullChoose = FindDescendant<TMP_Dropdown>("HullChoose");
        chosenSections = FindDescendant<TMP_Text>("ChoosedSections");
        nextButton = FindDescendant<Button>("NextButton");
        previousButton = FindDescendant<Button>("PreviousButton");

        Image previewImage = FindDescendant<Image>("SectionsPreview");

        if (previewImage != null)
        {
            sectionsPreview = previewImage.GetComponent<ShipCardPreview>();

            if (sectionsPreview == null)
            {
                sectionsPreview =
                    previewImage.gameObject.AddComponent<ShipCardPreview>();
            }
        }

        if (!isPlayerCard)
        {
            attackButton = FindDescendant<Button>("AtackButton");
        }
    }

    private void BuildHullList()
    {
        hulls.Clear();

        if (shipCatalog != null)
        {
            shipCatalog.ApplyToHulls();

            foreach (ShipCatalogEntry entry in shipCatalog.Entries)
            {
                ShipHullDefinition hull = entry?.Hull;

                if (hull != null && !hulls.Contains(hull))
                {
                    hulls.Add(hull);
                }
            }
        }

        if (hullChoose == null)
        {
            Debug.LogError($"В {gameObject.name} не найден HullChoose.", this);
            return;
        }

        hullChoose.ClearOptions();

        List<string> options = new List<string>();

        foreach (ShipHullDefinition hull in hulls)
        {
            options.Add(hull.HullName);
        }

        hullChoose.AddOptions(options);
        hullChoose.interactable = hulls.Count > 1;
    }

    private void BindControls()
    {
        if (hullChoose != null)
        {
            hullChoose.onValueChanged.AddListener(SelectHullByIndex);
        }

        if (attackButton != null)
        {
            attackButton.onClick.AddListener(StartBattle);
        }
    }

    private void SelectInitialHull()
    {
        if (hulls.Count == 0)
        {
            selectedHull = null;
            selectedLayout = null;
            UpdateView();
            Debug.LogError(
                $"Для карточки {gameObject.name} не назначены доступные корпуса.",
                this
            );
            return;
        }

        int selectedIndex = Mathf.Clamp(
            hullChoose != null ? hullChoose.value : 0,
            0,
            hulls.Count - 1
        );

        SelectHullByIndex(selectedIndex);
    }

    private void SelectHullByIndex(int index)
    {
        if (index < 0 || index >= hulls.Count)
        {
            return;
        }

        selectedHull = hulls[index];
        layoutIndex = 0;
        currentLayouts.Clear();

        if (shipCatalog != null &&
            shipCatalog.TryGetLayouts(
                selectedHull,
                out IReadOnlyList<ShipLayoutDefinition> layouts))
        {
            for (int i = 0; i < layouts.Count; i++)
            {
                if (layouts[i] != null)
                {
                    currentLayouts.Add(layouts[i]);
                }
            }
        }

        if (hullChoose != null)
        {
            hullChoose.SetValueWithoutNotify(index);
            hullChoose.RefreshShownValue();
        }

        SelectLayout(layoutIndex);
    }

    private void SelectLayout(int requestedIndex)
    {
        int layoutCount = currentLayouts.Count;

        if (layoutCount == 0)
        {
            selectedLayout = null;
            layoutIndex = 0;
            UpdateView();
            return;
        }

        layoutIndex = (requestedIndex % layoutCount + layoutCount) % layoutCount;
        selectedLayout = currentLayouts[layoutIndex];

        UpdateView();
    }

    private void UpdateView()
    {
        if (chosenSections != null)
        {
            chosenSections.text = selectedLayout != null
                ? selectedLayout.LayoutName
                : "Нет доступных комплектаций";
        }

        bool canCycle = currentLayouts.Count > 1;

        if (nextButton != null)
        {
            nextButton.interactable = canCycle;
        }

        if (previousButton != null)
        {
            previousButton.interactable = canCycle;
        }

        if (sectionsPreview != null)
        {
            sectionsPreview.Show(selectedHull, selectedLayout);
        }

        if (isPlayerCard)
        {
            GameSession.SelectPlayerShip(selectedHull, selectedLayout);
        }
        else
        {
            GameSession.SelectEnemyShip(selectedHull, selectedLayout);
        }
    }

    private T FindDescendant<T>(string objectName) where T : Component
    {
        T[] components = GetComponentsInChildren<T>(true);

        foreach (T component in components)
        {
            if (component.gameObject.name == objectName)
            {
                return component;
            }
        }

        return null;
    }
}
