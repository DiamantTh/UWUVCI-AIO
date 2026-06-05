using UWUVCI.Config.Models;

namespace UWUVCI.Config.Validation;

/// <summary>
/// Validates a <see cref="ToolManifestModel"/> at load time.
/// </summary>
public static class ToolManifestValidator
{
    public static IReadOnlyList<string> Validate(ToolManifestModel m)
    {
        var errors = new List<string>();

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < m.Tools.Count; i++)
        {
            var t = m.Tools[i];
            string pos = $"tools[{i}]";

            if (string.IsNullOrWhiteSpace(t.Name))
                errors.Add($"{pos}: 'name' must not be empty.");
            else if (!seen.Add(t.Name))
                errors.Add($"{pos}: duplicate tool name '{t.Name}'.");

            if (string.IsNullOrWhiteSpace(t.WindowsFileName))
                errors.Add($"{pos} ('{t.Name}'): 'windows_file' must not be empty.");

            // Native Linux tools must have a linux_file; Windows-only tools may omit it.
            if (!t.WindowsOnly && string.IsNullOrWhiteSpace(t.LinuxFileName))
                errors.Add($"{pos} ('{t.Name}'): 'linux_file' is required when 'windows_only' is false.");
        }

        return errors;
    }
}
