using System.Collections.Generic;
using UnityEngine;

public static class CrewNameGenerator
{
    public const string DefaultName = "Безымянный матрос";

    private const string NamesResourcePath = "Names";
    private const string PirateNamesHeader = "ПИРАТЫ — ИМЕНА";
    private const string PirateNicknamesHeader = "ПИРАТЫ — ПРОЗВИЩА";
    private const string EnemyNamesHeader = "БРИТАНЦЫ — ИМЕНА";
    private const string EnemySurnamesHeader = "БРИТАНЦЫ — ФАМИЛИИ";

    private static readonly List<string> pirateNames = new List<string>();
    private static readonly List<string> pirateNicknames = new List<string>();
    private static readonly List<string> enemyNames = new List<string>();
    private static readonly List<string> enemySurnames = new List<string>();

    private static bool isLoaded;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset()
    {
        pirateNames.Clear();
        pirateNicknames.Clear();
        enemyNames.Clear();
        enemySurnames.Clear();
        isLoaded = false;
    }

    public static string Generate(ShipTeam team)
    {
        EnsureLoaded();

        if (team == ShipTeam.Player)
        {
            if (TryGetRandom(pirateNames, out string firstName) &&
                TryGetRandom(pirateNicknames, out string nickname))
            {
                return $"{firstName} \"{nickname}\"";
            }

            return DefaultName;
        }

        if (TryGetRandom(enemyNames, out string enemyFirstName) &&
            TryGetRandom(enemySurnames, out string surname))
        {
            return $"{enemyFirstName} {surname}";
        }

        return DefaultName;
    }

    private static void EnsureLoaded()
    {
        if (isLoaded)
        {
            return;
        }

        isLoaded = true;

        TextAsset namesAsset = Resources.Load<TextAsset>(NamesResourcePath);

        if (namesAsset == null)
        {
            Debug.LogWarning(
                $"Crew names file Resources/{NamesResourcePath}.txt was not found. " +
                $"The fallback name '{DefaultName}' will be used."
            );
            return;
        }

        Parse(namesAsset.text);
    }

    private static void Parse(string contents)
    {
        List<string> currentList = null;
        string[] lines = contents.Split('\n');

        foreach (string sourceLine in lines)
        {
            string line = sourceLine.Trim().TrimStart('\uFEFF');

            if (string.IsNullOrEmpty(line))
            {
                continue;
            }

            switch (line)
            {
                case PirateNamesHeader:
                    currentList = pirateNames;
                    continue;
                case PirateNicknamesHeader:
                    currentList = pirateNicknames;
                    continue;
                case EnemyNamesHeader:
                    currentList = enemyNames;
                    continue;
                case EnemySurnamesHeader:
                    currentList = enemySurnames;
                    continue;
            }

            currentList?.Add(line);
        }
    }

    private static bool TryGetRandom(
        IReadOnlyList<string> values,
        out string value)
    {
        if (values == null || values.Count == 0)
        {
            value = null;
            return false;
        }

        value = values[Random.Range(0, values.Count)];
        return true;
    }
}
