using Core.Errors;
using Elmanhg.Application.EssayGrading.GradeEssay;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Shared.AiService;
using Elmanhg.Application.Shared.Options;
using Elmanhg.Domain.EssayGrading;
using Elmanhg.Domain.Lessons;
using Elmanhg.Domain.Questions;
using Elmanhg.Domain.Questions.Grading;
using Elmanhg.Domain.Subjects;
using Elmanhg.Domain.Units;
using Elmanhg.Infrastructure.RichText;
using Elmanhg.Tests.Builders;
using FluentAssertions;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using System.Linq.Expressions;
using System.Text.Json;

namespace Elmanhg.Tests.Application.Features.EssayGrading.GradeEssay;

public sealed class GradeEssayHandlerTests
{
    private const string EditedSpec = """{"criteria":[{"id":"c1","title":"Definition","points":4,"levels":[{"points":0,"description":"Missing"},{"points":4,"description":"Complete"}]}],"modelAnswers":["<p>Inertia is resistance to change in motion.</p>"]}""";
    private static readonly DateTimeOffset Now = EssayGradeBuilder.DefaultRequestedAt.AddMinutes(1);
    private readonly IEssayGradeRepository _essayGradeRepository = Substitute.For<IEssayGradeRepository>();
    private readonly IQuestionRepository _questionRepository = Substitute.For<IQuestionRepository>();
    private readonly ILessonRepository _lessonRepository = Substitute.For<ILessonRepository>();
    private readonly ICurriculumUnitRepository _unitRepository = Substitute.For<ICurriculumUnitRepository>();
    private readonly ISubjectRepository _subjectRepository = Substitute.For<ISubjectRepository>();
    private readonly IAiEssayGradingClient _essayGradingClient = Substitute.For<IAiEssayGradingClient>();
    private readonly TimeProvider _timeProvider = Substitute.For<TimeProvider>();
    private readonly QuestionBuilder _questions = new();
    private readonly Question _question;
    private readonly List<EssayGrade> _grades = [];
    private readonly List<Lesson> _lessons = [];
    private AiEssayGradingRequest? _captured;

    public GradeEssayHandlerTests()
    {
        _question = _questions.Essay().Build();
        _lessons.Add(_questions.Lesson);
        _timeProvider.GetUtcNow().Returns(Now);
        StubRepositories();
        _essayGradingClient.GradeAsync(Arg.Do<AiEssayGradingRequest>(x => _captured = x), Arg.Any<CancellationToken>()).Returns(Reply(0.9m));
    }

    [Fact]
    public async Task Handle_DueGrade_GradesAgainstServedRevisionAndSaves()
    {
        var grade = Seed();
        _question.Update(QuestionType.Essay, QuestionBuilder.EssayContent() with { GradingSpec = EditedSpec }, new QuestionMetadata(QuestionDifficulty.Medium, null, []), _questions.Lesson, Guid.NewGuid());

        await Handle(grade.Id);

        _captured!.Criteria[0].Points.Should().Be(2);
        (grade.Status, grade.Score).Should().Be((EssayGradeStatus.Graded, (decimal?)2.5m));
        await _essayGradeRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_LowConfidence_MarksInReview()
    {
        _essayGradingClient.GradeAsync(Arg.Any<AiEssayGradingRequest>(), Arg.Any<CancellationToken>()).Returns(Reply(0.3m));
        var grade = Seed();

        await Handle(grade.Id);

        (grade.Status, grade.ReviewReason).Should().Be((EssayGradeStatus.InReview, (EssayReviewReason?)EssayReviewReason.LowConfidence));
        await _essayGradeRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_NotDue_DoesNothing()
    {
        var grade = Seed();
        grade.FailAttempt("ESSAY_GRADING_UNAVAILABLE", Now, 4, TimeSpan.FromSeconds(30));

        await Handle(grade.Id);

        await ShouldNotGrade();
    }

    [Fact]
    public async Task Handle_GradedAwaitingApplication_DoesNotCallAiAgain()
    {
        var grade = Seed();
        grade.Complete(EssayGradeBuilder.Assessment(), new QuestionGrade(2.5m, 0.5m, GradeOutcome.Partial, null), 0.7m, Now);

        await Handle(grade.Id);

        grade.IsAwaitingApplication.Should().BeTrue();
        await ShouldNotGrade();
    }

    [Fact]
    public async Task Handle_Missing_DoesNothing()
    {
        await Handle(Guid.NewGuid());

        await ShouldNotGrade();
        await _questionRepository.DidNotReceive().GetRevisionsAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_RevisionMissing_ThrowsQuestionNotFound()
    {
        var grade = new EssayGradeBuilder().ForQuestion(_question.Id, 9).Build();
        _grades.Add(grade);

        var act = () => Handle(grade.Id);

        (await act.Should().ThrowAsync<NotFoundCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.QuestionNotFound);
        await _essayGradeRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_QuestionMissing_ThrowsQuestionNotFound()
    {
        _questionRepository.FirstOrDefaultAsync(Arg.Any<Expression<Func<Question, bool>>>(), Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<Question>, IQueryable<Question>>?>(), Arg.Any<Func<IQueryable<Question>, IOrderedQueryable<Question>>?>(), Arg.Any<bool>())
            .Returns((Question?)null);
        var grade = Seed();

        var act = () => Handle(grade.Id);

        (await act.Should().ThrowAsync<NotFoundCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.QuestionNotFound);
        await ShouldNotGrade();
    }

    [Fact]
    public async Task Handle_LessonGone_GradesWithoutContext()
    {
        _lessons.Clear();
        var grade = Seed();

        await Handle(grade.Id);

        (_captured!.Subject, grade.Status).Should().Be(((string?)null, EssayGradeStatus.Graded));
        _captured.Objectives.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_AiUnavailable_PropagatesWithoutSaving()
    {
        _essayGradingClient.GradeAsync(Arg.Any<AiEssayGradingRequest>(), Arg.Any<CancellationToken>()).ThrowsAsync(new ServiceUnavailableCoreException(ErrorCodes.EssayGradingUnavailable));
        var grade = Seed();

        var act = () => Handle(grade.Id);

        (await act.Should().ThrowAsync<ServiceUnavailableCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.EssayGradingUnavailable);
        grade.Status.Should().Be(EssayGradeStatus.Pending);
        await _essayGradeRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_Request_OmitsStudentIdentity()
    {
        var grade = Seed();

        await Handle(grade.Id);

        var json = JsonSerializer.Serialize(_captured);
        json.Should().NotContain(grade.StudentId.ToString()).And.NotContain(grade.SessionId.ToString());
        _captured!.Subject.Should().Be("Physics");
    }

    private static AiEssayGradingResult Reply(decimal confidence) => new([new AiEssayCriterionScore("c1", 1, "Partly correct.")], 1, 2, "Good definition; add an example.", confidence, "claude-sonnet-5", "v1", 900, 150, "end_turn", 0.004m);

    private void StubRepositories()
    {
        _essayGradeRepository.FirstOrDefaultAsync(Arg.Any<Expression<Func<EssayGrade, bool>>>(), Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<EssayGrade>, IQueryable<EssayGrade>>?>(), Arg.Any<Func<IQueryable<EssayGrade>, IOrderedQueryable<EssayGrade>>?>(), Arg.Any<bool>())
            .Returns(call => _grades.FirstOrDefault(call.Arg<Expression<Func<EssayGrade, bool>>>().Compile()));
        _lessonRepository.FirstOrDefaultAsync(Arg.Any<Expression<Func<Lesson, bool>>>(), Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<Lesson>, IQueryable<Lesson>>?>(), Arg.Any<Func<IQueryable<Lesson>, IOrderedQueryable<Lesson>>?>(), Arg.Any<bool>())
            .Returns(call => _lessons.FirstOrDefault(call.Arg<Expression<Func<Lesson, bool>>>().Compile()));
        _unitRepository.FirstOrDefaultAsync(Arg.Any<Expression<Func<CurriculumUnit, bool>>>(), Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<CurriculumUnit>, IQueryable<CurriculumUnit>>?>(), Arg.Any<Func<IQueryable<CurriculumUnit>, IOrderedQueryable<CurriculumUnit>>?>(), Arg.Any<bool>())
            .Returns(call => new[] { _questions.Unit }.FirstOrDefault(call.Arg<Expression<Func<CurriculumUnit, bool>>>().Compile()));
        _subjectRepository.FirstOrDefaultAsync(Arg.Any<Expression<Func<Subject, bool>>>(), Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<Subject>, IQueryable<Subject>>?>(), Arg.Any<Func<IQueryable<Subject>, IOrderedQueryable<Subject>>?>(), Arg.Any<bool>())
            .Returns(call => new[] { _questions.Subject }.FirstOrDefault(call.Arg<Expression<Func<Subject, bool>>>().Compile()));
        _questionRepository.FirstOrDefaultAsync(Arg.Any<Expression<Func<Question, bool>>>(), Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<Question>, IQueryable<Question>>?>(), Arg.Any<Func<IQueryable<Question>, IOrderedQueryable<Question>>?>(), Arg.Any<bool>())
            .Returns(call => new[] { _question }.FirstOrDefault(call.Arg<Expression<Func<Question, bool>>>().Compile()));
        _questionRepository.GetRevisionsAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>()).Returns(_ => _question.Revisions.ToList());
    }

    private EssayGrade Seed()
    {
        var grade = new EssayGradeBuilder().ForQuestion(_question.Id, 1).Build();
        _grades.Add(grade);
        return grade;
    }

    private async Task ShouldNotGrade()
    {
        await _essayGradingClient.DidNotReceive().GradeAsync(Arg.Any<AiEssayGradingRequest>(), Arg.Any<CancellationToken>());
        await _essayGradeRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    private Task Handle(Guid gradeId) => new GradeEssayHandler(_essayGradeRepository, _questionRepository, _lessonRepository, _unitRepository, _subjectRepository, new RichTextExtractor(), _essayGradingClient, Options.Create(new EssayGradingOptions()), _timeProvider).Handle(new GradeEssayCommand(gradeId), TestContext.Current.CancellationToken);
}
