using Core.Errors;
using Core.Identity.Tokens.CurrentUser;
using Elmanhg.Application.Configuration.UpdateRuntimeSetting;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Shared.RuntimeSettings;
using Elmanhg.Domain.RuntimeSettings;
using Elmanhg.Tests.Fixtures.RuntimeSettings;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using NSubstitute;

namespace Elmanhg.Tests.Application.Features.Configuration.UpdateRuntimeSetting;

public sealed class UpdateRuntimeSettingHandlerTests : IDisposable
{
    private static readonly Guid AdminId = Guid.Parse("6a1c2e3f-4b5d-4e6f-8a9b-0c1d2e3f4a5b");
    private readonly IRuntimeSettingOverrideRepository _repository = Substitute.For<IRuntimeSettingOverrideRepository>();
    private readonly ICurrentUserService _currentUserService = Substitute.For<ICurrentUserService>();
    private readonly MemoryCache _memoryCache = new(new MemoryCacheOptions());
    private readonly List<RuntimeSettingOverride> _rows = [];
    private readonly UpdateRuntimeSettingHandler _handler;

    public UpdateRuntimeSettingHandlerTests()
    {
        _currentUserService.UserId.Returns(AdminId);
        _repository.GetAllAsync(Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<RuntimeSettingOverride>, IQueryable<RuntimeSettingOverride>>?>(), Arg.Any<Func<IQueryable<RuntimeSettingOverride>, IOrderedQueryable<RuntimeSettingOverride>>?>(), Arg.Any<bool>())
            .Returns(_ => _rows.ToList());
        _memoryCache.Set(RuntimeSettingsCache.Key, "cached");
        _handler = new UpdateRuntimeSettingHandler(_repository, FakeRuntimeSettings.DefaultRegistry(), _currentUserService, _memoryCache);
    }

    [Fact]
    public async Task Handle_NoUser_ThrowsUnauthorized()
    {
        _currentUserService.UserId.Returns((Guid?)null);

        var act = () => Handle("plans.freeDailyQuizQuestions", 12);

        (await act.Should().ThrowAsync<UnauthorizedCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.UserNotAuthenticated);
        await _repository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_UnknownKey_ThrowsNotFound()
    {
        var act = () => Handle("nope.key", 12);

        (await act.Should().ThrowAsync<NotFoundCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.RuntimeSettingNotFound);
        await _repository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_FirstOverride_AddsRowSavesAndClearsCache()
    {
        RuntimeSettingOverride? added = null;
        await _repository.AddAsync(Arg.Do<RuntimeSettingOverride>(x => added = x), Arg.Any<CancellationToken>());

        var result = await Handle("plans.freeDailyQuizQuestions", 12);

        (added!.Key, added.Value).Should().Be(("plans.freeDailyQuizQuestions", "12"));
        await _repository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        _memoryCache.TryGetValue(RuntimeSettingsCache.Key, out _).Should().BeFalse();
        (result.Value.GetInt32(), result.IsOverridden, result.OverrideId).Should().Be((12, true, (Guid?)added.Id));
    }

    [Fact]
    public async Task Handle_ExistingRow_OverridesInPlace()
    {
        var row = RuntimeSettingOverride.Create("plans.freeDailyQuizQuestions", "12", AdminId);
        _rows.Add(row);

        await Handle("plans.freeDailyQuizQuestions", 15);

        row.Value.Should().Be("15");
        await _repository.DidNotReceive().AddAsync(Arg.Any<RuntimeSettingOverride>(), Arg.Any<CancellationToken>());
        await _repository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ReminderAfterSla_ThrowsReminderOrderInvalid()
    {
        var act = () => Handle("askTeacher.secondReminderAfterHours", 30);

        (await act.Should().ThrowAsync<BusinessRuleViolationCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.AskTeacherReminderOrderInvalid);
        await _repository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
        _memoryCache.TryGetValue(RuntimeSettingsCache.Key, out _).Should().BeTrue();
    }

    public void Dispose() => _memoryCache.Dispose();

    private Task<Elmanhg.Application.Configuration.Shared.RuntimeSettingResult> Handle(string key, int value) => _handler.Handle(new UpdateRuntimeSettingCommand(key, RuntimeSettingJson.ToElement(value)), TestContext.Current.CancellationToken);
}
