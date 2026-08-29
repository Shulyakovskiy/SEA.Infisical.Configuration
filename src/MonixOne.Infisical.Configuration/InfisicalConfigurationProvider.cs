using Infisical.Sdk;
using Infisical.Sdk.Model;
using Microsoft.Extensions.Configuration;

namespace MonixOne.Infisical.Configuration;

/// <summary>
/// Fetches Infisical secrets and exposes them as a normal configuration source.
/// Keys containing double underscores are normalized to ':' for options binding.
/// </summary>
public sealed class InfisicalConfigurationProvider : ConfigurationProvider, IDisposable
{
    private readonly InfisicalConfigurationOptions _options;
    private readonly InfisicalClient _client;
    private readonly SemaphoreSlim _refreshLock = new(1, 1);
    private IReadOnlyDictionary<string, string> _secrets =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    public InfisicalConfigurationProvider(InfisicalConfigurationOptions options)
    {
        _options = options;
        _client = CreateClient(options.Url);
    }

    public IReadOnlyDictionary<string, string> Secrets => _secrets;

    public override void Load()
    {
        RefreshAsync(CancellationToken.None).GetAwaiter().GetResult();
    }

    public async Task<IReadOnlyDictionary<string, string>> RefreshAsync(
        CancellationToken cancellationToken = default)
    {
        await _refreshLock.WaitAsync(cancellationToken);

        try
        {
            // A new login is performed for every refresh so that a rarely used
            // service never depends on a short-lived token kept in memory.
            await _client.Auth().UniversalAuth().LoginAsync(
                _options.ClientId,
                _options.ClientSecret);

            var secrets = await _client.Secrets().ListAsync(new ListSecretsOptions
            {
                ProjectId = _options.ProjectId,
                EnvironmentSlug = _options.EnvironmentSlug,
                SecretPath = _options.SecretPath,
                Recursive = _options.Recursive,
                ExpandSecretReferences = _options.ExpandSecretReferences,
                SetSecretsAsEnvironmentVariables = false,
                ViewSecretValue = true
            });

            if (secrets is null)
            {
                throw new InvalidOperationException("Infisical returned an empty response.");
            }

            var configurationData = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
            var processEnvironmentData = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var secret in secrets)
            {
                var configurationKey = NormalizeConfigurationKey(secret.SecretKey);
                var secretValue = secret.SecretValue ?? string.Empty;

                if (!configurationData.TryAdd(configurationKey, secretValue))
                {
                    throw new InvalidOperationException(
                        $"Infisical returned duplicate configuration key '{configurationKey}'. " +
                        "Use unique secret keys or disable recursive loading.");
                }

                processEnvironmentData[secret.SecretKey] = secretValue;
            }

            if (_options.SetProcessEnvironment)
            {
                foreach (var (key, value) in processEnvironmentData)
                {
                    Environment.SetEnvironmentVariable(
                        key,
                        value,
                        EnvironmentVariableTarget.Process);
                }
            }

            Data = configurationData;
            _secrets = processEnvironmentData;
            OnReload();

            return _secrets;
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    public void Dispose()
    {
        _refreshLock.Dispose();
    }

    private static InfisicalClient CreateClient(string? url)
    {
        var settingsBuilder = new InfisicalSdkSettingsBuilder();

        if (!string.IsNullOrWhiteSpace(url))
        {
            settingsBuilder.WithHostUri(url);
        }

        return new InfisicalClient(settingsBuilder.Build());
    }

    private static string NormalizeConfigurationKey(string key) =>
        key.Replace("__", ConfigurationPath.KeyDelimiter, StringComparison.Ordinal);
}
