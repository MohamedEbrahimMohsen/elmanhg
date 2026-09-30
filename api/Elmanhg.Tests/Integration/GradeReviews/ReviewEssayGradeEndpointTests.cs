using Elmanhg.Domain.EssayGrading;
using Elmanhg.Domain.Sessions;
using Elmanhg.Domain.TrainingData;
using Elmanhg.Infrastructure.Data.Context;
using Elmanhg.Tests.Integration.Authorization;
using Elmanhg.Tests.Integration.Content;
using Elmanhg.Tests.Integration.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using static Elmanhg.Tests.Integration.GradeReviews.GradeReviewTestData;

namespace Elmanhg.Tests.Integration.GradeReviews;

public sealed class ReviewEssayGradeEndpointTests(ApiFactory factory)
{
    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Post_Override_FinalisesGradeAndWritesAttemptMasteryAndTrainingRows()
    {
        var subjectId = await ScopeTestData.SeedSubjectAsync(factory, "Physics", CancellationToken);
        var (gradeId, sessionId, questionId, student) = await SeedInReviewEssayAsync(factory, subjectId);
        var (_, client) = await ReviewClientAsync(factory, subjectId);

        using var response = await client.PostAsJsonAsync(EssayRoute(subjectId, gradeId), new { decision = "Overridden", score = 4, comment = "Full marks for the definition." }, CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(CancellationToken);
        (body.GetProperty("status").GetString(), body.GetProperty("finalScore").GetDecimal(), body.GetProperty("review").GetProperty("decision").GetString()).Should().Be(("Graded", 4m, "Overridden"));
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var grade = await context.EssayGrades.AsNoTracking().SingleAsync(x => x.Id == gradeId, CancellationToken);
        (grade.Status, grade.ReviewedScore, grade.AppliedAt.HasValue).Should().Be((EssayGradeStatus.Graded, (decimal?)4m, true));
        var attempt = await context.Attempts.AsNoTracking().SingleAsync(x => x.SessionId == sessionId && x.QuestionId == questionId, CancellationToken);
        (attempt.GradedBy, attempt.Score).Should().Be((AttemptGrader.Teacher, 4m));
        (await context.QuestionMasteries.AsNoTracking().CountAsync(x => x.StudentId == student.Id && x.QuestionId == questionId, CancellationToken)).Should().Be(1);
        (await context.EssayGradeTrainingRecords.AsNoTracking().Where(x => x.EssayGradeId == gradeId).Select(x => x.Trigger).ToListAsync(CancellationToken)).Should().BeEquivalentTo([EssayGradeTrainingTrigger.Completed, EssayGradeTrainingTrigger.TeacherReviewed]);
        (await context.AttemptTrainingRecords.AsNoTracking().SingleAsync(x => x.AttemptId == attempt.Id, CancellationToken)).GradedBy.Should().Be(AttemptGrader.Teacher);
        (await ContentTestData.ReadAuditAsync(factory, "EssayGrade.Review", gradeId, CancellationToken)).Outcome.Should().Be("Success");
    }

    [Fact]
    public async Task Post_Accept_StudentGradeReadsGradedWithReviewNote()
    {
        var subjectId = await ScopeTestData.SeedSubjectAsync(factory, "Physics", CancellationToken);
        var (gradeId, sessionId, questionId, student) = await SeedInReviewEssayAsync(factory, subjectId);
        var (_, client) = await ReviewClientAsync(factory, subjectId);
        using var studentClient = await ScopeTestData.SignedInClientAsync(factory, student, CancellationToken);

        using var review = await client.PostAsJsonAsync(EssayRoute(subjectId, gradeId), new { decision = "Accepted", score = (decimal?)null, comment = (string?)null }, CancellationToken);
        using var response = await studentClient.GetAsync($"/api/sessions/{sessionId}/questions/{questionId}/essay-grade", CancellationToken);

        review.StatusCode.Should().Be(HttpStatusCode.OK);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(CancellationToken);
        (body.GetProperty("status").GetString(), body.GetProperty("score").GetDecimal(), body.GetProperty("review").GetProperty("decision").GetString()).Should().Be(("Graded", 2.5m, "Accepted"));
    }

    [Fact]
    public async Task Post_Twice_Returns409GradeNotInReview()
    {
        var subjectId = await ScopeTestData.SeedSubjectAsync(factory, "Physics", CancellationToken);
        var (gradeId, sessionId, _, _) = await SeedInReviewEssayAsync(factory, subjectId);
        var (_, client) = await ReviewClientAsync(factory, subjectId);
        using var first = await client.PostAsJsonAsync(EssayRoute(subjectId, gradeId), new { decision = "Accepted" }, CancellationToken);

        using var second = await client.PostAsJsonAsync(EssayRoute(subjectId, gradeId), new { decision = "Overridden", score = 3, comment = "Changed my mind." }, CancellationToken);

        first.StatusCode.Should().Be(HttpStatusCode.OK);
        second.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await second.Content.ReadFromJsonAsync<JsonElement>(CancellationToken)).GetProperty("code").GetString().Should().Be("GRADE_NOT_IN_REVIEW");
        using var scope = factory.Services.CreateScope();
        (await scope.ServiceProvider.GetRequiredService<AppDbContext>().Attempts.CountAsync(x => x.SessionId == sessionId, CancellationToken)).Should().Be(1);
    }

    [Fact]
    public async Task Post_OverrideWithoutComment_Returns422CommentRequired()
    {
        var subjectId = await ScopeTestData.SeedSubjectAsync(factory, "Physics", CancellationToken);
        var (gradeId, _, _, _) = await SeedInReviewEssayAsync(factory, subjectId);
        var (_, client) = await ReviewClientAsync(factory, subjectId);

        using var response = await client.PostAsJsonAsync(EssayRoute(subjectId, gradeId), new { decision = "Overridden", score = 3 }, CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await response.Content.ReadAsStringAsync(CancellationToken)).Should().Contain("GRADE_REVIEW_COMMENT_REQUIRED");
    }

    [Fact]
    public async Task Post_ScoreAboveMax_Returns400ScoreOutOfRange()
    {
        var subjectId = await ScopeTestData.SeedSubjectAsync(factory, "Physics", CancellationToken);
        var (gradeId, _, _, _) = await SeedInReviewEssayAsync(factory, subjectId);
        var (_, client) = await ReviewClientAsync(factory, subjectId);

        using var response = await client.PostAsJsonAsync(EssayRoute(subjectId, gradeId), new { decision = "Overridden", score = 6, comment = "Too generous." }, CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadFromJsonAsync<JsonElement>(CancellationToken)).GetProperty("code").GetString().Should().Be("GRADE_REVIEW_SCORE_OUT_OF_RANGE");
        (await ReadStatusAsync(gradeId)).Should().Be(EssayGradeStatus.InReview);
    }

    [Fact]
    public async Task Post_AcceptGradingFailed_Returns400NoAiScore()
    {
        var subjectId = await ScopeTestData.SeedSubjectAsync(factory, "Physics", CancellationToken);
        var (gradeId, _, _, _) = await SeedInReviewEssayAsync(factory, subjectId, gradingFailed: true);
        var (_, client) = await ReviewClientAsync(factory, subjectId);

        using var response = await client.PostAsJsonAsync(EssayRoute(subjectId, gradeId), new { decision = "Accepted" }, CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadFromJsonAsync<JsonElement>(CancellationToken)).GetProperty("code").GetString().Should().Be("GRADE_REVIEW_NO_AI_SCORE");
    }

    [Fact]
    public async Task Post_Admin_Succeeds()
    {
        var subjectId = await ScopeTestData.SeedSubjectAsync(factory, "Physics", CancellationToken);
        var (gradeId, _, _, _) = await SeedInReviewEssayAsync(factory, subjectId);
        var admin = await ScopeTestData.SeedAdminAsync(factory, CancellationToken);
        using var client = await ScopeTestData.SignedInClientAsync(factory, admin, CancellationToken);

        using var response = await client.PostAsJsonAsync(EssayRoute(subjectId, gradeId), new { decision = "Overridden", score = 3.5, comment = "Reviewed by the admin." }, CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadStatusAsync(gradeId)).Should().Be(EssayGradeStatus.Graded);
    }

    [Fact]
    public async Task Post_UnassignedTeacher_Returns403()
    {
        var subjectId = await ScopeTestData.SeedSubjectAsync(factory, "Physics", CancellationToken);
        var otherId = await ScopeTestData.SeedSubjectAsync(factory, "Chemistry", CancellationToken);
        var (gradeId, _, _, _) = await SeedInReviewEssayAsync(factory, subjectId);
        var (_, client) = await ReviewClientAsync(factory, otherId);

        using var response = await client.PostAsJsonAsync(EssayRoute(subjectId, gradeId), new { decision = "Accepted" }, CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await response.Content.ReadFromJsonAsync<JsonElement>(CancellationToken)).GetProperty("code").GetString().Should().Be("SUBJECT_OUT_OF_SCOPE");
        (await ContentTestData.ReadAuditAsync(factory, "EssayGrade.Review", gradeId, CancellationToken)).Outcome.Should().Be("Failure");
    }

    private async Task<EssayGradeStatus> ReadStatusAsync(Guid gradeId)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await context.EssayGrades.AsNoTracking().Where(x => x.Id == gradeId).Select(x => x.Status).SingleAsync(CancellationToken);
    }
}
