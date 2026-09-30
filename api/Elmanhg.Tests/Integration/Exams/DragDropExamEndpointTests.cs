using Elmanhg.Domain.ExamBlueprints;
using Elmanhg.Domain.Lessons;
using Elmanhg.Domain.Questions;
using Elmanhg.Domain.Questions.Grading;
using Elmanhg.Infrastructure.Data.Context;
using Elmanhg.Tests.Builders;
using Elmanhg.Tests.Integration.Content;
using Elmanhg.Tests.Integration.Infrastructure;
using Elmanhg.Tests.Integration.Sessions;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using static Elmanhg.Tests.Integration.Exams.ExamTestData;

namespace Elmanhg.Tests.Integration.Exams;

public sealed class DragDropExamEndpointTests(ApiFactory factory)
{
    [Fact]
    public async Task Start_UnitExamWithDragDrop_ServesResolvedImageUrl()
    {
        var (unitId, lessonId, dragDropId) = await SeedDragDropUnitAsync();
        var (_, client) = await SessionTestData.SignedInStudentAsync(factory);

        var session = await StartAsync(client, unitId);

        var item = session.GetProperty("items").EnumerateArray().Single(x => x.GetProperty("questionId").GetGuid() == dragDropId);
        item.GetProperty("type").GetString().Should().Be("DragDrop");
        item.GetProperty("body").GetProperty("image").GetProperty("url").GetString().Should().Be($"/api/media/{QuestionBuilder.ForLesson(QuestionBuilder.DragDropImageKey, lessonId)}");
    }

    [Fact]
    public async Task Put_DragDropAnswer_StoresCanonicalAnswer()
    {
        var (client, sessionId, dragDropId) = await StartDragDropExamAsync();

        using var response = await SaveDragDropAsync(client, sessionId, dragDropId, new { placements = new[] { new { zoneId = "z1", itemIds = Array.Empty<string>() }, new { zoneId = "z2", itemIds = new[] { "i4", "i3" } } } });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var session = await SessionTestData.ReadSessionAsync(factory, sessionId);
        var saved = session.Items.Single(x => x.QuestionId == dragDropId).SavedAnswer;
        JsonNode.DeepEquals(JsonNode.Parse(saved!), JsonNode.Parse("""{"placements":[{"zoneId":"z2","itemIds":["i4","i3"]}]}""")).Should().BeTrue(saved);
    }

    [Fact]
    public async Task Submit_SavedDragDropAnswer_GradesPerItem()
    {
        var (client, sessionId, dragDropId) = await StartDragDropExamAsync();
        using var saved = await SaveDragDropAsync(client, sessionId, dragDropId, new { placements = new[] { new { zoneId = "z1", itemIds = new[] { "i1", "i2" } }, new { zoneId = "z2", itemIds = new[] { "i4" } } } });

        using var response = await SubmitAsync(client, sessionId);

        (saved.StatusCode, response.StatusCode).Should().Be((HttpStatusCode.OK, HttpStatusCode.OK));
        var attempt = (await SessionTestData.ReadAttemptsAsync(factory, sessionId)).Should().ContainSingle(x => x.QuestionId == dragDropId).Which;
        (attempt.Outcome, attempt.Score).Should().Be((GradeOutcome.Partial, 3m));
    }

    private async Task<(HttpClient Client, Guid SessionId, Guid DragDropId)> StartDragDropExamAsync()
    {
        var (unitId, _, dragDropId) = await SeedDragDropUnitAsync();
        var (_, client) = await SessionTestData.SignedInStudentAsync(factory);
        var session = await StartAsync(client, unitId);
        ItemQuestionIds(session).Should().Contain(dragDropId);
        return (client, session.GetProperty("id").GetGuid(), dragDropId);
    }

    private async Task<(Guid UnitId, Guid LessonId, Guid DragDropId)> SeedDragDropUnitAsync()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var subjectId = await ContentTestData.SeedSubjectAsync(factory, "Biology", 1, cancellationToken);
        var unitId = await ContentTestData.SeedUnitAsync(factory, subjectId, "Cells", 1, cancellationToken);
        var lessonId = await ContentTestData.SeedLessonInStateAsync(factory, unitId, "Plant cells", 1, LessonState.Published, cancellationToken);
        await QuestionTestData.SeedQuestionAsync(factory, lessonId, approved: true, cancellationToken);
        var dragDropId = await QuestionTestData.SeedDragDropQuestionAsync(factory, lessonId, cancellationToken);
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var unit = await context.Units.SingleAsync(x => x.Id == unitId, cancellationToken);
        var shape = new ExamBlueprintShape([new ExamTypeCount(QuestionType.Mcq, 1), new ExamTypeCount(QuestionType.DragDrop, 1)], null, 30, 50);
        context.ExamBlueprints.Add(ExamBlueprint.CreateForUnit(unit, shape, ExamBlueprintBuilder.Plenty(), Guid.NewGuid()));
        await context.SaveChangesAsync(cancellationToken);
        return (unitId, lessonId, dragDropId);
    }

    private static Task<HttpResponseMessage> SaveDragDropAsync(HttpClient client, Guid sessionId, Guid questionId, object answer)
    {
        return client.PutAsJsonAsync($"{Route}/{sessionId}/answers/{questionId}", new { answer }, TestContext.Current.CancellationToken);
    }
}
