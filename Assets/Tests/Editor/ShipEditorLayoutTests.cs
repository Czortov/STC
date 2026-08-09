#if UNITY_EDITOR
using System.Linq;
using NUnit.Framework;

public sealed class ShipEditorLayoutTests
{
    private const string BrigHull =
        "240000000\n" +
        "132222422\n" +
        "111111310\n" +
        "001111300";

    private const string BrigLayout =
        "1;00000000\n" +
        "2;0;0050;0;00\n" +
        "030;030;0;20\n" +
        "00;6;6;6;6;000";

    [TestCase(0, 0, HullCellType.ExteriorRoom)]
    [TestCase(1, 0, HullCellType.ExteriorLadder)]
    [TestCase(0, 1, HullCellType.InteriorRoom)]
    [TestCase(1, 1, HullCellType.InteriorLadder)]
    [TestCase(2, 1, HullCellType.ExteriorRoom)]
    [TestCase(8, 3, HullCellType.None)]
    public void HullCells_AreDecodedWithoutVerticalFlip(
        int x,
        int y,
        HullCellType expected)
    {
        Assert.That(
            ShipEditorLayoutUtility.TryGetHullCell(BrigHull, x, y, out HullCellType actual),
            Is.True
        );
        Assert.That(actual, Is.EqualTo(expected));
    }

    [TestCase(-1, 0)]
    [TestCase(9, 0)]
    [TestCase(0, -1)]
    [TestCase(0, 4)]
    public void HullCells_RejectCoordinatesOutsideMatrix(int x, int y)
    {
        Assert.That(
            ShipEditorLayoutUtility.TryGetHullCell(BrigHull, x, y, out _),
            Is.False
        );
    }

    [TestCase(ShipModuleType.Rudder, '1')]
    [TestCase(ShipModuleType.Supplies, '2')]
    [TestCase(ShipModuleType.Cannon, '3')]
    [TestCase(ShipModuleType.OpenDeck, '4')]
    [TestCase(ShipModuleType.Sails, '5')]
    [TestCase(ShipModuleType.Bunks, '6')]
    [TestCase(ShipModuleType.None, '0')]
    public void ReplaceModule_UsesProjectCipher(
        ShipModuleType module,
        char expected)
    {
        string matrix = BrigLayout;
        Assert.That(
            ShipEditorLayoutUtility.TryReplaceModule(ref matrix, 2, 2, module),
            Is.True
        );
        string compact = ShipEditorLayoutUtility.GetRows(matrix)[2].Replace(";", string.Empty);
        Assert.That(compact[2], Is.EqualTo(expected));
    }

    [Test]
    public void ReplaceModule_PreservesRoomSeparators()
    {
        string matrix = BrigLayout;
        ShipEditorLayoutUtility.TryReplaceModule(ref matrix, 4, 1, ShipModuleType.Cannon);
        Assert.That(
            ShipEditorLayoutUtility.GetRows(matrix)[1].Count(c => c == ';'),
            Is.EqualTo(4)
        );
    }

    [TestCase(-1, 0)]
    [TestCase(9, 0)]
    [TestCase(0, -1)]
    [TestCase(0, 4)]
    public void ReplaceModule_RejectsCoordinatesOutsideMatrix(int x, int y)
    {
        string matrix = BrigLayout;
        Assert.That(
            ShipEditorLayoutUtility.TryReplaceModule(ref matrix, x, y, ShipModuleType.Cannon),
            Is.False
        );
        Assert.That(matrix, Is.EqualTo(BrigLayout));
    }

    [Test]
    public void ClearModules_PreservesDimensionsAndSeparators()
    {
        string cleared = ShipEditorLayoutUtility.ClearModules(BrigLayout);
        string[] originalRows = ShipEditorLayoutUtility.GetRows(BrigLayout);
        string[] clearedRows = ShipEditorLayoutUtility.GetRows(cleared);
        Assert.That(clearedRows.Length, Is.EqualTo(originalRows.Length));

        for (int y = 0; y < originalRows.Length; y++)
        {
            Assert.That(clearedRows[y].Length, Is.EqualTo(originalRows[y].Length));
            Assert.That(clearedRows[y].Count(c => c == ';'), Is.EqualTo(originalRows[y].Count(c => c == ';')));
            Assert.That(clearedRows[y].Replace(";", string.Empty), Does.Not.Contain("1"));
            Assert.That(clearedRows[y].Replace(";", string.Empty), Does.Not.Contain("6"));
        }
    }

    [Test]
    public void EmptyMatrix_MatchesHullDimensions()
    {
        string[] hullRows = ShipEditorLayoutUtility.GetRows(BrigHull);
        string[] emptyRows = ShipEditorLayoutUtility.GetRows(
            ShipEditorLayoutUtility.CreateEmptyMatrix(BrigHull)
        );
        Assert.That(emptyRows.Length, Is.EqualTo(4));

        for (int y = 0; y < hullRows.Length; y++)
        {
            Assert.That(emptyRows[y].Length, Is.EqualTo(hullRows[y].Length));
            Assert.That(emptyRows[y], Is.EqualTo("000000000"));
        }
    }
}
#endif
