using Core.Settings;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using NSubstitute;

namespace Elmanhg.Tests.Core.Settings;

public sealed class CachedRuntimeSettingsTests : IDisposable
{
    private readonly IRuntimeSettingOverrideStore _store = Substitute.For<IRuntimeSettingOverrideStore>();
    private readonly MemoryCache _memoryCache = new(new MemoryCacheOptions());
    private readonly CachedRuntimeSettings _settings;
    private string _stored = "7";

    public CachedRuntimeSettingsTests()
    {
        _store.GetOverridesAsync(Arg.Any<CancellationToken>()).Returns(_ => [new ProbeOverride("probe.count", _stored)]);
        var registry = new RuntimeSettingRegistry([new ProbeRuntimeSettings()], ProbeRuntimeSettings.GroupOrder);
        _settings = new CachedRuntimeSettings(_store, registry, _memoryCache, Microsoft.Extensions.Options.Options.Create(new RuntimeSettingsOptions()));
    }

    [Fact]
    public async Task GetAsync_FirstCall_ReadsOverrideFromStore()
    {
        var value = await _settings.GetAsync(ProbeRuntimeSettings.Count, TestContext.Current.CancellationToken);

        value.Should().Be(7);
    }

    [Fact]
    public async Task GetAsync_SecondCall_IsServedFromCache()
    {
        await _settings.GetAsync(ProbeRuntimeSettings.Count, TestContext.Current.CancellationToken);
        _stored = "9";

        var value = await _settings.GetAsync(ProbeRuntimeSettings.Count, TestContext.Current.CancellationToken);

        value.Should().Be(7);
        await _store.Received(1).GetOverridesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetAsync_AfterCacheKeyRemoved_ReturnsNewValue()
    {
        await _settings.GetAsync(ProbeRuntimeSettings.Count, TestContext.Current.CancellationToken);
        _stored = "9";
        _memoryCache.Remove(RuntimeSettingsCache.Key);

        var value = await _settings.GetAsync(ProbeRuntimeSettings.Count, TestContext.Current.CancellationToken);

        value.Should().Be(9);
    }

    public void Dispose() => _memoryCache.Dispose();
}
