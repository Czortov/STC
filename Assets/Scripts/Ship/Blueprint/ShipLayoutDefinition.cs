using UnityEngine;

[CreateAssetMenu(
    fileName = "NewShipLayout",
    menuName = "Pirates/Ships/Layout Definition")]
public sealed class ShipLayoutDefinition : ScriptableObject
{
    [SerializeField] private string layoutName = "Combat Layout";

    [Tooltip(
        "0 — без изменений, 1 — руль, 2 — припасы, " +
        "3 — пушка, 4 — палуба, 5 — паруса, 6 — койки. " +
        "Точка с запятой разделяет отсеки.")]
    [TextArea(4, 12)]
    [SerializeField] private string roomMatrix =
        "1;00000000\n" +
        "2;0;0050;0;00\n" +
        "030;030;0;20\n" +
        "00;6;6;6;6;000";

    public string LayoutName => layoutName;
    public string RoomMatrix => roomMatrix;
}