using Elmanhg.Application.EssayGrading.FailEssayGrade;
using Elmanhg.Application.Shared.Options;
using Elmanhg.Domain.EssayGrading;
using Elmanhg.Domain.Questions.Grading;
using Elmanhg.Tests.Builders;
using FluentAssertions;
using Microsoft.Extensions.Options;
using NSubstitute;
using System.Linq.Expressions;

namespace Elmanhg.Tests.Application.Features.EssayGrading.FailEssayGrade;

public sealed class FailEssayGradeHandlerTests
{
    private static readonly DateTimeOffset Now = EssayGradeBuilder.DefaultRequestedAt.AddMinutes(1);
    private readonly IEssayGradeRepository _essayGradeRepository = Substitute.For<IEssayGradeRepository>();
    private readonly TimeProvider _timeProvider = Substitute.For<TimeProvider>();
    private readonly List<EssayGrade> _grades = [];

    public FailEssayGradeHandlerTests()
    {
        _timeProvider.GetUtcNow().Returns(Now);
        _essayGradeRepository.FirstOrDefaultAsync(Arg.Any<Expression<Func<EssayGrade, bool>>>(), Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<EssayGrade>, IQueryable<EssayGrade>>?>(), Arg.Any<Func<IQueryable<EssayGrade>, IOrderedQueryable<EssayGrade>>?>(), Arg.Any<bool>())
            .Returns(call => _grades.FirstOrDefault(call.Arg<Expression<Func<EssayGrade, bool>>>().Compile()));
    }

    [Fact]
    public async Task Handle_PendingGrade_RecordsFailedAttemptAndSaves()
    {
        var grade = Seed();

        await Handle(grade.Id, new EssayGradingOptions());

        (grade.Attempts, grade.NextAttemptAt, grade.LastErrorCode).Should().Be((1, (DateTimeOffset?)Now.AddSeconds(30), "ESSAY_GRADING_UNAVAILABLE"));
        await _essayGradeRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_LastAttempt_MarksInReview()
    {
        var grade = Seed();

        await Handle(grade.Id, new EssayGradingOptions { MaxAttempts = 1 });

        (grade.Status, grade.ReviewReason).Should().Be((EssayGradeStatus.InReview, (EssayReviewReason?)EssayReviewReason.GradingFailed));
        await _essayGradeRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_NotPending_DoesNothing()
    {
        var grade = Seed();
        grade.Complete(EssayGradeBuilder.Assessment(), new QuestionGrade(2.5m, 0.5m, GradeOutcome.Partial, null), 0.7m, Now);

        await Handle(grade.Id, new EssayGradingOptions());

        grade.Status.Should().Be(EssayGradeStatus.Graded);
        await _essayGradeRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_Missing_DoesNothing()
    {
        await Handle(Guid.NewGuid(), new EssayGradingOptions());

        await _essayGradeRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    private EssayGrade Seed()
    {
        var grade = new EssayGradeBuilder().Build();
        _grades.Add(grade);
        return grade;
    }

    private Task Handle(Guid gradeId, EssayGradingOptions options) => new FailEssayGradeHandler(_essayGradeRepository, Options.Create(options), _timeProvider).Handle(new FailEssayGradeCommand(gradeId, "ESSAY_GRADING_UNAVAILABLE"), TestContext.Current.CancellationToken);
}
