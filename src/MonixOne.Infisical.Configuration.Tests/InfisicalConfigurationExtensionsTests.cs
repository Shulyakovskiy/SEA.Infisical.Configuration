using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace MonixOne.Infisical.Configuration.Tests;

public sealed class InfisicalConfigurationExtensionsTests
{
    private const string ClientIdVariable = "SEA_INFISICAL_TEST_CLIENT_ID";
    private const string ClientSecretVariable = "SEA_INFISICAL_TEST_CLIENT_SECRET";
    private const string ProjectIdVariable = "SEA_INFISICAL_TEST_PROJECT_ID";
    private const string EnvironmentVariable = "SEA_INFISICAL_TEST_ENVIRONMENT";
    private const string RefreshIntervalVariable = "SEA_INFISICAL_TEST_REFRESH_INTERVAL_SECONDS";

    [Fact]
    public void AddInfisical_MissingRequiredSettings_ThrowsBeforeAddingProvider()
    {
        // Arrange
        using var environment = new EnvironmentVariableScope(
            (ClientIdVariable, null),
            (ClientSecretVariable, null),
            (ProjectIdVariable, null),
            (EnvironmentVariable, null));
        var services = new ServiceCollection();
        var configuration = new ConfigurationManager();
        var initialSourceCount = configuration.Sources.Count;

        // Act
        var exception = Should.Throw<InvalidOperationException>(() =>
            services.AddInfisical(configuration, ConfigureEnvironmentVariableNames));

        // Assert
        exception.Message.ShouldContain(ClientIdVariable);
        exception.Message.ShouldContain(ClientSecretVariable);
        exception.Message.ShouldContain(ProjectIdVariable);
        exception.Message.ShouldContain(EnvironmentVariable);
        configuration.Sources.Count.ShouldBe(initialSourceCount);
    }

    [Fact]
    public void AddInfisical_InvalidRefreshIntervalEnvironmentValue_ThrowsBeforeAddingProvider()
    {
        // Arrange
        using var environment = new EnvironmentVariableScope(
            (ClientIdVariable, "client-id"),
            (ClientSecretVariable, "client-secret"),
            (ProjectIdVariable, "project-id"),
            (EnvironmentVariable, "development"),
            (RefreshIntervalVariable, "0"));
        var services = new ServiceCollection();
        var configuration = new ConfigurationManager();
        var initialSourceCount = configuration.Sources.Count;

        // Act
        var exception = Should.Throw<InvalidOperationException>(() =>
            services.AddInfisical(configuration, ConfigureEnvironmentVariableNames));

        // Assert
        exception.Message.ShouldContain(RefreshIntervalVariable);
        configuration.Sources.Count.ShouldBe(initialSourceCount);
    }

    [Fact]
    public void AddInfisical_ZeroExplicitRefreshInterval_ThrowsBeforeAddingProvider()
    {
        // Arrange
        using var environment = new EnvironmentVariableScope((RefreshIntervalVariable, null));
        var services = new ServiceCollection();
        var configuration = new ConfigurationManager();
        var initialSourceCount = configuration.Sources.Count;

        // Act
        var exception = Should.Throw<InvalidOperationException>(() =>
            services.AddInfisical(configuration, options =>
            {
                ConfigureEnvironmentVariableNames(options);
                options.ClientId = "client-id";
                options.ClientSecret = "client-secret";
                options.ProjectId = "project-id";
                options.EnvironmentSlug = "development";
                options.RefreshInterval = TimeSpan.Zero;
            }));

        // Assert
        exception.Message.ShouldContain("refresh interval must be positive");
        configuration.Sources.Count.ShouldBe(initialSourceCount);
    }

    private static void ConfigureEnvironmentVariableNames(InfisicalConfigurationOptions options)
    {
        options.ClientIdEnvironmentVariable = ClientIdVariable;
        options.ClientSecretEnvironmentVariable = ClientSecretVariable;
        options.ProjectIdEnvironmentVariable = ProjectIdVariable;
        options.EnvironmentSlugEnvironmentVariable = EnvironmentVariable;
        options.RefreshIntervalSecondsEnvironmentVariable = RefreshIntervalVariable;
    }

    /// <summary>
    /// Restores process-level variables after each public configuration API test.
    /// </summary>
    private sealed class EnvironmentVariableScope : IDisposable
    {
        private readonly Dictionary<string, string?> _previousValues = new(StringComparer.Ordinal);

        public EnvironmentVariableScope(params (string Name, string? Value)[] values)
        {
            foreach (var (name, value) in values)
            {
                _previousValues[name] = Environment.GetEnvironmentVariable(name);
                Environment.SetEnvironmentVariable(name, value);
            }
        }

        public void Dispose()
        {
            foreach (var (name, value) in _previousValues)
            {
                Environment.SetEnvironmentVariable(name, value);
            }
        }
    }
}
