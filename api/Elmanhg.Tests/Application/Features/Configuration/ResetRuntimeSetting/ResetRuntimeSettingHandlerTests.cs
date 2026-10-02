using Core.Errors;
using Core.Identity.Tokens.CurrentUser;
using Elmanhg.Application.Configuration.ResetRuntimeSetting;
using Elmanhg.Application.Configuration.Shared;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Shared.RuntimeSettings;
using Elmanhg.Domain.RuntimeSettings;
using Elmanhg.Tests.Fixtures.RuntimeSettings;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using NSubstitute;

namespace Elmanhg.Tests.Application.Features.Configuration.ResetRuntimeSetting;

public sealed class ResetRuntimeSettingHandlerTests : IDisposable
{
    private static readonly Guid AdminId = Guid.Parse("6a1c2e3f-4b5d-4e6f-8a9b-0c1d2e3f4a5b");
    private readonly IRuntimeSettingOverrideRepository _repository = Substitute.For<IRuntimeSettingOverrideRepository>();
    private readonly ICurrentUserService _currentUserService = Substitute.For<ICurrentUserService>();
    private readonly MemoryCache _memoryCache = new(new MemoryCacheOptions());
    private readonly List<RuntimeSettingOverride> _rows = [];
    private readonly ResetRuntimeSettingHandler _handler;

    public ResetRuntimeSettingHandlerTests()
    {
        _currentUserService.UserId.Returns(AdminId);
        _repository.GetAllAsync(Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<RuntimeSettingOverride>, IQueryable<RuntimeSettingOverride>>?>(), Arg.Any<Func<IQueryable<RuntimeSettingOverride>, IOrderedQueryable<RuntimeSettingOverride>>?>(), Arg.Any<bool>())
            .Returns(_ => _rows.ToList());
        _memoryCache.Set(RuntimeSettingsCache.Key, "cached");
        _handler = new ResetRuntimeSettingHandler(_repository, FakeRuntimeSettings.DefaultRegistry(), _currentUserService, _memoryCache);
    }

    [Fact]
    public async Task Handle_NoUser_ThrowsUnauthorized()
    {
        _currentUserService.UserId.Returns((Guid?)null);

        var act = () => Handle("plans.freeDailyQuizQuestions");

        (await act.Should().ThrowAsync<UnauthorizedCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.UserNotAuthenticated);
        await _repository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_UnknownKey_ThrowsNotFound()
    {
        var act = () => Handle("nope.key");

        (await act.Should().ThrowAsync<NotFoundCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.RuntimeSettingNotFound);
        await _repository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_NotOverridden_ReturnsDefaultWithoutSaving()
    {
        var result = await Handle("plans.freeDailyQuizQuestions");

        (result.IsOverridden, result.Value.GetInt32()).Should().Be((false, 10));
        await _repository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
        _memoryCache.TryGetValue(RuntimeSettingsCache.Key, out _).Should().BeTrue();
    }

    [Fact]
    public async Task Handle_AlreadyReset_ReturnsDefaultWithoutSaving()
    {
        var earlierAdminId = Guid.Parse("0b7d4c2a-9e1f-4a3b-8c5d-6e7f8a9b0c1d");
        var row = Add("plans.freeDailyQuizQuestions", "12");
        row.Reset(earlierAdminId);
        var resetAt = row.UpdationDate;

        var result = await Handle("plans.freeDailyQuizQuestions");

        (result.IsOverridden, result.Value.GetInt32()).Should().Be((false, 10));
        (row.UpdationDate, row.UpdatedBy).Should().Be((resetAt, (Guid?)earlierAdminId));
        await _repository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
        _memoryCache.TryGetValue(RuntimeSettingsCache.Key, out _).Should().BeTrue();
    }

    [Fact]
    public async Task Handle_Overridden_ClearsValueSavesAndClearsCache()
    {
        var row = Add("plans.freeDailyQuizQuestions", "12");

        var result = await Handle("plans.freeDailyQuizQuestions");

        row.Value.Should().BeNull();
        await _repository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        _memoryCache.TryGetValue(RuntimeSettingsCache.Key, out _).Should().BeFalse();
        (result.Value.GetInt32(), result.IsOverridden, result.OverrideId).Should().Be((10, false, (Guid?)row.Id));
    }

    [Fact]
    public async Task Handle_ResetBreaksReminderOrder_Throws()
    {
        var first = Add("askTeacher.firstReminderAfterHours", "8");
        Add("askTeacher.secondReminderAfterHours", "10");

        var act = () => Handle("askTeacher.firstReminderAfterHours");

        (await act.Should().ThrowAsync<BusinessRuleViolationCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.AskTeacherReminderOrderInvalid);
        first.Value.Should().Be("8");
        await _repository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    public void Dispose() => _memoryCache.Dispose();

    private RuntimeSettingOverride Add(string key, string value)
    {
        var row = RuntimeSettingOverride.Create(key, value, AdminId);
        _rows.Add(row);
        return row;
    }

    private Task<RuntimeSettingResult> Handle(string key) => _handler.Handle(new ResetRuntimeSettingCommand(key), TestContext.Current.CancellationToken);
}
