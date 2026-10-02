using Elmanhg.Application.Configuration.GetRuntimeSettings;
using Elmanhg.Application.Configuration.Shared;
using Elmanhg.Application.Shared.RuntimeSettings;
using Elmanhg.Domain.RuntimeSettings;
using Elmanhg.Tests.Fixtures.RuntimeSettings;
using FluentAssertions;
using NSubstitute;

namespace Elmanhg.Tests.Application.Features.Configuration.GetRuntimeSettings;

public sealed class GetRuntimeSettingsHandlerTests
{
    private static readonly Guid AdminId = Guid.Parse("6a1c2e3f-4b5d-4e6f-8a9b-0c1d2e3f4a5b");
    private readonly IRuntimeSettingOverrideRepository _repository = Substitute.For<IRuntimeSettingOverrideRepository>();
    private readonly List<RuntimeSettingOverride> _rows = [];
    private readonly GetRuntimeSettingsHandler _handler;

    public GetRuntimeSettingsHandlerTests()
    {
        _repository.GetAllAsync(Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<RuntimeSettingOverride>, IQueryable<RuntimeSettingOverride>>?>(), Arg.Any<Func<IQueryable<RuntimeSettingOverride>, IOrderedQueryable<RuntimeSettingOverride>>?>(), Arg.Any<bool>())
            .Returns(_ => _rows.ToList());
        _handler = new GetRuntimeSettingsHandler(_repository, FakeRuntimeSettings.DefaultRegistry());
    }

    [Fact]
    public async Task Handle_NoOverrides_ReturnsGroupsInEnumOrderWithDefaults()
    {
        var result = await _handler.Handle(new GetRuntimeSettingsQuery(), TestContext.Current.CancellationToken);

        result.Select(x => x.Group).Should().Equal(RuntimeSettingGroup.Features, RuntimeSettingGroup.AskTeacher, RuntimeSettingGroup.SlaCalendar, RuntimeSettingGroup.PlanLimits, RuntimeSettingGroup.Grading, RuntimeSettingGroup.Uploads);
        var sla = ReplySla(result);
        (sla.Value.GetInt32(), sla.DefaultValue.GetInt32(), sla.IsOverridden, sla.UpdatedAt).Should().Be((24, 24, false, (DateTimeOffset?)null));
    }

    [Fact]
    public async Task Handle_Overridden_ReturnsOverrideDefaultAndUpdatedAt()
    {
        var row = RuntimeSettingOverride.Create("askTeacher.replySlaHours", "30", AdminId);
        _rows.Add(row);

        var result = await _handler.Handle(new GetRuntimeSettingsQuery(), TestContext.Current.CancellationToken);

        var sla = ReplySla(result);
        (sla.Value.GetInt32(), sla.DefaultValue.GetInt32(), sla.IsOverridden, sla.UpdatedAt).Should().Be((30, 24, true, (DateTimeOffset?)row.UpdationDate));
    }

    private static RuntimeSettingResult ReplySla(List<RuntimeSettingGroupResult> groups) => groups.SelectMany(x => x.Settings).Single(x => x.Key == "askTeacher.replySlaHours");
}
