using Elmanhg.Application.Exams.AutoSubmitExam;
using Elmanhg.Application.Exams.GetExpiredExamSessionIds;
using Elmanhg.Tests.Integration.Infrastructure;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using static Elmanhg.Tests.Integration.Exams.ExamTestData;
using static Elmanhg.Tests.Integration.Sessions.SessionTestData;

namespace Elmanhg.Tests.Integration.Exams;

public sealed class ExpiredExamSubmissionTests(ApiFactory factory)
{
    [Fact]
    public async Task AutoSubmit_ExpiredExam_SubmitsAndGrades()
    {
        var sessionId = await StartWithFirstAnsweredAsync();
        await ExpireAsync(factory, sessionId, TimeSpan.FromHours(1));

        await SendAsync(new AutoSubmitExamCommand(sessionId));

        var session = await ReadSessionAsync(factory, sessionId);
        session.SubmittedAt.Should().NotBeNull();
        session.ScorePercent.Should().Be(50m);
        (await ReadAttemptsAsync(factory, sessionId)).Should().HaveCount(1);
    }

    [Fact]
    public async Task AutoSubmit_WithinGrace_LeavesOpen()
    {
        var sessionId = await StartWithFirstAnsweredAsync();
        await ExpireAsync(factory, sessionId, TimeSpan.FromSeconds(10));

        await SendAsync(new AutoSubmitExamCommand(sessionId));

        (await ReadSessionAsync(factory, sessionId)).SubmittedAt.Should().BeNull();
    }

    [Fact]
    public async Task GetExpiredExamSessionIds_ReturnsExpiredOpenExam()
    {
        var expiredId = await StartWithFirstAnsweredAsync();
        var freshId = await StartWithFirstAnsweredAsync();
        await ExpireAsync(factory, expiredId, TimeSpan.FromDays(3650));

        using var scope = factory.Services.CreateScope();
        var result = await scope.ServiceProvider.GetRequiredService<ISender>().Send(new GetExpiredExamSessionIdsQuery([]), TestContext.Current.CancellationToken);

        result.Should().Contain(expiredId).And.NotContain(freshId);
    }

    [Fact]
    public async Task GetExpiredExamSessionIds_ExcludedId_IsSkipped()
    {
        var excludedId = await StartWithFirstAnsweredAsync();
        var expiredId = await StartWithFirstAnsweredAsync();
        await ExpireAsync(factory, excludedId, TimeSpan.FromDays(3651));
        await ExpireAsync(factory, expiredId, TimeSpan.FromDays(3651));

        using var scope = factory.Services.CreateScope();
        var result = await scope.ServiceProvider.GetRequiredService<ISender>().Send(new GetExpiredExamSessionIdsQuery([excludedId]), TestContext.Current.CancellationToken);

        result.Should().Contain(expiredId).And.NotContain(excludedId);
    }

    private async Task SendAsync(AutoSubmitExamCommand command)
    {
        using var scope = factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<ISender>().Send(command, TestContext.Current.CancellationToken).ConfigureAwait(false);
    }

    private async Task<Guid> StartWithFirstAnsweredAsync()
    {
        var (_, unitId, _, _) = await SeedExamUnitAsync(factory, 2).ConfigureAwait(false);
        var (_, client) = await SignedInStudentAsync(factory).ConfigureAwait(false);
        var body = await StartAsync(client, unitId).ConfigureAwait(false);
        var sessionId = body.GetProperty("id").GetGuid();
        using var save = await SaveAsync(client, sessionId, ItemQuestionIds(body)[0], "b").ConfigureAwait(false);
        return sessionId;
    }
}
