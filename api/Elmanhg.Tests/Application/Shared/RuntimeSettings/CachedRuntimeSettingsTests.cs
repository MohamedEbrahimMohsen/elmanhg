using Elmanhg.Application.Shared.Options;
using Elmanhg.Application.Shared.RuntimeSettings;
using Elmanhg.Application.Shared.RuntimeSettings.Definitions;
using Elmanhg.Domain.RuntimeSettings;
using Elmanhg.Tests.Fixtures.RuntimeSettings;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using NSubstitute;
using System.Linq.Expressions;

namespace Elmanhg.Tests.Application.Shared.RuntimeSettings;

public sealed class CachedRuntimeSettingsTests : IDisposable
{
    private static readonly Guid AdminId = Guid.Parse("6a1c2e3f-4b5d-4e6f-8a9b-0c1d2e3f4a5b");
    private readonly IRuntimeSettingOverrideRepository _repository = Substitute.For<IRuntimeSettingOverrideRepository>();
    private readonly MemoryCache _memoryCache = new(new MemoryCacheOptions());
    private readonly CachedRuntimeSettings _settings;
    private string _stored = "7";

    public CachedRuntimeSettingsTests()
    {
        _repository.FindAsync(Arg.Any<Expression<Func<RuntimeSettingOverride, bool>>>(), Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<RuntimeSettingOverride>, IQueryable<RuntimeSettingOverride>>?>(), Arg.Any<Func<IQueryable<RuntimeSettingOverride>, IOrderedQueryable<RuntimeSettingOverride>>?>(), Arg.Any<bool>())
            .Returns(_ => [RuntimeSettingOverride.Create("plans.freeDailyQuizQuestions", _stored, AdminId)]);
        _settings = new CachedRuntimeSettings(_repository, FakeRuntimeSettings.DefaultRegistry(), _memoryCache, Microsoft.Extensions.Options.Options.Create(new RuntimeSettingsOptions()));
    }

    [Fact]
    public async Task GetAsync_FirstCall_ReadsOverrideFromRepository()
    {
        var value = await _settings.GetAsync(PlanLimitRuntimeSettings.FreeDailyQuizQuestions, TestContext.Current.CancellationToken);

        value.Should().Be(7);
    }

    [Fact]
    public async Task GetAsync_SecondCall_IsServedFromCache()
    {
        await _settings.GetAsync(PlanLimitRuntimeSettings.FreeDailyQuizQuestions, TestContext.Current.CancellationToken);
        _stored = "9";

        var value = await _settings.GetAsync(PlanLimitRuntimeSettings.FreeDailyQuizQuestions, TestContext.Current.CancellationToken);

        value.Should().Be(7);
        await _repository.Received(1).FindAsync(Arg.Any<Expression<Func<RuntimeSettingOverride, bool>>>(), Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<RuntimeSettingOverride>, IQueryable<RuntimeSettingOverride>>?>(), Arg.Any<Func<IQueryable<RuntimeSettingOverride>, IOrderedQueryable<RuntimeSettingOverride>>?>(), Arg.Any<bool>());
    }

    [Fact]
    public async Task GetAsync_AfterCacheKeyRemoved_ReturnsNewValue()
    {
        await _settings.GetAsync(PlanLimitRuntimeSettings.FreeDailyQuizQuestions, TestContext.Current.CancellationToken);
        _stored = "9";
        _memoryCache.Remove(RuntimeSettingsCache.Key);

        var value = await _settings.GetAsync(PlanLimitRuntimeSettings.FreeDailyQuizQuestions, TestContext.Current.CancellationToken);

        value.Should().Be(9);
    }

    public void Dispose() => _memoryCache.Dispose();
}
