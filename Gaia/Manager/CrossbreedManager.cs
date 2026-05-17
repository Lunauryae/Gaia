using System.IO;

namespace Gaia.Manager;

public class CrossbreedManager
{
    private readonly Dictionary<string, string> _crosses = new();
    public bool IsLoaded { get; private set; } = false;

    // Now it takes the exact path to the CSV!
    public CrossbreedManager(string csvFilePath)
    {
        if (File.Exists(csvFilePath)) LoadCsv(csvFilePath);
    }

    private void LoadCsv(string path)
    {
        try
        {
            string content = File.ReadAllText(path);
            var rows = ParseCsv(content); // Uses our custom safe-parser below
            if (rows.Count < 2) return;

            var headers = rows[0];

            for (int i = 1; i < rows.Count; i++)
            {
                var cols = rows[i];
                if (cols.Length == 0) continue;

                string seedA = NormalizeName(cols[0]);
                if (string.IsNullOrEmpty(seedA)) continue;

                // Loop through columns and map them to the headers
                for (int j = 1; j < cols.Length && j < headers.Length; j++)
                {
                    string seedB = NormalizeName(headers[j]);
                    string result = cols[j].Trim();

                    // Filter out dead/empty spreadsheet cells
                    if (!string.IsNullOrEmpty(seedB) && !string.IsNullOrEmpty(result) && result != "X" && result != "Dead" && !result.Contains("Round:"))
                    {
                        // Clean up Google Sheets line breaks
                        result = result.Replace("\n", " / ").Replace("\r", "");

                        string key = GetKey(seedA, seedB);
                        if (!_crosses.ContainsKey(key))
                        {
                            _crosses[key] = result;
                        }
                    }
                }
            }
            IsLoaded = true;
        }
        catch (Exception) { /* Fail silently if CSV is bad */ }
    }

    // A bulletproof CSV parser that handles quotes and newlines inside cells
    private List<string[]> ParseCsv(string content)
    {
        var result = new List<string[]>();
        var currentLine = new List<string>();
        var currentValue = new System.Text.StringBuilder();
        bool inQuotes = false;

        for (int i = 0; i < content.Length; i++)
        {
            char c = content[i];

            if (c == '\"')
            {
                if (inQuotes && i + 1 < content.Length && content[i + 1] == '\"')
                {
                    currentValue.Append('\"');
                    i++;
                }
                else inQuotes = !inQuotes;
            }
            else if (c == ',' && !inQuotes)
            {
                currentLine.Add(currentValue.ToString().Trim());
                currentValue.Clear();
            }
            else if ((c == '\n' || c == '\r') && !inQuotes)
            {
                if (c == '\r' && i + 1 < content.Length && content[i + 1] == '\n') i++;

                currentLine.Add(currentValue.ToString().Trim());
                currentValue.Clear();
                result.Add(currentLine.ToArray());
                currentLine.Clear();
            }
            else currentValue.Append(c);
        }

        if (currentValue.Length > 0 || currentLine.Count > 0)
        {
            currentLine.Add(currentValue.ToString().Trim());
            result.Add(currentLine.ToArray());
        }
        return result;
    }

    private string NormalizeName(string name)
    {
        name = name.Trim();

        // The Translation Book: Fix discrepancies between FFXIV's exact names and the CSV's shorthand
        if (name.Equals("Almonds", StringComparison.OrdinalIgnoreCase)) return "Almond";
        if (name.Equals("Coerthan Tea Leaves", StringComparison.OrdinalIgnoreCase)) return "Coerthan Tea";
        if (name.Equals("Royal Kukuru Bean", StringComparison.OrdinalIgnoreCase)) return "Royal Kukuru"; // <--- THE MAGIC FIX!

        return name;
    }

    private string GetKey(string a, string b)
    {
        // Alphabetize so SeedA+SeedB is always exactly the same as SeedB+SeedA
        int compare = string.Compare(a, b, StringComparison.OrdinalIgnoreCase);
        return compare < 0 ? $"{a}|{b}" : $"{b}|{a}";
    }

    public string GetCross(string seedA, string seedB)
    {
        seedA = NormalizeName(seedA);
        seedB = NormalizeName(seedB);
        string key = GetKey(seedA, seedB);
        return _crosses.TryGetValue(key, out var res) ? res : "Unknown / None";
    }
}