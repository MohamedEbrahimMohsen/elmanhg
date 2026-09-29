using Core.Errors;
using Core.Identity.Tokens.CurrentUser;
using Elmanhg.Application.Browse.GetStudentUnit;
using Elmanhg.Application.Browse.Shared;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Shared.Options;
using Elmanhg.Domain.Lessons;
using Elmanhg.Domain.Mastery;
using Elmanhg.Domain.Sessions;
using Elmanhg.Domain.Subjects;
using Elmanhg.Domain.Subscriptions;
using Elmanhg.Domain.Units;
using Elmanhg.Tests.Application.Features.Subscriptions;
using FluentAssertions;
using Microsoft.Extensions.Options;
using NSubstitute;
using System.Linq.Expressions;

namespace Elmanhg.Tests.Application.Features.Browse.GetStudentUnit;

public sealed class GetStudentUnitFreeTierTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);
    private readonly ICurriculumUnitRepository _unitRepository = Substitute.For<ICurriculumUnitRepository>();
    private readonly ISubjectRepository _subjectRepository = Substitute.For<ISubjectRepository>();
    private readonly ILessonRepository _lessonRepository = Substitute.For<ILessonRepository>();
    private readonly IQuestionMasteryRepository _questionMasteryRepository = Substitute.For<IQuestionMasteryRepository>();
    private readonly ISessionRepository _sessionRepository = Substitute.For<ISessionRepository>();
    private readonly ISubscriptionRepository _subscriptionRepository = Substitute.For<ISubscriptionRepository>();
    private readonly TimeProvider _timeProvider = Substitute.For<TimeProvider>();
    private readonly ICurrentUserService _currentUserService = Substitute.For<ICurrentUserService>();
    private readonly Guid _studentId = Guid.NewGuid();
    private readonly Subject _subject = Subject.Create("Physics", 1, Guid.NewGuid());
    private readonly CurriculumUnit _unit;
    private readonly List<Lesson> _lessons = [];
    private readonly GetStudentUnitHandler _handler;

    public GetStudentUnitFreeTierTests()
    {
        _currentUserService.UserId.Returns(_studentId);
        _unit = CurriculumUnit.Create(_subject, "Mechanics", 1, Guid.NewGuid());
        _unitRepository.GetByIdAsync(_unit.Id, Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<CurriculumUnit>, IQueryable<CurriculumUnit>>?>(), Arg.Any<bool>()).Returns(_unit);
        _subjectRepository.GetByIdAsync(_subject.Id, Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<Subject>, IQueryable<Subject>>?>(), Arg.Any<bool>()).Returns(_subject);
        _lessonRepository.FindAsync(Arg.Any<Expression<Func<Lesson, bool>>>(), Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<Lesson>, IQueryable<Lesson>>?>(), Arg.Any<Func<IQueryable<Lesson>, IOrderedQueryable<Lesson>>?>(), Arg.Any<bool>())
            .Returns(call => _lessons.Where(call.Arg<Expression<Func<Lesson, bool>>>().Compile()).ToList());
        _questionMasteryRepository.GetLessonCountsAsync(_studentId, _subject.Id, Arg.Any<CancellationToken>()).Returns([]);
        _sessionRepository.GetBestExamScoresAsync(_studentId, Arg.Any<CancellationToken>()).Returns([]);
        _timeProvider.GetUtcNow().Returns(Now);
        SubscriptionRepositoryStub.Stub(_subscriptionRepository);
        _handler = new GetStudentUnitHandler(_unitRepository, _subjectRepository, _lessonRepository, _questionMasteryRepository, _sessionRepository, _subscriptionRepository, Options.Create(new SubscriptionsOptions()), _timeProvider, _currentUserService);
    }

    [Fact]
    public async Task Handle_FreeStudent_LocksEveryLessonAfterTheFirst()
    {
        AddLesson("Forces");
        AddLesson("Energy");
        AddLesson("Momentum");

        var result = await _handler.Handle(new GetStudentUnitQuery(_unit.Id), TestContext.Current.CancellationToken);

        result.Lessons.Select(x => (x.Name, x.IsLocked)).Should().Equal(("Forces", false), ("Energy", true), ("Momentum", true));
    }

    [Fact]
    public async Task Handle_SubscribedStudent_LocksNoLesson()
    {
        SubscriptionRepositoryStub.Stub(_subscriptionRepository, SubscriptionRepositoryStub.EntitledBase(_studentId, Now));
        AddLesson("Forces");
        AddLesson("Energy");
        AddLesson("Momentum");

        var result = await _handler.Handle(new GetStudentUnitQuery(_unit.Id), TestContext.Current.CancellationToken);

        result.Lessons.Select(x => x.IsLocked).Should().Equal(false, false, false);
    }

    private void AddLesson(string name)
    {
        var lesson = Lesson.Create(_unit, name, _lessons.Count + 1, Guid.NewGuid());
        lesson.Publish(Guid.NewGuid());
        _lessons.Add(lesson);
    }
}
