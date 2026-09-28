using Core.Errors;
using Core.Identity.Tokens.CurrentUser;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Progress.GetSubjectProgress;
using Elmanhg.Application.Progress.Shared;
using Elmanhg.Domain.Mastery;
using Elmanhg.Domain.Sessions;
using Elmanhg.Domain.Subjects;
using Elmanhg.Domain.Units;
using FluentAssertions;
using NSubstitute;

namespace Elmanhg.Tests.Application.Features.Progress.GetSubjectProgress;

public sealed class GetSubjectProgressHandlerTests
{
    private readonly IQuestionMasteryRepository _questionMasteryRepository = Substitute.For<IQuestionMasteryRepository>();
    private readonly ISessionRepository _sessionRepository = Substitute.For<ISessionRepository>();
    private readonly ISubjectRepository _subjectRepository = Substitute.For<ISubjectRepository>();
    private readonly ICurriculumUnitRepository _unitRepository = Substitute.For<ICurriculumUnitRepository>();
    private readonly ICurrentUserService _currentUserService = Substitute.For<ICurrentUserService>();
    private readonly Guid _studentId = Guid.NewGuid();
    private readonly Subject _physics = Subject.Create("Physics", 1, Guid.NewGuid());
    private readonly Subject _chemistry = Subject.Create("Chemistry", 2, Guid.NewGuid());
    private readonly CurriculumUnit _mechanics;
    private readonly CurriculumUnit _waves;
    private readonly CurriculumUnit _atoms;
    private readonly GetSubjectProgressHandler _handler;

    public GetSubjectProgressHandlerTests()
    {
        _currentUserService.UserId.Returns(_studentId);
        _mechanics = CurriculumUnit.Create(_physics, "Mechanics", 1, Guid.NewGuid());
        _waves = CurriculumUnit.Create(_physics, "Waves", 2, Guid.NewGuid());
        _atoms = CurriculumUnit.Create(_chemistry, "Atoms", 1, Guid.NewGuid());
        _subjectRepository.GetAllAsync(Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<Subject>, IQueryable<Subject>>?>(), Arg.Any<Func<IQueryable<Subject>, IOrderedQueryable<Subject>>?>(), Arg.Any<bool>()).Returns([_physics, _chemistry]);
        _unitRepository.GetAllAsync(Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<CurriculumUnit>, IQueryable<CurriculumUnit>>?>(), Arg.Any<Func<IQueryable<CurriculumUnit>, IOrderedQueryable<CurriculumUnit>>?>(), Arg.Any<bool>()).Returns([_mechanics, _waves, _atoms]);
        StubCounts([]);
        StubBests([]);
        _handler = new GetSubjectProgressHandler(_questionMasteryRepository, _sessionRepository, _subjectRepository, _unitRepository, _currentUserService);
    }

    [Fact]
    public async Task Handle_NoCurrentUser_ThrowsUserNotAuthenticated()
    {
        _currentUserService.UserId.Returns((Guid?)null);

        var act = () => _handler.Handle(new GetSubjectProgressQuery(), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<UnauthorizedCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.UserNotAuthenticated);
        await _questionMasteryRepository.DidNotReceive().GetLessonCountsAsync(Arg.Any<Guid>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_Subjects_ReturnsUnitsInOrderWithWeightedMastery()
    {
        StubCounts([Count(_mechanics, 4, 2, 3), Count(_mechanics, 3, 1, 1), Count(_waves, 1, 0, 1), Count(_atoms, 7, 3, 5)]);

        var result = await _handler.Handle(new GetSubjectProgressQuery(), TestContext.Current.CancellationToken);

        result.Select(x => x.SubjectId).Should().Equal(_physics.Id, _chemistry.Id);
        var physics = result[0];
        (physics.Name, physics.ServableCount, physics.MasteredCount, physics.SeenCount, physics.MasteryPercent).Should().Be(("Physics", 8, 3, 5, 37));
        physics.Units.Should().Equal(new UnitProgressResult(_mechanics.Id, "Mechanics", 7, 3, 4, 42, null), new UnitProgressResult(_waves.Id, "Waves", 1, 0, 1, 0, null));
        result[1].Units.Should().Equal(new UnitProgressResult(_atoms.Id, "Atoms", 7, 3, 5, 42, null));
    }

    [Fact]
    public async Task Handle_UnitWithoutServableQuestions_ReturnsZeroCounts()
    {
        StubCounts([Count(_mechanics, 2, 1, 2)]);

        var result = await _handler.Handle(new GetSubjectProgressQuery(), TestContext.Current.CancellationToken);

        result[0].Units[1].Should().Be(new UnitProgressResult(_waves.Id, "Waves", 0, 0, 0, 0, null));
        (result[1].ServableCount, result[1].MasteredCount, result[1].SeenCount, result[1].MasteryPercent).Should().Be((0, 0, 0, 0));
        result[1].Units.Should().Equal(new UnitProgressResult(_atoms.Id, "Atoms", 0, 0, 0, 0, null));
    }

    [Fact]
    public async Task Handle_UnitExamBest_ReturnsBestOnlyForMatchingUnit()
    {
        StubBests([new UnitExamBestScore($"unit:{_mechanics.Id:D}", 87.5m), new UnitExamBestScore($"lesson:{_waves.Id:D}", 99m)]);

        var result = await _handler.Handle(new GetSubjectProgressQuery(), TestContext.Current.CancellationToken);

        result[0].Units.Select(x => x.BestExamScorePercent).Should().Equal(87.5m, null);
    }

    private LessonMasteryCount Count(CurriculumUnit unit, int servable, int mastered, int seen) => new(unit.SubjectId, 1, unit.Id, unit.Order, Guid.NewGuid(), 1, servable, mastered, seen);

    private void StubCounts(List<LessonMasteryCount> counts) => _questionMasteryRepository.GetLessonCountsAsync(_studentId, null, Arg.Any<CancellationToken>()).Returns(counts);

    private void StubBests(List<UnitExamBestScore> bests) => _sessionRepository.GetBestUnitExamScoresAsync(_studentId, Arg.Any<CancellationToken>()).Returns(bests);
}
