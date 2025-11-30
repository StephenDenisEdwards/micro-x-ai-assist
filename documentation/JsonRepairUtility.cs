using System;
using System.IO;
using System.Text;
using System.Text.Json;

public static class JsonRepairUtility
{
    public static int Run(string directory)
    {
        if (!Directory.Exists(directory))
        {
            Console.WriteLine($"Directory not found: {directory}");
            return 1;
        }

        var files = Directory.GetFiles(directory, "*.json", SearchOption.TopDirectoryOnly);
        int repaired = 0;
        foreach (var file in files)
        {
            if (file.EndsWith("_REPAIRED.json", StringComparison.OrdinalIgnoreCase))
                continue; // skip already repaired

            var content = File.ReadAllText(file);
            string target = Path.Combine(Path.GetDirectoryName(file)!, Path.GetFileNameWithoutExtension(file) + "_REPAIRED.json");

            // Enforce CORRECT-JSON schema: { "answer": string, "console_code": string }
            var repairedObj = RepairToSchema(content);
            var output = JsonSerializer.Serialize(repairedObj, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(target, output);
            Console.WriteLine($"[SCHEMA] {Path.GetFileName(file)} -> {Path.GetFileName(target)}");
            repaired++;
        }

        Console.WriteLine($"Completed. Repaired/Wrapped: {repaired} file(s).");
        return 0;
    }

    private static bool TryParseObject(string input, out JsonElement obj)
    {
        try
        {
            obj = JsonSerializer.Deserialize<JsonElement>(input);
            if (obj.ValueKind == JsonValueKind.Object) return true;
        }
        catch { }
        obj = default;
        return false;
    }

    private static string RepairStringNewlinesInsideJson(string input)
    {
        var sb = new StringBuilder(input.Length * 2);
        bool inString = false;
        bool escape = false;
        foreach (var ch in input)
        {
            if (inString)
            {
                if (escape)
                {
                    sb.Append(ch);
                    escape = false;
                    continue;
                }
                if (ch == '\\')
                {
                    sb.Append(ch);
                    escape = true;
                    continue;
                }
                if (ch == '"')
                {
                    sb.Append(ch);
                    inString = false;
                    continue;
                }
                if (ch == '\r')
                    continue; // drop CR
                if (ch == '\n')
                {
                    sb.Append("\\n");
                    continue;
                }
                sb.Append(ch);
            }
            else
            {
                if (ch == '"')
                {
                    inString = true;
                    sb.Append(ch);
                }
                else
                {
                    sb.Append(ch);
                }
            }
        }
        // If we ended while still inside a string, close it.
        if (inString)
        {
            sb.Append('"');
        }
        return sb.ToString();
    }

    private static string EscapeForJsonString(string raw)
    {
        var sb = new StringBuilder(raw.Length * 2);
        foreach (var ch in raw)
        {
            switch (ch)
            {
                case '"': sb.Append("\\\""); break;
                case '\\': sb.Append("\\\\"); break;
                case '\r': break; // ignore CR, normalize to \n
                case '\n': sb.Append("\\n"); break;
                case '\t': sb.Append("\\t"); break;
                default:
                    if (char.IsControl(ch))
                    {
                        sb.Append("\\u");
                        sb.Append(((int)ch).ToString("x4"));
                    }
                    else sb.Append(ch);
                    break;
            }
        }
        return sb.ToString();
    }

    private static object RepairToSchema(string raw)
    {
        // First try parse as object and read fields.
        if (TryParseObject(raw, out var obj))
        {
            string answer = null;
            string console = null;
            if (obj.TryGetProperty("answer", out var a) && a.ValueKind == JsonValueKind.String)
            {
                answer = a.GetString();
            }
            if (obj.TryGetProperty("console_code", out var c) && c.ValueKind == JsonValueKind.String)
            {
                console = c.GetString();
            }
            if (answer == null)
            {
                // If there's only one property and it's a string, treat it as answer.
                foreach (var prop in obj.EnumerateObject())
                {
                    if (prop.Value.ValueKind == JsonValueKind.String)
                    {
                        answer = prop.Value.GetString();
                        break;
                    }
                }
            }
            if (string.IsNullOrEmpty(console)) console = "no-code";
            if (string.IsNullOrEmpty(answer))
            {
                // Could not extract answer; fall back to raw
                answer = raw;
            }
            return new { answer = answer, console_code = console };
        }

        // If parsing fails, try to repair newlines inside JSON and reparse.
        var repairedText = RepairStringNewlinesInsideJson(raw);
        if (TryParseObject(repairedText, out var obj2))
        {
            string answer = null;
            string console = null;
            if (obj2.TryGetProperty("answer", out var a2) && a2.ValueKind == JsonValueKind.String)
            {
                answer = a2.GetString();
            }
            if (obj2.TryGetProperty("console_code", out var c2) && c2.ValueKind == JsonValueKind.String)
            {
                console = c2.GetString();
            }
            if (string.IsNullOrEmpty(console)) console = "no-code";
            if (string.IsNullOrEmpty(answer)) answer = raw;
            return new { answer = answer, console_code = console };
        }

        // Final fallback: treat the raw file content as the answer string.
        return new { answer = raw, console_code = "no-code" };
    }
}

public class Program
{
    public static int Main(string[] args)
    {
        string directory = args.Length > 0 ? args[0] : Path.Combine(Directory.GetCurrentDirectory(), "architecture", "JSON-samples");
        return JsonRepairUtility.Run(directory);
    }
}
