using Core.Errors;
using Core.Identity.Tokens.CurrentUser;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.MathStepGrading.GetMathStepGrade;
using Elmanhg.Application.MathStepGrading.Shared;
using Elmanhg.Domain.MathStepGrading;
using Elmanhg.Domain.Questions.Grading;
using Elmanhg.Tests.Builders;
using FluentAssertions;
using NSubstitute;
using System.Linq.Expressions;

namespace Elmanhg.Tests.Application.Features.MathStepGrading.GetMathStepGrade;

public sealed class GetMathStepGradeHandlerTests
{
    private static readonly DateTimeOffset GradedAt = MathStepGradeBuilder.DefaultRequestedAt.AddSeconds(40);
    private static readonly QuestionGrade PartialGrade = new(1.5m, 0.75m, GradeOutcome.Partial, GradeFeedback.MathStepTally(1, 2));
    private readonly IMathStepGradeRepository _mathStepGradeRepository = Substitute.For<IMathStepGradeRepository>();
    private readonly ICurrentUserService _currentUserService = Substitute.For<ICurrentUserService>();
    private readonly Guid _studentId = Guid.NewGuid();
    private readonly List<MathStepGrade> _grades = [];
    private readonly GetMathStepGradeHandler _handler;

    public GetMathStepGradeHandlerTests()
    {
        _currentUserService.UserId.Returns(_studentId);
        _mathStepGradeRepository.FirstOrDefaultAsync(Arg.Any<Expression<Func<MathStepGrade, bool>>>(), Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<MathStepGrade>, IQueryable<MathStepGrade>>?>(), Arg.Any<Func<IQueryable<MathStepGrade>, IOrderedQueryable<MathStepGrade>>?>(), Arg.Any<bool>())
            .Returns(call => _grades.FirstOrDefault(call.Arg<Expression<Func<MathStepGrade, bool>>>().Compile()));
        _handler = new GetMathStepGradeHandler(_mathStepGradeRepository, _currentUserService);
    }

    [Fact]
    public async Task Handle_Pending_ReturnsPendingWithoutScore()
    {
        var grade = Seed(_studentId);

        var result = await Handle(grade);

        (result.Id, result.Status, result.MaxScore, result.RequestedAt).Should().Be((grade.Id, "Pending", 2, grade.RequestedAt));
        (result.Score, result.Outcome, result.FinalAnswerVerdict, result.Justification, result.GradedAt).Should().Be(((decimal?)null, (string?)null, (string?)null, (string?)null, (DateTimeOffset?)null));
        result.Steps.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_GradedAwaitingApplication_ReportsPending()
    {
        var grade = Seed(_studentId);
        grade.Complete(MathStepGradeBuilder.Assessment(), PartialGrade, 0.7m, GradedAt);

        var result = await Handle(grade);

        (result.Status, result.Score, result.FinalAnswerVerdict).Should().Be(("Pending", (decimal?)null, (string?)null));
        result.Steps.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_Applied_ReturnsScoreVerdictStepsAndJustification()
    {
        var grade = Seed(_studentId);
        grade.Complete(MathStepGradeBuilder.Assessment(), PartialGrade, 0.7m, GradedAt);
        grade.MarkApplied(GradedAt.AddSeconds(1));

        var result = await Handle(grade);

        (result.Status, result.Score, result.NormalisedScore, result.Outcome).Should().Be(("Graded", (decimal?)1.5m, (decimal?)0.75m, "Partial"));
        (result.FinalAnswerVerdict, result.Justification, result.GradedAt).Should().Be(("Equivalent", "جيد", (DateTimeOffset?)GradedAt));
        result.Steps.Should().Equal(new MathStepScoreResult(0, "2x = 4", 2, 2, "صحيحة"), new MathStepScoreResult(1, "x = 2", 1, 2, "ناقصة"));
    }

    [Fact]
    public async Task Handle_InReview_HidesScore()
    {
        var grade = Seed(_studentId);
        grade.Complete(MathStepGradeBuilder.Assessment(0.2m), PartialGrade, 0.7m, GradedAt);

        var result = await Handle(grade);

        (result.Status, result.Score, result.Outcome, result.Justification).Should().Be(("InReview", (decimal?)null, (string?)null, (string?)null));
        result.Steps.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_Missing_ThrowsMathStepGradeNotFound()
    {
        var grade = Seed(Guid.NewGuid());

        var act = () => Handle(grade);

        (await act.Should().ThrowAsync<NotFoundCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.MathStepGradeNotFound);
    }

    [Fact]
    public async Task Handle_NoCurrentUser_ThrowsUserNotAuthenticated()
    {
        _currentUserService.UserId.Returns((Guid?)null);
        var grade = Seed(_studentId);

        var act = () => Handle(grade);

        (await act.Should().ThrowAsync<UnauthorizedCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.UserNotAuthenticated);
    }

    private MathStepGrade Seed(Guid studentId)
    {
        var grade = new MathStepGradeBuilder().ForStudent(studentId).Build();
        _grades.Add(grade);
        return grade;
    }

    private Task<MathStepGradeResult> Handle(MathStepGrade grade) => _handler.Handle(new GetMathStepGradeQuery(grade.SessionId, grade.QuestionId), TestContext.Current.CancellationToken);
}
