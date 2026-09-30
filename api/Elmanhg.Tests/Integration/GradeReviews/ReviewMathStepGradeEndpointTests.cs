using Elmanhg.Domain.Sessions;
using Elmanhg.Infrastructure.Data.Context;
using Elmanhg.Tests.Integration.Authorization;
using Elmanhg.Tests.Integration.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using static Elmanhg.Tests.Integration.GradeReviews.GradeReviewTestData;

namespace Elmanhg.Tests.Integration.GradeReviews;

public sealed class ReviewMathStepGradeEndpointTests(ApiFactory factory)
{
    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Post_OverrideUnchecked_WritesTeacherAttemptAndStudentSeesGraded()
    {
        var subjectId = await ScopeTestData.SeedSubjectAsync(factory, "Physics", CancellationToken);
        var (gradeId, sessionId, questionId, student) = await SeedInReviewMathStepAsync(factory, subjectId);
        var (_, client) = await ReviewClientAsync(factory, subjectId);
        using var studentClient = await ScopeTestData.SignedInClientAsync(factory, student, CancellationToken);

        using var review = await client.PostAsJsonAsync(MathStepsRoute(subjectId, gradeId), new { decision = "Overridden", score = 2, comment = "Correct method." }, CancellationToken);
        using var response = await studentClient.GetAsync($"/api/sessions/{sessionId}/questions/{questionId}/math-step-grade", CancellationToken);

        review.StatusCode.Should().Be(HttpStatusCode.OK);
        using (var scope = factory.Services.CreateScope())
        {
            var attempt = await scope.ServiceProvider.GetRequiredService<AppDbContext>().Attempts.AsNoTracking().SingleAsync(x => x.SessionId == sessionId && x.QuestionId == questionId, CancellationToken);
            (attempt.GradedBy, attempt.Score).Should().Be((AttemptGrader.Teacher, 2m));
        }

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(CancellationToken);
        (body.GetProperty("status").GetString(), body.GetProperty("finalAnswerVerdict").ValueKind, body.GetProperty("review").GetProperty("comment").GetString()).Should().Be(("Graded", JsonValueKind.Null, "Correct method."));
    }

    [Fact]
    public async Task Post_GradeFromOtherSubject_Returns404()
    {
        var subjectId = await ScopeTestData.SeedSubjectAsync(factory, "Physics", CancellationToken);
        var otherId = await ScopeTestData.SeedSubjectAsync(factory, "Chemistry", CancellationToken);
        var (gradeId, _, _, _) = await SeedInReviewMathStepAsync(factory, subjectId);
        var (_, client) = await ReviewClientAsync(factory, otherId);

        using var response = await client.PostAsJsonAsync(MathStepsRoute(otherId, gradeId), new { decision = "Overridden", score = 2, comment = "Correct method." }, CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await response.Content.ReadFromJsonAsync<JsonElement>(CancellationToken)).GetProperty("code").GetString().Should().Be("GRADE_REVIEW_NOT_FOUND");
    }

    [Fact]
    public async Task GetAndPost_TestModeGrades_Return404()
    {
        var subjectId = await ScopeTestData.SeedSubjectAsync(factory, "Physics", CancellationToken);
        var (essayId, _, _, _) = await SeedInReviewEssayAsync(factory, subjectId, isTestMode: true);
        var (mathId, mathSessionId, _, _) = await SeedInReviewMathStepAsync(factory, subjectId, isTestMode: true);
        var (_, client) = await ReviewClientAsync(factory, subjectId);

        using var essayDetail = await client.GetAsync(EssayRoute(subjectId, essayId), CancellationToken);
        using var mathDetail = await client.GetAsync(MathStepsRoute(subjectId, mathId), CancellationToken);
        using var essayReview = await client.PostAsJsonAsync(EssayRoute(subjectId, essayId), new { decision = "Accepted" }, CancellationToken);
        using var mathReview = await client.PostAsJsonAsync(MathStepsRoute(subjectId, mathId), new { decision = "Overridden", score = 2, comment = "Correct method." }, CancellationToken);

        new[] { essayDetail.StatusCode, mathDetail.StatusCode, essayReview.StatusCode, mathReview.StatusCode }.Should().AllBeEquivalentTo(HttpStatusCode.NotFound);
        (await mathReview.Content.ReadFromJsonAsync<JsonElement>(CancellationToken)).GetProperty("code").GetString().Should().Be("GRADE_REVIEW_NOT_FOUND");
        using var scope = factory.Services.CreateScope();
        (await scope.ServiceProvider.GetRequiredService<AppDbContext>().Attempts.CountAsync(x => x.SessionId == mathSessionId, CancellationToken)).Should().Be(0);
    }
}
