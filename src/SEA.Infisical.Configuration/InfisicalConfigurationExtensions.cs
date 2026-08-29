using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace SEA.Infisical.Configuration;

public static class InfisicalConfigurationExtensions
{
    /// <summary>
    /// Loads Infisical into IConfiguration and registers background refresh.
    /// Call this before Configure&lt;T&gt; / AddOptions().BindConfiguration().
    /// </summary>
    public static IServiceCollection AddInfisical(
        this IServiceCollection services,
        IConfigurationManager configuration,
        Action<InfisicalConfigurationOptions>? configure = null)
    {
        var options = new InfisicalConfigurationOptions();
        configure?.Invoke(options);
        options.ApplyEnvironmentDefaults();
        options.Validate();

        var source = new InfisicalConfigurationSource(options);
        configuration.Add(source);

        if (source.Provider is null)
        {
            throw new InvalidOperationException("Infisical configuration provider was not created.");
        }

        services.AddSingleton(options);
        services.AddSingleton(source.Provider);
        services.AddHostedService<InfisicalConfigurationRefreshService>();

        return services;
    }
}

internal sealed class InfisicalConfigurationSource(
    InfisicalConfigurationOptions options) : IConfigurationSource
{
    public InfisicalConfigurationProvider? Provider { get; private set; }

    public IConfigurationProvider Build(IConfigurationBuilder builder)
    {
        Provider = new InfisicalConfigurationProvider(options);
        return Provider;
    }
}
