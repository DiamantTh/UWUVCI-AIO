using UWUVCI.Config.Models;

namespace UWUVCI.Config.Validation;

/// <summary>
/// Validates <see cref="AppSettingsModel"/> at load time.
/// Returns a list of human-readable error strings; empty list = valid.
/// CKey is never included in error messages.
/// </summary>
public static class AppSettingsValidator
{
    public static IReadOnlyList<string> Validate(AppSettingsModel m)
    {
        var errors = new List<string>();

        if (m.FileCopyParallelism < 1 || m.FileCopyParallelism > 64)
            errors.Add($"FileCopyParallelism must be between 1 and 64 (got {m.FileCopyParallelism}).");

        if (m.UnixWaitDelayMs < 0)
            errors.Add($"UnixWaitDelayMs must be >= 0 (got {m.UnixWaitDelayMs}).");

        if (!string.IsNullOrEmpty(m.Theme) &&
            m.Theme != "Dark" && m.Theme != "Light")
            errors.Add($"Theme must be 'Dark' or 'Light' (got '{m.Theme}').");

        if (!string.IsNullOrEmpty(m.LastVersionSeen) &&
            !System.Version.TryParse(m.LastVersionSeen, out _))
            errors.Add($"LastVersionSeen is not a valid version string (got '{m.LastVersionSeen}').");

        return errors;
    }
}
