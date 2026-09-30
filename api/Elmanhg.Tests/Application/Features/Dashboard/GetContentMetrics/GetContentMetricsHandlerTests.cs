using Core.Errors;
using Elmanhg.Application.Dashboard.GetContentMetrics;
using Elmanhg.Application.Exceptions;
using Elmanhg.Domain.Lessons;
using Elmanhg.Domain.Questions;
using Elmanhg.Domain.Subjects;
using Elmanhg.Domain.Units;
using FluentAssertions;
using NSubstitute;
using System.Linq.Expressions;

namespace Elmanhg.Tests.Application.Features.Dashboard.GetContentMetrics;

public sealed class GetContentMetricsHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 15, 10, 0, 0, TimeSpan.Zero);
    private readonly ISubjectRepository _subjectRepository = Substitute.For<ISubjectRepository>();
    private readonly ICurriculumUnitRepository _unitRepository = Substitute.For<ICurriculumUnitRepository>();
    private readonly ILessonRepository _lessonRepository = Substitute.For<ILessonRepository>();
    private readonly IQuestionRepository _questionRepository = Substitute.For<IQuestionRepository>();
    private readonly TimeProvider _timeProvider = Substitute.For<TimeProvider>();
    private readonly Subject _subject = Subject.Create("Physics", 1, Guid.NewGuid());
    private readonly GetContentMetricsHandler _handler;

    public GetContentMetricsHandlerTests()
    {
        _timeProvider.GetUtcNow().Returns(Now);
        _subjectRepository.GetByIdAsync(_subject.Id, Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<Subject>, IQueryable<Subject>>?>(), Arg.Any<bool>()).Returns(_subject);
        _lessonRepository.CountByStateAsync(Arg.Any<Guid?>(), Arg.Any<CancellationToken>()).Returns([]);
        _questionRepository.CountInventoryAsync(Arg.Any<Guid?>(), Arg.Any<CancellationToken>()).Returns([]);
        _handler = new GetContentMetricsHandler(_subjectRepository, _unitRepository, _lessonRepository, _questionRepository, _timeProvider);
    }

    [Fact]
    public async Task Handle_UnknownSubject_ThrowsSubjectNotFound()
    {
        var act = () => _handler.Handle(new GetContentMetricsQuery(Guid.NewGuid()), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<NotFoundCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.SubjectNotFound);
        await _questionRepository.DidNotReceive().CountInventoryAsync(Arg.Any<Guid?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_SplitsRetiredOutOfTheirStatus()
    {
        _questionRepository.CountInventoryAsync(null, Arg.Any<CancellationToken>()).Returns(
        [
            new QuestionInventoryCount(QuestionValidationStatus.Approved, QuestionType.Mcq, true, 2),
            new QuestionInventoryCount(QuestionValidationStatus.Approved, QuestionType.Mcq, false, 3),
            new QuestionInventoryCount(QuestionValidationStatus.Pending, QuestionType.Fill, false, 1),
        ]);

        var result = await _handler.Handle(new GetContentMetricsQuery(null), TestContext.Current.CancellationToken);

        result.QuestionsApproved.Should().Be(3);
        result.QuestionsRetired.Should().Be(2);
        result.QuestionsPending.Should().Be(1);
        result.QuestionsRejected.Should().Be(0);
        result.QuestionsByType.Should().Equal(Enum.GetValues<QuestionType>().Select(type => new QuestionTypeCountResult(type, type switch
        {
            QuestionType.Mcq => 5,
            QuestionType.Fill => 1,
            _ => 0,
        })));
    }

    [Fact]
    public async Task Handle_NoSubject_UsesGlobalServableCountAndSubjectTotal()
    {
        _subjectRepository.CountAsync(Arg.Any<CancellationToken>(), Arg.Any<Expression<Func<Subject, bool>>?>()).Returns(4);
        _questionRepository.CountServableAsync(Arg.Any<CancellationToken>()).Returns(40);

        var result = await _handler.Handle(new GetContentMetricsQuery(null), TestContext.Current.CancellationToken);

        result.Subjects.Should().Be(4);
        result.ServableTotal.Should().Be(40);
        result.SubjectId.Should().BeNull();
        result.GeneratedAt.Should().Be(Now);
        await _questionRepository.DidNotReceive().CountServableInSubjectAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WithSubject_UsesSubjectServableCountAndOneSubject()
    {
        _questionRepository.CountServableInSubjectAsync(_subject.Id, Arg.Any<CancellationToken>()).Returns(12);
        _unitRepository.CountAsync(Arg.Any<CancellationToken>(), Arg.Any<Expression<Func<CurriculumUnit, bool>>?>()).Returns(3);

        var result = await _handler.Handle(new GetContentMetricsQuery(_subject.Id), TestContext.Current.CancellationToken);

        result.Subjects.Should().Be(1);
        result.Units.Should().Be(3);
        result.ServableTotal.Should().Be(12);
        result.SubjectId.Should().Be(_subject.Id);
        await _questionRepository.DidNotReceive().CountServableAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_MapsLessonStates()
    {
        _lessonRepository.CountByStateAsync(null, Arg.Any<CancellationToken>()).Returns([new LessonStateCount(LessonState.Draft, 2), new LessonStateCount(LessonState.Published, 5), new LessonStateCount(LessonState.Archived, 1)]);

        var result = await _handler.Handle(new GetContentMetricsQuery(null), TestContext.Current.CancellationToken);

        result.LessonsDraft.Should().Be(2);
        result.LessonsPublished.Should().Be(5);
        result.LessonsArchived.Should().Be(1);
    }
}
