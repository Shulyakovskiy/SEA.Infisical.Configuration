using Shouldly;
using Xunit;

namespace MonixOne.Infisical.Configuration.Tests;

public sealed class InfisicalConfigurationProviderTests
{
    [Fact]
    public async Task RefreshAsync_Cancelled_KeepsExistingConfiguration()
    {
        // Arrange
        using var provider = new InfisicalConfigurationProvider(
            new InfisicalConfigurationOptions());
        provider.Set("Application:ExistingSetting", "preserved");
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        // Act
        await Should.ThrowAsync<OperationCanceledException>(() =>
            provider.RefreshAsync(cancellation.Token));

        // Assert
        provider.TryGet("Application:ExistingSetting", out var value).ShouldBeTrue();
        value.ShouldBe("preserved");
    }
}
