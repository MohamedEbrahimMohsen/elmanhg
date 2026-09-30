using Core.Errors;
using Core.Identity.Tokens.CurrentUser;
using Elmanhg.Application.EssayGrading.GetEssayGrade;
using Elmanhg.Application.EssayGrading.Shared;
using Elmanhg.Application.Exceptions;
using Elmanhg.Domain.EssayGrading;
using Elmanhg.Domain.Questions.Grading;
using Elmanhg.Tests.Builders;
using FluentAssertions;
using NSubstitute;
using System.Linq.Expressions;

namespace Elmanhg.Tests.Application.Features.EssayGrading.GetEssayGrade;

public sealed class GetEssayGradeHandlerTests
{
    private static readonly DateTimeOffset GradedAt = EssayGradeBuilder.DefaultRequestedAt.AddSeconds(40);
    private static readonly QuestionGrade PartialGrade = new(2.5m, 0.5m, GradeOutcome.Partial, null);
    private readonly IEssayGradeRepository _essayGradeRepository = Substitute.For<IEssayGradeRepository>();
    private readonly ICurrentUserService _currentUserService = Substitute.For<ICurrentUserService>();
    private readonly Guid _studentId = Guid.NewGuid();
    private readonly List<EssayGrade> _grades = [];
    private readonly GetEssayGradeHandler _handler;

    public GetEssayGradeHandlerTests()
    {
        _currentUserService.UserId.Returns(_studentId);
        _essayGradeRepository.FirstOrDefaultAsync(Arg.Any<Expression<Func<EssayGrade, bool>>>(), Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<EssayGrade>, IQueryable<EssayGrade>>?>(), Arg.Any<Func<IQueryable<EssayGrade>, IOrderedQueryable<EssayGrade>>?>(), Arg.Any<bool>())
            .Returns(call => _grades.FirstOrDefault(call.Arg<Expression<Func<EssayGrade, bool>>>().Compile()));
        _handler = new GetEssayGradeHandler(_essayGradeRepository, _currentUserService);
    }

    [Fact]
    public async Task Handle_OwnGradedEssay_ReturnsScoreCriteriaAndJustification()
    {
        var grade = Seed(_studentId);
        grade.Complete(EssayGradeBuilder.Assessment(0.9m), PartialGrade, 0.7m, GradedAt);
        grade.MarkApplied(GradedAt.AddSeconds(1));

        var result = await Handle(grade);

        (result.Status, result.Score, result.Outcome, result.Justification, result.GradedAt).Should().Be(("Graded", (decimal?)2.5m, "Partial", "جيد", (DateTimeOffset?)GradedAt));
        result.Criteria.Should().Equal(new EssayCriterionResult("c1", "Definition", 1, 2, "ناقص"));
    }

    [Fact]
    public async Task Handle_GradedNotYetApplied_ReportsPendingWithoutScore()
    {
        var grade = Seed(_studentId);
        grade.Complete(EssayGradeBuilder.Assessment(0.9m), PartialGrade, 0.7m, GradedAt);

        var result = await Handle(grade);

        (result.Status, result.Score, result.Outcome, result.Justification, result.GradedAt).Should().Be(("Pending", (decimal?)null, (string?)null, (string?)null, (DateTimeOffset?)null));
        result.Criteria.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_Pending_HidesScore()
    {
        var grade = Seed(_studentId);

        var result = await Handle(grade);

        (result.Status, result.Score, result.Outcome, result.Justification, result.MaxScore).Should().Be(("Pending", (decimal?)null, (string?)null, (string?)null, 5));
        result.Criteria.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_InReview_HidesAiScore()
    {
        var grade = Seed(_studentId);
        grade.Complete(EssayGradeBuilder.Assessment(0.3m), PartialGrade, 0.7m, GradedAt);

        var result = await Handle(grade);

        (result.Status, result.Score).Should().Be(("InReview", (decimal?)null));
        result.Criteria.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_NotFound_ThrowsEssayGradeNotFound()
    {
        var grade = Seed(Guid.NewGuid());

        var act = () => Handle(grade);

        (await act.Should().ThrowAsync<NotFoundCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.EssayGradeNotFound);
    }

    [Fact]
    public async Task Handle_NoUser_ThrowsUnauthorized()
    {
        _currentUserService.UserId.Returns((Guid?)null);
        var grade = Seed(_studentId);

        var act = () => Handle(grade);

        (await act.Should().ThrowAsync<UnauthorizedCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.UserNotAuthenticated);
    }

    private EssayGrade Seed(Guid studentId)
    {
        var grade = new EssayGradeBuilder().ForStudent(studentId).Build();
        _grades.Add(grade);
        return grade;
    }

    private Task<EssayGradeResult> Handle(EssayGrade grade) => _handler.Handle(new GetEssayGradeQuery(grade.SessionId, grade.QuestionId), TestContext.Current.CancellationToken);
}
