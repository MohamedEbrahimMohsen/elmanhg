using Core.Errors;
using Core.Identity.Tokens.CurrentUser;
using Elmanhg.Application.Browse.GetStudentSubject;
using Elmanhg.Application.Exceptions;
using Elmanhg.Domain.Lessons;
using Elmanhg.Domain.Mastery;
using Elmanhg.Domain.Sessions;
using Elmanhg.Domain.Subjects;
using Elmanhg.Domain.Units;
using FluentAssertions;
using NSubstitute;
using System.Linq.Expressions;

namespace Elmanhg.Tests.Application.Features.Browse.GetStudentSubject;

public sealed class GetStudentSubjectHandlerTests
{
    private readonly ISubjectRepository _subjectRepository = Substitute.For<ISubjectRepository>();
    private readonly ICurriculumUnitRepository _unitRepository = Substitute.For<ICurriculumUnitRepository>();
    private readonly ILessonRepository _lessonRepository = Substitute.For<ILessonRepository>();
    private readonly IQuestionMasteryRepository _questionMasteryRepository = Substitute.For<IQuestionMasteryRepository>();
    private readonly ISessionRepository _sessionRepository = Substitute.For<ISessionRepository>();
    private readonly ICurrentUserService _currentUserService = Substitute.For<ICurrentUserService>();
    private readonly Guid _studentId = Guid.NewGuid();
    private readonly Subject _subject = Subject.Create("Physics", 1, Guid.NewGuid());
    private readonly CurriculumUnit _mechanics;
    private readonly CurriculumUnit _waves;
    private readonly GetStudentSubjectHandler _handler;

    public GetStudentSubjectHandlerTests()
    {
        _currentUserService.UserId.Returns(_studentId);
        _mechanics = CurriculumUnit.Create(_subject, "Mechanics", 1, Guid.NewGuid());
        _waves = CurriculumUnit.Create(_subject, "Waves", 2, Guid.NewGuid());
        List<CurriculumUnit> units = [_mechanics, _waves, CurriculumUnit.Create(Subject.Create("Chemistry", 2, Guid.NewGuid()), "Atoms", 1, Guid.NewGuid())];
        _subjectRepository.GetByIdAsync(_subject.Id, Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<Subject>, IQueryable<Subject>>?>(), Arg.Any<bool>()).Returns(_subject);
        _unitRepository.FindAsync(Arg.Any<Expression<Func<CurriculumUnit, bool>>>(), Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<CurriculumUnit>, IQueryable<CurriculumUnit>>?>(), Arg.Any<Func<IQueryable<CurriculumUnit>, IOrderedQueryable<CurriculumUnit>>?>(), Arg.Any<bool>())
            .Returns(call => units.Where(call.Arg<Expression<Func<CurriculumUnit, bool>>>().Compile()).ToList());
        _lessonRepository.CountByUnitAsync(Arg.Any<IReadOnlyCollection<Guid>>(), true, Arg.Any<CancellationToken>()).Returns(new Dictionary<Guid, int> { [_mechanics.Id] = 2 });
        _questionMasteryRepository.GetLessonCountsAsync(_studentId, _subject.Id, Arg.Any<CancellationToken>()).Returns([]);
        _sessionRepository.GetBestExamScoresAsync(_studentId, Arg.Any<CancellationToken>()).Returns([]);
        _handler = new GetStudentSubjectHandler(_subjectRepository, _unitRepository, _lessonRepository, _questionMasteryRepository, _sessionRepository, _currentUserService);
    }

    [Fact]
    public async Task Handle_NoCurrentUser_ThrowsUserNotAuthenticated()
    {
        _currentUserService.UserId.Returns((Guid?)null);

        var act = () => _handler.Handle(new GetStudentSubjectQuery(_subject.Id), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<UnauthorizedCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.UserNotAuthenticated);
    }

    [Fact]
    public async Task Handle_UnknownSubject_ThrowsSubjectNotFound()
    {
        var act = () => _handler.Handle(new GetStudentSubjectQuery(Guid.NewGuid()), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<NotFoundCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.SubjectNotFound);
    }

    [Fact]
    public async Task Handle_Subject_ReturnsUnitsInOrderWithMasteryLessonCountAndBestScore()
    {
        _questionMasteryRepository.GetLessonCountsAsync(_studentId, _subject.Id, Arg.Any<CancellationToken>()).Returns([Count(_mechanics, 4, 2, 3), Count(_mechanics, 4, 0, 0)]);
        _sessionRepository.GetBestExamScoresAsync(_studentId, Arg.Any<CancellationToken>()).Returns([new ExamBestScore(new UnitExamScope(_mechanics.Id).ToKey(), 72.4m), new ExamBestScore($"multi:{Guid.NewGuid():D}", 90m)]);

        var result = await _handler.Handle(new GetStudentSubjectQuery(_subject.Id), TestContext.Current.CancellationToken);

        (result.Id, result.Name, result.ServableCount, result.MasteredCount, result.SeenCount, result.MasteryPercent).Should().Be((_subject.Id, "Physics", 8, 2, 3, 25));
        result.Units.Select(x => x.Id).Should().Equal(_mechanics.Id, _waves.Id);
        var mechanics = result.Units[0];
        (mechanics.Name, mechanics.LessonCount, mechanics.ServableCount, mechanics.MasteredCount, mechanics.SeenCount, mechanics.MasteryPercent, mechanics.BestExamScorePercent).Should().Be(("Mechanics", 2, 8, 2, 3, 25, 72.4m));
    }

    [Fact]
    public async Task Handle_UnitWithoutServableQuestionsOrExam_ReturnsZerosAndNullBest()
    {
        var result = await _handler.Handle(new GetStudentSubjectQuery(_subject.Id), TestContext.Current.CancellationToken);

        var waves = result.Units.Single(x => x.Id == _waves.Id);
        (waves.LessonCount, waves.ServableCount, waves.MasteredCount, waves.SeenCount, waves.MasteryPercent, waves.BestExamScorePercent).Should().Be((0, 0, 0, 0, 0, (decimal?)null));
    }

    private LessonMasteryCount Count(CurriculumUnit unit, int servable, int mastered, int seen) => new(_subject.Id, 1, unit.Id, unit.Order, Guid.NewGuid(), 1, servable, mastered, seen);
}
