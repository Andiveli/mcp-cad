using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace McpCad.Tools;

/// <summary>
/// File I/O + parameter substitution for the template system.
/// Templates are JSON files under ./templates/ (repo root or cwd) compatible with macro_god_part.
/// Substitution uses regex replacement for ${PARAM} (overrides take precedence over template defaults).
/// </summary>
public static class TemplateManager
{
    /// <summary>
    /// Base directory for templates. Created on first use.
    /// </summary>
    public static string TemplateDir
    {
        get
        {
            var dir = Path.Combine(Directory.GetCurrentDirectory(), "templates");
            Directory.CreateDirectory(dir);
            return dir;
        }
    }

    private static string TemplatePath(string name) =>
        Path.Combine(TemplateDir, SanitizeFileName(name) + ".json");

    private static string SanitizeFileName(string name) =>
        string.Concat(name.Where(c => !Path.GetInvalidFileNameChars().Contains(c)));

    /// <summary>
    /// Save a template (as JsonElement) to disk.
    /// </summary>
    public static bool Save(string name, JsonElement template)
    {
        if (string.IsNullOrWhiteSpace(name))
            return false;

        try
        {
            var path = TemplatePath(name);
            var json = JsonSerializer.Serialize(template, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(path, json);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Load a template by name. Returns null if not found or invalid.
    /// </summary>
    public static JsonElement? Load(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return null;

        var path = TemplatePath(name);
        if (!File.Exists(path))
            return null;

        try
        {
            var txt = File.ReadAllText(path);
            return JsonSerializer.Deserialize<JsonElement>(txt);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// List available template names (without .json).
    /// </summary>
    public static string[] List()
    {
        try
        {
            var dir = TemplateDir;
            if (!Directory.Exists(dir))
                return Array.Empty<string>();

            return Directory.GetFiles(dir, "*.json")
                .Select(p => Path.GetFileNameWithoutExtension(p))
                .OrderBy(n => n)
                .ToArray();
        }
        catch
        {
            return Array.Empty<string>();
        }
    }

    /// <summary>
    /// Delete a template by name.
    /// </summary>
    public static bool Delete(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return false;

        var path = TemplatePath(name);
        if (!File.Exists(path))
            return false;

        try
        {
            File.Delete(path);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Substitute ${KEY} placeholders inside the config (string values) using overrides (preferred) then defaults.
    /// Returns a new JsonElement with replacements applied (text-based on serialized form for embedded expr support).
    /// </summary>
    public static JsonElement Substitute(JsonElement config, Dictionary<string, object?>? overrides = null, Dictionary<string, object?>? defaults = null)
    {
        overrides ??= new();
        defaults ??= new();

        try
        {
            string jsonText = JsonSerializer.Serialize(config);

            // Replace full JSON string tokens "${KEY}" with the serialized value (handles numbers/strings correctly in JSON)
            foreach (var kv in overrides.Concat(defaults))
            {
                if (kv.Value is null) continue;
                string token = $"\"${{{kv.Key}}}\"";
                string replacement = JsonSerializer.Serialize(kv.Value);
                jsonText = jsonText.Replace(token, replacement);
            }

            // Also handle bare ${KEY} inside strings (e.g. "radius": "${OD}/2" -> "60/2")
            jsonText = Regex.Replace(jsonText, @"""\$\{(\w+)\}""", match =>
            {
                var key = match.Groups[1].Value;
                if (overrides.TryGetValue(key, out var ov) && ov != null)
                    return JsonSerializer.Serialize(ov);
                if (defaults.TryGetValue(key, out var dv) && dv != null)
                    return JsonSerializer.Serialize(dv);
                return match.Value;
            });

            return JsonSerializer.Deserialize<JsonElement>(jsonText);
        }
        catch
        {
            return config; // best effort: return original on error
        }
    }
}
