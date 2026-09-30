using Elmanhg.Application.MathStepGrading.FailMathStepGrade;
using Elmanhg.Application.Shared.Options;
using Elmanhg.Domain.MathStepGrading;
using Elmanhg.Domain.Questions.Grading;
using Elmanhg.Tests.Builders;
using FluentAssertions;
using Microsoft.Extensions.Options;
using NSubstitute;
using System.Linq.Expressions;

namespace Elmanhg.Tests.Application.Features.MathStepGrading.FailMathStepGrade;

public sealed class FailMathStepGradeHandlerTests
{
    private static readonly DateTimeOffset Now = MathStepGradeBuilder.DefaultRequestedAt.AddMinutes(1);
    private readonly IMathStepGradeRepository _mathStepGradeRepository = Substitute.For<IMathStepGradeRepository>();
    private readonly TimeProvider _timeProvider = Substitute.For<TimeProvider>();
    private readonly List<MathStepGrade> _grades = [];

    public FailMathStepGradeHandlerTests()
    {
        _timeProvider.GetUtcNow().Returns(Now);
        _mathStepGradeRepository.FirstOrDefaultAsync(Arg.Any<Expression<Func<MathStepGrade, bool>>>(), Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<MathStepGrade>, IQueryable<MathStepGrade>>?>(), Arg.Any<Func<IQueryable<MathStepGrade>, IOrderedQueryable<MathStepGrade>>?>(), Arg.Any<bool>())
            .Returns(call => _grades.FirstOrDefault(call.Arg<Expression<Func<MathStepGrade, bool>>>().Compile()));
    }

    [Fact]
    public async Task Handle_Pending_SchedulesRetryAndSaves()
    {
        var grade = Seed();

        await Handle(grade.Id, new MathStepGradingOptions());

        (grade.Attempts, grade.NextAttemptAt, grade.LastErrorCode).Should().Be((1, (DateTimeOffset?)Now.AddSeconds(30), "MATH_STEP_GRADING_UNAVAILABLE"));
        await _mathStepGradeRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_LastAttempt_MarksInReview()
    {
        var grade = Seed();

        await Handle(grade.Id, new MathStepGradingOptions { MaxAttempts = 1 });

        (grade.Status, grade.ReviewReason).Should().Be((MathStepGradeStatus.InReview, (MathStepReviewReason?)MathStepReviewReason.GradingFailed));
        await _mathStepGradeRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_NotPending_DoesNothing()
    {
        var grade = Seed();
        grade.Complete(MathStepGradeBuilder.Assessment(), new QuestionGrade(1.5m, 0.75m, GradeOutcome.Partial, null), 0.7m, Now);

        await Handle(grade.Id, new MathStepGradingOptions());

        grade.Status.Should().Be(MathStepGradeStatus.Graded);
        await _mathStepGradeRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    private MathStepGrade Seed()
    {
        var grade = new MathStepGradeBuilder().Build();
        _grades.Add(grade);
        return grade;
    }

    private Task Handle(Guid gradeId, MathStepGradingOptions options) => new FailMathStepGradeHandler(_mathStepGradeRepository, Options.Create(options), _timeProvider).Handle(new FailMathStepGradeCommand(gradeId, "MATH_STEP_GRADING_UNAVAILABLE"), TestContext.Current.CancellationToken);
}
