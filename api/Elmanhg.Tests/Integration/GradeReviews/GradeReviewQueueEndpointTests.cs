using Elmanhg.Infrastructure.Data.Context;
using Elmanhg.Tests.Integration.Auth;
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

public sealed class GradeReviewQueueEndpointTests(ApiFactory factory)
{
    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task GetSubjects_Teacher_ReturnsAssignedSubjectCounts()
    {
        var subjectId = await ScopeTestData.SeedSubjectAsync(factory, "Physics", CancellationToken);
        var unassignedId = await ScopeTestData.SeedSubjectAsync(factory, "Chemistry", CancellationToken);
        await SeedInReviewEssayAsync(factory, subjectId);
        await SeedInReviewMathStepAsync(factory, subjectId);
        var (_, client) = await ReviewClientAsync(factory, subjectId);

        using var response = await client.GetAsync("/api/grade-reviews/subjects", CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(CancellationToken);
        var subject = body.EnumerateArray().Should().ContainSingle().Subject;
        (subject.GetProperty("subjectId").GetGuid(), subject.GetProperty("essayCount").GetInt32(), subject.GetProperty("mathStepsCount").GetInt32()).Should().Be((subjectId, 1, 1));
        body.EnumerateArray().Select(x => x.GetProperty("subjectId").GetGuid()).Should().NotContain(unassignedId);
    }

    [Fact]
    public async Task GetQueue_Essays_ListsInReviewGradesOldestFirstExcludingTestMode()
    {
        var subjectId = await ScopeTestData.SeedSubjectAsync(factory, "Physics", CancellationToken);
        var (olderId, _, _, _) = await SeedInReviewEssayAsync(factory, subjectId);
        var (newerId, _, _, _) = await SeedInReviewEssayAsync(factory, subjectId, gradingFailed: true);
        var (testModeId, _, _, _) = await SeedInReviewEssayAsync(factory, subjectId, isTestMode: true);
        var (gradedId, _, _, _) = await SeedInReviewEssayAsync(factory, subjectId);
        await AcceptDirectlyAsync(gradedId);
        var (_, client) = await ReviewClientAsync(factory, subjectId);

        using var response = await client.GetAsync(QueueRoute(subjectId, "Essay"), CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(CancellationToken);
        body.GetProperty("items").EnumerateArray().Select(x => x.GetProperty("id").GetGuid()).Should().Equal(olderId, newerId);
        body.GetProperty("totalItems").GetInt64().Should().Be(2);
        body.GetProperty("items").EnumerateArray().Select(x => x.GetProperty("id").GetGuid()).Should().NotContain([testModeId, gradedId]);
    }

    [Fact]
    public async Task GetQueue_MathSteps_ListsUncheckedGrade()
    {
        var subjectId = await ScopeTestData.SeedSubjectAsync(factory, "Physics", CancellationToken);
        var (gradeId, _, _, _) = await SeedInReviewMathStepAsync(factory, subjectId);
        var (_, client) = await ReviewClientAsync(factory, subjectId);

        using var response = await client.GetAsync(QueueRoute(subjectId, "MathSteps"), CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var item = (await response.Content.ReadFromJsonAsync<JsonElement>(CancellationToken)).GetProperty("items").EnumerateArray().Should().ContainSingle().Subject;
        (item.GetProperty("id").GetGuid(), item.GetProperty("reviewReason").GetString(), item.GetProperty("aiScore").ValueKind).Should().Be((gradeId, "FinalAnswerUnchecked", JsonValueKind.Null));
    }

    [Fact]
    public async Task GetQueue_UnassignedTeacher_Returns403SubjectOutOfScope()
    {
        var subjectId = await ScopeTestData.SeedSubjectAsync(factory, "Physics", CancellationToken);
        var otherId = await ScopeTestData.SeedSubjectAsync(factory, "Chemistry", CancellationToken);
        var (_, client) = await ReviewClientAsync(factory, otherId);

        using var response = await client.GetAsync(QueueRoute(subjectId, "Essay"), CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await response.Content.ReadFromJsonAsync<JsonElement>(CancellationToken)).GetProperty("code").GetString().Should().Be("SUBJECT_OUT_OF_SCOPE");
    }

    [Fact]
    public async Task GetQueue_Student_Returns403()
    {
        var subjectId = await ScopeTestData.SeedSubjectAsync(factory, "Physics", CancellationToken);
        var student = await ScopeTestData.SeedStudentAsync(factory, CancellationToken);
        using var client = await ScopeTestData.SignedInClientAsync(factory, student, CancellationToken);

        using var response = await client.GetAsync(QueueRoute(subjectId, "Essay"), CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetQueue_Anonymous_Returns401()
    {
        using var anonymous = AuthTestClient.Create(factory);

        using var response = await anonymous.GetAsync(QueueRoute(Guid.NewGuid(), "Essay"), CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetEssayReview_GradeFromOtherSubjectInRoute_Returns404()
    {
        var subjectId = await ScopeTestData.SeedSubjectAsync(factory, "Physics", CancellationToken);
        var otherId = await ScopeTestData.SeedSubjectAsync(factory, "Chemistry", CancellationToken);
        var (gradeId, _, _, _) = await SeedInReviewEssayAsync(factory, subjectId);
        var (teacher, client) = await ReviewClientAsync(factory, subjectId);
        await ScopeTestData.AssignAsync(factory, teacher.Id, otherId, CancellationToken);

        using var response = await client.GetAsync(EssayRoute(otherId, gradeId), CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await response.Content.ReadFromJsonAsync<JsonElement>(CancellationToken)).GetProperty("code").GetString().Should().Be("GRADE_REVIEW_NOT_FOUND");
    }

    private async Task AcceptDirectlyAsync(Guid gradeId)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var grade = await context.EssayGrades.SingleAsync(x => x.Id == gradeId, CancellationToken);
        grade.Accept(Guid.NewGuid(), null, DateTimeOffset.UtcNow);
        await context.SaveChangesAsync(CancellationToken);
    }
}
