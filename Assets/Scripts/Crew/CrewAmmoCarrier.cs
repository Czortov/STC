using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(CrewUnit))]
public sealed class CrewAmmoCarrier : MonoBehaviour
{
    [Header("Visual")]

    [Tooltip(
        "Необязательный дочерний объект, который отображается, " +
        "пока пират несёт ящик с боеприпасами.")]
    [SerializeField]
    private GameObject carriedBoxVisual;

    private CrewUnit crewUnit;
    private ShipCellView lastProcessedCell;

    public bool HasAmmoBox { get; private set; }

    private void Awake()
    {
        crewUnit = GetComponent<CrewUnit>();
        UpdateVisual();
    }

    private void Update()
    {
        if (crewUnit == null ||
            crewUnit.IsMoving ||
            crewUnit.CurrentCell == null)
        {
            lastProcessedCell = null;
            return;
        }

        ShipCellView currentCell =
            crewUnit.CurrentCell;

        if (currentCell == lastProcessedCell)
        {
            return;
        }

        lastProcessedCell = currentCell;

        if (!HasAmmoBox)
        {
            TryPickUpAmmoBox(currentCell);
            return;
        }

        TryDeliverAmmoBox(currentCell);
    }

    private void TryPickUpAmmoBox(
        ShipCellView currentCell)
    {
        if (currentCell.ModuleType !=
            ShipModuleType.Supplies)
        {
            return;
        }

        HasAmmoBox = true;
        UpdateVisual();

        Debug.Log(
            $"{name} взял ящик с боеприпасами.",
            this
        );
    }

    private void TryDeliverAmmoBox(
        ShipCellView currentCell)
    {
        ShipRoomRuntime room =
            currentCell.Room;

        if (room == null)
        {
            return;
        }

        CannonSystemRuntime cannon =
            room.GetComponent<CannonSystemRuntime>();

        if (cannon == null)
        {
            return;
        }

        // Полная пушка не забирает ящик.
        if (!cannon.RefillAllAmmo())
        {
            return;
        }

        HasAmmoBox = false;
        UpdateVisual();

        Debug.Log(
            $"{name} пополнил весь боезапас пушки " +
            $"в отсеке {room.Id}.",
            this
        );
    }

    private void UpdateVisual()
    {
        if (carriedBoxVisual != null)
        {
            carriedBoxVisual.SetActive(
                HasAmmoBox
            );
        }
    }
}