namespace ZEngine.Plugins;

public enum PluginDependencyKind
{
    Required,
    Optional,
    Development
}

public enum NativeReloadPolicy
{
    RestartRuntime,
    KeepLoaded,
    Reloadable
}

public readonly record struct SemanticVersion(
    int Major,
    int Minor,
    int Patch,
    string? Prerelease = null) : IComparable<SemanticVersion>
{
    public static SemanticVersion Parse(string value)
    {
        if (!TryParse(value, out SemanticVersion version))
        {
            throw new FormatException($"'{value}' is not a supported SemVer 2.0 version.");
        }

        return version;
    }

    public static bool TryParse(
        string? value,
        out SemanticVersion version)
    {
        version = default;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        string coreAndPrerelease = value.Split('+', 2)[0];
        string[] prereleaseParts = coreAndPrerelease.Split('-', 2);
        string[] numbers = prereleaseParts[0].Split('.');
        if (numbers.Length != 3
            || !int.TryParse(numbers[0], out int major)
            || !int.TryParse(numbers[1], out int minor)
            || !int.TryParse(numbers[2], out int patch)
            || major < 0
            || minor < 0
            || patch < 0)
        {
            return false;
        }

        version = new(
            major,
            minor,
            patch,
            prereleaseParts.Length == 2 ? prereleaseParts[1] : null);
        return true;
    }

    public int CompareTo(SemanticVersion other)
    {
        int core = Major.CompareTo(other.Major);
        if (core == 0)
        {
            core = Minor.CompareTo(other.Minor);
        }

        if (core == 0)
        {
            core = Patch.CompareTo(other.Patch);
        }

        if (core != 0)
        {
            return core;
        }

        if (Prerelease is null)
        {
            return other.Prerelease is null ? 0 : 1;
        }

        return other.Prerelease is null
            ? -1
            : string.Compare(Prerelease, other.Prerelease, StringComparison.Ordinal);
    }

    public override string ToString() =>
        $"{Major}.{Minor}.{Patch}"
        + (Prerelease is null ? string.Empty : $"-{Prerelease}");
}

public readonly record struct VersionRange(
    SemanticVersion? Minimum,
    bool IncludeMinimum,
    SemanticVersion? Maximum,
    bool IncludeMaximum)
{
    public static VersionRange Any { get; } = new(null, false, null, false);

    public static VersionRange Parse(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        if (value == "*")
        {
            return Any;
        }

        if (value.Length < 2
            || value[0] is not ('[' or '(')
            || value[^1] is not (']' or ')'))
        {
            SemanticVersion exact = SemanticVersion.Parse(value);
            return new(exact, true, exact, true);
        }

        string[] bounds = value[1..^1].Split(',', 2);
        if (bounds.Length != 2)
        {
            throw new FormatException($"'{value}' is not a version range.");
        }

        SemanticVersion? minimum = string.IsNullOrWhiteSpace(bounds[0])
            ? null
            : SemanticVersion.Parse(bounds[0].Trim());
        SemanticVersion? maximum = string.IsNullOrWhiteSpace(bounds[1])
            ? null
            : SemanticVersion.Parse(bounds[1].Trim());
        return new(
            minimum,
            value[0] == '[',
            maximum,
            value[^1] == ']');
    }

    public bool Contains(SemanticVersion version)
    {
        if (Minimum is { } minimum)
        {
            int comparison = version.CompareTo(minimum);
            if (comparison < 0 || (comparison == 0 && !IncludeMinimum))
            {
                return false;
            }
        }

        if (Maximum is { } maximum)
        {
            int comparison = version.CompareTo(maximum);
            if (comparison > 0 || (comparison == 0 && !IncludeMaximum))
            {
                return false;
            }
        }

        return true;
    }
}

public sealed record PluginDependency(
    string Id,
    string VersionRange,
    PluginDependencyKind Kind = PluginDependencyKind.Required);

public sealed record PluginManifest(
    string Id,
    string Version,
    string EntryAssembly,
    string EntryType,
    IReadOnlyList<string> ContractAssemblies,
    IReadOnlyList<PluginDependency> Dependencies,
    IReadOnlyList<string> RequiredExports,
    NativeReloadPolicy NativeReloadPolicy = NativeReloadPolicy.RestartRuntime,
    string? ContentSha256 = null)
{
    public SemanticVersion SemanticVersion => SemanticVersion.Parse(Version);
}
