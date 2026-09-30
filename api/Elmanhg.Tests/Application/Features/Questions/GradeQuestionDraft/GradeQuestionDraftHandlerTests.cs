using Core.Errors;
using Core.Localization;
using Elmanhg.Application.EssayGrading.Shared;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Questions.GradeQuestionDraft;
using Elmanhg.Application.Shared.AiService;
using Elmanhg.Application.Shared.Options;
using Elmanhg.Application.Shared.RichText;
using Elmanhg.Domain.Lessons;
using Elmanhg.Domain.Questions;
using Elmanhg.Domain.Subjects;
using Elmanhg.Domain.Units;
using Elmanhg.Infrastructure.RichText;
using Elmanhg.Tests.Builders;
using FluentAssertions;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using System.Linq.Expressions;
using static Elmanhg.Tests.Builders.QuestionBuilder;

namespace Elmanhg.Tests.Application.Features.Questions.GradeQuestionDraft;

public sealed class GradeQuestionDraftHandlerTests
{
    private readonly IRichTextSanitizer _richTextSanitizer = Substitute.For<IRichTextSanitizer>();
    private readonly ILocalizer _localizer = Substitute.For<ILocalizer>();
    private readonly ILessonRepository _lessonRepository = Substitute.For<ILessonRepository>();
    private readonly ICurriculumUnitRepository _unitRepository = Substitute.For<ICurriculumUnitRepository>();
    private readonly ISubjectRepository _subjectRepository = Substitute.For<ISubjectRepository>();
    private readonly IAiEssayGradingClient _essayGradingClient = Substitute.For<IAiEssayGradingClient>();
    private readonly GradeQuestionDraftHandler _handler;

    public GradeQuestionDraftHandlerTests()
    {
        _richTextSanitizer.Sanitize(Arg.Any<string?>()).Returns(x => x.Arg<string?>() ?? string.Empty);
        _handler = new GradeQuestionDraftHandler(_richTextSanitizer, new RichTextExtractor(), _lessonRepository, _unitRepository, _subjectRepository, _essayGradingClient, Options.Create(new EssayGradingOptions()), _localizer);
    }

    [Fact]
    public async Task Handle_CorrectMcq_ReturnsCorrectWithMaxScore()
    {
        var result = await _handler.Handle(new GradeQuestionDraftQuery(McqFields(), Json("""{"optionId":"b"}""")), TestContext.Current.CancellationToken);

        result.Should().Be(new QuestionGradeResult(1m, 1m, "Correct", 1, null));
    }

    [Fact]
    public async Task Handle_FillOneOfTwo_ReturnsPartial()
    {
        var draft = McqFields("<p>[[1]] m and [[2]] s</p>") with
        {
            Type = QuestionType.Fill,
            Body = Json("""{"blanks":[{"id":"1"},{"id":"2"}]}"""),
            GradingSpec = Json("""{"blanks":[{"id":"1","acceptedAnswers":["20"]},{"id":"2","acceptedAnswers":["5"]}]}"""),
            MaxScore = 2,
        };

        var result = await _handler.Handle(new GradeQuestionDraftQuery(draft, Json("""{"blanks":[{"id":"1","text":"20"},{"id":"2","text":"6"}]}""")), TestContext.Current.CancellationToken);

        result.Score.Should().Be(1m);
        result.Outcome.Should().Be("Partial");
    }

    [Fact]
    public async Task Handle_MultiPartial_ReturnsLocalizedFeedback()
    {
        _localizer.GetMessage("GRADE_FEEDBACK_CHOICE_TALLY", Arg.Any<string?>(), Arg.Any<Dictionary<string, object>?>()).Returns("tally");
        var draft = McqFields("<p>Vectors?</p>") with
        {
            Type = QuestionType.Multi,
            Body = Json("""{"options":[{"id":"a","text":"Force"},{"id":"b","text":"Velocity"},{"id":"c","text":"Mass"}]}"""),
            GradingSpec = Json("""{"correctOptionIds":["a","b"],"partialCredit":true}"""),
        };

        var result = await _handler.Handle(new GradeQuestionDraftQuery(draft, Json("""{"optionIds":["a"]}""")), TestContext.Current.CancellationToken);

        result.Should().Be(new QuestionGradeResult(0.5m, 0.5m, "Partial", 1, "tally"));
    }

    [Fact]
    public async Task Handle_Essay_SendsRubricToAiAndScalesAwards()
    {
        AiEssayGradingRequest? captured = null;
        _essayGradingClient.GradeAsync(Arg.Do<AiEssayGradingRequest>(x => captured = x), Arg.Any<CancellationToken>()).Returns(Reply(0.62m));

        var result = await _handler.Handle(new GradeQuestionDraftQuery(EssayFields(), Json("""{"text":"  Inertia resists change.  "}""")), TestContext.Current.CancellationToken);

        (result.Score, result.Outcome).Should().Be((2.5m, "Partial"));
        result.Essay!.Criteria.Should().Equal(new EssayCriterionResult("c1", "Definition", 1, 2, "Partly correct."));
        result.Essay.Confidence.Should().Be(0.62m);
        (captured!.Question, captured.Criteria[0].Levels.Count, captured.Essay, captured.Subject).Should().Be(("Explain inertia.", 3, "Inertia resists change.", (string?)null));
        captured.ModelAnswers.Should().Equal("Inertia is resistance to change in motion.");
        captured.Objectives.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_EssayWithLesson_SendsSubjectAndObjectives()
    {
        var questions = new QuestionBuilder();
        questions.Lesson.Update(questions.Lesson.Name, string.Empty, string.Empty, null, [new LessonObjectiveContent(questions.ObjectiveId, "First"), new LessonObjectiveContent(null, "Second")], Guid.NewGuid());
        StubContext(questions);
        AiEssayGradingRequest? captured = null;
        _essayGradingClient.GradeAsync(Arg.Do<AiEssayGradingRequest>(x => captured = x), Arg.Any<CancellationToken>()).Returns(Reply(0.9m));

        await _handler.Handle(new GradeQuestionDraftQuery(EssayFields(), Json("""{"text":"Inertia"}"""), questions.Lesson.Id), TestContext.Current.CancellationToken);

        captured!.Subject.Should().Be("Physics");
        captured.Objectives.Should().Equal("First", "Second");
    }

    [Fact]
    public async Task Handle_EssayUnknownLesson_ThrowsLessonNotFound()
    {
        StubContext(new QuestionBuilder());

        var act = () => _handler.Handle(new GradeQuestionDraftQuery(EssayFields(), Json("""{"text":"Inertia"}"""), Guid.NewGuid()), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<NotFoundCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.LessonNotFound);
        await _essayGradingClient.DidNotReceive().GradeAsync(Arg.Any<AiEssayGradingRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_BlankEssay_ReturnsUnansweredWithoutCallingAi()
    {
        _localizer.GetMessage("GRADE_FEEDBACK_UNANSWERED", Arg.Any<string?>(), Arg.Any<Dictionary<string, object>?>()).Returns("unanswered");

        var result = await _handler.Handle(new GradeQuestionDraftQuery(EssayFields(), Json("""{"text":"  "}""")), TestContext.Current.CancellationToken);

        result.Should().Be(new QuestionGradeResult(0m, 0m, "Incorrect", 5, "unanswered"));
        await _essayGradingClient.DidNotReceive().GradeAsync(Arg.Any<AiEssayGradingRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_EssayGraderUnavailable_Propagates()
    {
        _essayGradingClient.GradeAsync(Arg.Any<AiEssayGradingRequest>(), Arg.Any<CancellationToken>()).ThrowsAsync(new ServiceUnavailableCoreException(ErrorCodes.EssayGradingUnavailable));

        var act = () => _handler.Handle(new GradeQuestionDraftQuery(EssayFields(), Json("""{"text":"Inertia"}""")), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<ServiceUnavailableCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.EssayGradingUnavailable);
    }

    private static AiEssayGradingResult Reply(decimal confidence) => new([new AiEssayCriterionScore("c1", 1, "Partly correct.")], 1, 2, "Good definition; add an example.", confidence, "claude-sonnet-5", "v1", 900, 150, "end_turn", 0.004m);

    private void StubContext(QuestionBuilder questions)
    {
        _lessonRepository.FirstOrDefaultAsync(Arg.Any<Expression<Func<Lesson, bool>>>(), Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<Lesson>, IQueryable<Lesson>>?>(), Arg.Any<Func<IQueryable<Lesson>, IOrderedQueryable<Lesson>>?>(), Arg.Any<bool>())
            .Returns(call => new[] { questions.Lesson }.FirstOrDefault(call.Arg<Expression<Func<Lesson, bool>>>().Compile()));
        _unitRepository.FirstOrDefaultAsync(Arg.Any<Expression<Func<CurriculumUnit, bool>>>(), Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<CurriculumUnit>, IQueryable<CurriculumUnit>>?>(), Arg.Any<Func<IQueryable<CurriculumUnit>, IOrderedQueryable<CurriculumUnit>>?>(), Arg.Any<bool>())
            .Returns(call => new[] { questions.Unit }.FirstOrDefault(call.Arg<Expression<Func<CurriculumUnit, bool>>>().Compile()));
        _subjectRepository.FirstOrDefaultAsync(Arg.Any<Expression<Func<Subject, bool>>>(), Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<Subject>, IQueryable<Subject>>?>(), Arg.Any<Func<IQueryable<Subject>, IOrderedQueryable<Subject>>?>(), Arg.Any<bool>())
            .Returns(call => new[] { questions.Subject }.FirstOrDefault(call.Arg<Expression<Func<Subject, bool>>>().Compile()));
    }
}
