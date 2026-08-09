using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class ShipEditorModuleEntry : MonoBehaviour
{
    private ShipModuleType moduleType;
    private ShipEditorController owner;
    private Image background;

    public ShipModuleType ModuleType => moduleType;

    public void Initialize(
        ShipEditorController controller,
        ShipModuleType type,
        Sprite icon,
        string displayName,
        Image backgroundImage,
        Image iconImage,
        TMP_Text nameText,
        Button button)
    {
        owner = controller;
        moduleType = type;
        background = backgroundImage;
        iconImage.sprite = icon;
        iconImage.color = icon != null
            ? Color.white
            : new Color(0.45f, 0.45f, 0.45f, 1f);
        nameText.text = displayName;
        button.onClick.AddListener(Select);
        SetSelected(false);
    }

    public void SetSelected(bool selected)
    {
        if (background != null)
        {
            background.color = selected
                ? new Color(0.95f, 0.68f, 0.22f, 1f)
                : new Color(0.17f, 0.22f, 0.28f, 0.96f);
        }
    }

    private void Select()
    {
        owner.SelectModule(moduleType);
    }
}
