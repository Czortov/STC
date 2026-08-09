using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class ShipEditorCell : MonoBehaviour,
    IPointerClickHandler,
    IPointerEnterHandler,
    IPointerExitHandler
{
    private static readonly Color AvailableHover =
        new Color(0.25f, 0.85f, 0.55f, 0.45f);
    private static readonly Color ReplaceHover =
        new Color(1f, 0.72f, 0.2f, 0.5f);
    private static readonly Color LockedHover =
        new Color(0.9f, 0.25f, 0.25f, 0.35f);

    private Image overlay;
    private ShipEditorPreview owner;

    public int X { get; private set; }
    public int Y { get; private set; }
    public HullCellType HullType { get; private set; }
    public ShipModuleType ModuleType { get; private set; }
    public bool IsEditable => HullType.IsRoom();

    public void Initialize(
        ShipEditorPreview preview,
        int x,
        int y,
        HullCellType hullType,
        ShipModuleType moduleType,
        Image hoverOverlay)
    {
        owner = preview;
        X = x;
        Y = y;
        HullType = hullType;
        ModuleType = moduleType;
        overlay = hoverOverlay;
        SetHover(false);
    }

    public void SetModule(ShipModuleType moduleType)
    {
        ModuleType = moduleType;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Right)
        {
            owner.RemoveModule(this);
        }
        else if (eventData.button == PointerEventData.InputButton.Left)
        {
            owner.PlaceSelectedModule(this);
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        SetHover(true);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        SetHover(false);
    }

    private void SetHover(bool visible)
    {
        if (overlay == null)
        {
            return;
        }

        overlay.enabled = visible;

        if (!visible)
        {
            return;
        }

        overlay.color = !IsEditable
            ? LockedHover
            : ModuleType == ShipModuleType.None
                ? AvailableHover
                : ReplaceHover;
    }
}
