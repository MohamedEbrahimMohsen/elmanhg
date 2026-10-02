using Core.Errors;
using Core.Identity.Tokens.CurrentUser;
using Elmanhg.Application.Browse.GetStudentLesson;
using Elmanhg.Application.Browse.Shared;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Shared.Options;
using Elmanhg.Domain.Lessons;
using Elmanhg.Domain.Mastery;
using Elmanhg.Domain.Subjects;
using Elmanhg.Domain.Subscriptions;
using Elmanhg.Domain.Units;
using Elmanhg.Tests.Application.Features.Subscriptions;
using Elmanhg.Tests.Fixtures.RuntimeSettings;
using FluentAssertions;
using Microsoft.Extensions.Options;
using NSubstitute;
using System.Linq.Expressions;

namespace Elmanhg.Tests.Application.Features.Browse.GetStudentLesson;

public sealed class GetStudentLessonFreeTierTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);
    private readonly ILessonRepository _lessonRepository = Substitute.For<ILessonRepository>();
    private readonly ICurriculumUnitRepository _unitRepository = Substitute.For<ICurriculumUnitRepository>();
    private readonly ISubjectRepository _subjectRepository = Substitute.For<ISubjectRepository>();
    private readonly IQuestionMasteryRepository _questionMasteryRepository = Substitute.For<IQuestionMasteryRepository>();
    private readonly ISubscriptionRepository _subscriptionRepository = Substitute.For<ISubscriptionRepository>();
    private readonly TimeProvider _timeProvider = Substitute.For<TimeProvider>();
    private readonly ICurrentUserService _currentUserService = Substitute.For<ICurrentUserService>();
    private readonly Guid _studentId = Guid.NewGuid();
    private readonly Subject _subject = Subject.Create("Physics", 1, Guid.NewGuid());
    private readonly CurriculumUnit _mechanics;
    private readonly CurriculumUnit _waves;
    private readonly List<CurriculumUnit> _units;
    private readonly List<Lesson> _lessons = [];
    private readonly GetStudentLessonHandler _handler;

    public GetStudentLessonFreeTierTests()
    {
        _currentUserService.UserId.Returns(_studentId);
        _mechanics = CurriculumUnit.Create(_subject, "Mechanics", 1, Guid.NewGuid());
        _waves = CurriculumUnit.Create(_subject, "Waves", 2, Guid.NewGuid());
        _units = [_mechanics, _waves];
        _lessonRepository.GetWithObjectivesAsync(Arg.Any<Guid>(), Arg.Any<bool>(), Arg.Any<CancellationToken>()).Returns(call => _lessons.FirstOrDefault(x => x.Id == call.Arg<Guid>()));
        _lessonRepository.FindAsync(Arg.Any<Expression<Func<Lesson, bool>>>(), Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<Lesson>, IQueryable<Lesson>>?>(), Arg.Any<Func<IQueryable<Lesson>, IOrderedQueryable<Lesson>>?>(), Arg.Any<bool>())
            .Returns(call => _lessons.Where(call.Arg<Expression<Func<Lesson, bool>>>().Compile()).ToList());
        _unitRepository.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<CurriculumUnit>, IQueryable<CurriculumUnit>>?>(), Arg.Any<bool>()).Returns(call => _units.FirstOrDefault(x => x.Id == call.Arg<Guid>()));
        _unitRepository.FindAsync(Arg.Any<Expression<Func<CurriculumUnit, bool>>>(), Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<CurriculumUnit>, IQueryable<CurriculumUnit>>?>(), Arg.Any<Func<IQueryable<CurriculumUnit>, IOrderedQueryable<CurriculumUnit>>?>(), Arg.Any<bool>())
            .Returns(call => _units.Where(call.Arg<Expression<Func<CurriculumUnit, bool>>>().Compile()).ToList());
        _subjectRepository.GetByIdAsync(_subject.Id, Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<Subject>, IQueryable<Subject>>?>(), Arg.Any<bool>()).Returns(_subject);
        _questionMasteryRepository.GetLessonCountsAsync(_studentId, _subject.Id, Arg.Any<CancellationToken>()).Returns([]);
        _timeProvider.GetUtcNow().Returns(Now);
        SubscriptionRepositoryStub.Stub(_subscriptionRepository);
        _handler = new GetStudentLessonHandler(_lessonRepository, _unitRepository, _subjectRepository, _questionMasteryRepository, _subscriptionRepository, Options.Create(new SubscriptionsOptions()), _timeProvider, _currentUserService, new FakeRuntimeSettings());
    }

    [Fact]
    public async Task Handle_FreeStudentLockedLesson_ReturnsLockedResultWithoutContent()
    {
        AddLesson("Forces", 1);
        var energy = AddLesson("Energy", 2);
        _questionMasteryRepository.GetLessonCountsAsync(_studentId, _subject.Id, Arg.Any<CancellationToken>()).Returns([new LessonMasteryCount(_subject.Id, 1, _mechanics.Id, 1, energy.Id, 2, 4, 1, 2)]);

        var result = await Handle(energy);

        (result.IsLocked, result.Explanation, result.Summary, result.VideoUrl).Should().Be((true, string.Empty, string.Empty, (string?)null));
        result.Objectives.Should().BeEmpty();
        (result.Name, result.UnitName, result.ServableCount).Should().Be(("Energy", "Mechanics", 4));
    }

    [Fact]
    public async Task Handle_FreeStudentFirstLesson_ReturnsContent()
    {
        var forces = AddLesson("Forces", 1);
        AddLesson("Energy", 2);

        var result = await Handle(forces);

        (result.IsLocked, result.Explanation).Should().Be((false, "<p>Forces explained</p>"));
    }

    [Fact]
    public async Task Handle_SubscribedStudentSecondLesson_ReturnsContent()
    {
        SubscriptionRepositoryStub.Stub(_subscriptionRepository, SubscriptionRepositoryStub.EntitledBase(_studentId, Now));
        AddLesson("Forces", 1);
        var energy = AddLesson("Energy", 2);

        var result = await Handle(energy);

        result.IsLocked.Should().BeFalse();
        result.Objectives.Select(x => x.Text).Should().Equal("Define Energy");
    }

    private Task<StudentLessonResult> Handle(Lesson lesson) => _handler.Handle(new GetStudentLessonQuery(lesson.Id), TestContext.Current.CancellationToken);

    private Lesson AddLesson(string name, int order)
    {
        var lesson = Lesson.Create(_mechanics, name, order, Guid.NewGuid());
        lesson.Update(name, $"<p>{name} explained</p>", $"<p>{name} summary</p>", "https://video.example.com/" + order, [new LessonObjectiveContent(null, $"Define {name}")], Guid.NewGuid());
        lesson.Publish(Guid.NewGuid());
        _lessons.Add(lesson);
        return lesson;
    }
}
