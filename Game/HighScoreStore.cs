using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace SeaHunter.Game;

// Saves and loads the top scores as JSON in the user's AppData folder
public static class HighScoreStore
{
    public const int MaxEntries = 10;

    private static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "SeaHunter",
        "highscores.json");

    public static List<int> Load()
    {
        try
        {
            if (!File.Exists(FilePath)) return new();
            string json = File.ReadAllText(FilePath);
            return JsonSerializer.Deserialize<List<int>>(json) ?? new();
        }
        catch
        {
            return new(); // Corrupt or unreadable file: start fresh
        }
    }

    // Adds a score, keeps only the best ones, saves and returns the new list
    public static List<int> Add(int score)
    {
        var top = Load()
            .Append(score)
            .OrderByDescending(s => s)
            .Take(MaxEntries)
            .ToList();

        Save(top);
        return top;
    }

    // Always returns MaxEntries lines, with "-" for empty slots: " 1.  001200"
    public static List<string> Format(List<int> scores)
    {
        var lines = new List<string>();
        for (int i = 0; i < MaxEntries; i++)
        {
            string value = i < scores.Count ? scores[i].ToString("D6") : "------";
            lines.Add($"{i + 1,2}.  {value}");
        }
        return lines;
    }

    private static void Save(List<int> scores)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(scores));
        }
        catch
        {
            // Saving failed (e.g. no write access): the game keeps running anyway
        }
    }
}