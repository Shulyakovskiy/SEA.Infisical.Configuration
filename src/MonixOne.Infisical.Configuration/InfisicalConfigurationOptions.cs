using System.Globalization;

namespace MonixOne.Infisical.Configuration;

/// <summary>
/// Settings for loading Infisical secrets into ASP.NET Core configuration and
/// the current process environment.
/// </summary>
public sealed class InfisicalConfigurationOptions
{
    /// <summary>
    /// Enables loading Infisical secrets and registering background refresh.
    /// When disabled, AddInfisical does not validate Infisical settings or
    /// change application configuration.
    /// </summary>
    public bool Enabled { get; set; } = true;

    public string ClientId { get; set; } = string.Empty;

    public string ClientSecret { get; set; } = string.Empty;

    public string ProjectId { get; set; } = string.Empty;

    public string EnvironmentSlug { get; set; } = string.Empty;

    public string SecretPath { get; set; } = string.Empty;

    public string? Url { get; set; }

    public bool Recursive { get; set; }

    public bool ExpandSecretReferences { get; set; } = true;

    /// <summary>
    /// Also copies each Infisical secret to EnvironmentVariableTarget.Process.
    /// </summary>
    public bool SetProcessEnvironment { get; set; } = true;

    /// <summary>
    /// Background configuration refresh interval. Defaults to one day.
    /// </summary>
    public TimeSpan RefreshInterval { get; set; } = TimeSpan.FromDays(1);

    public string ClientIdEnvironmentVariable { get; set; } = "INFISICAL_CLIENT_ID";

    public string ClientSecretEnvironmentVariable { get; set; } = "INFISICAL_CLIENT_SECRET";

    public string ProjectIdEnvironmentVariable { get; set; } = "INFISICAL_PROJECT_ID";

    public string EnvironmentSlugEnvironmentVariable { get; set; } = "INFISICAL_ENVIRONMENT";

    public string SecretPathEnvironmentVariable { get; set; } = "INFISICAL_SECRET_PATH";

    public string UrlEnvironmentVariable { get; set; } = "INFISICAL_URL";

    public string RecursiveEnvironmentVariable { get; set; } = "INFISICAL_RECURSIVE";

    public string RefreshIntervalSecondsEnvironmentVariable { get; set; } =
        "INFISICAL_REFRESH_INTERVAL_SECONDS";

    internal void ApplyEnvironmentDefaults()
    {
        ClientId = ValueOrCurrent(ClientId, ClientIdEnvironmentVariable);
        ClientSecret = ValueOrCurrent(ClientSecret, ClientSecretEnvironmentVariable);
        ProjectId = ValueOrCurrent(ProjectId, ProjectIdEnvironmentVariable);
        EnvironmentSlug = ValueOrCurrent(EnvironmentSlug, EnvironmentSlugEnvironmentVariable);
        SecretPath = ValueOrCurrent(SecretPath, SecretPathEnvironmentVariable);
        Url = ValueOrCurrentNullable(Url, UrlEnvironmentVariable);

        if (bool.TryParse(Environment.GetEnvironmentVariable(RecursiveEnvironmentVariable), out var recursive))
        {
            Recursive = recursive;
        }

        var refreshIntervalSeconds = Environment.GetEnvironmentVariable(
            RefreshIntervalSecondsEnvironmentVariable);

        if (!string.IsNullOrWhiteSpace(refreshIntervalSeconds))
        {
            if (!long.TryParse(
                    refreshIntervalSeconds,
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out var seconds)
                || seconds <= 0
                || seconds > (long)TimeSpan.MaxValue.TotalSeconds)
            {
                throw new InvalidOperationException(
                    $"{RefreshIntervalSecondsEnvironmentVariable} must be a positive integer number of seconds.");
            }

            RefreshInterval = TimeSpan.FromSeconds(seconds);
        }
    }

    internal void Validate()
    {
        var missingVariables = new[]
            {
                (Name: ClientIdEnvironmentVariable, Value: ClientId),
                (Name: ClientSecretEnvironmentVariable, Value: ClientSecret),
                (Name: ProjectIdEnvironmentVariable, Value: ProjectId),
                (Name: EnvironmentSlugEnvironmentVariable, Value: EnvironmentSlug)
            }
            .Where(variable => string.IsNullOrWhiteSpace(variable.Value))
            .Select(variable => variable.Name)
            .ToArray();

        if (missingVariables.Length > 0)
        {
            throw new InvalidOperationException(
                $"Missing required Infisical settings: {string.Join(", ", missingVariables)}.");
        }

        if (string.IsNullOrWhiteSpace(SecretPath))
        {
            SecretPath = "/";
        }

        if (RefreshInterval <= TimeSpan.Zero)
        {
            throw new InvalidOperationException("Infisical refresh interval must be positive.");
        }
    }

    private static string ValueOrCurrent(string current, string environmentVariable) =>
        !string.IsNullOrWhiteSpace(current)
            ? current
            : Environment.GetEnvironmentVariable(environmentVariable) ?? string.Empty;

    private static string? ValueOrCurrentNullable(string? current, string environmentVariable) =>
        !string.IsNullOrWhiteSpace(current)
            ? current
            : Environment.GetEnvironmentVariable(environmentVariable);
}
