using Core.Errors;
using Elmanhg.Api.Hosting;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.LoadTesting.SeedLoadTestData;
using Elmanhg.Domain.Identity;
using Elmanhg.Domain.Lessons;
using Elmanhg.Domain.Questions;
using Elmanhg.Domain.Subscriptions;
using Elmanhg.Infrastructure.Data.Context;
using Elmanhg.Tests.Integration.Auth;
using Elmanhg.Tests.Integration.Content;
using Elmanhg.Tests.Integration.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NSubstitute;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace Elmanhg.Tests.Integration.Hosting;

[Collection(ServableCountCollection.Name)]
public sealed class LoadTestSeedCommandTests(ApiFactory factory)
{
    private const string Password = "loadtest-password-1";
    private const string Staging = "Staging";

    [Fact]
    public void IsRequested_CommandLineFlagTrue_ReturnsTrue()
    {
        var configuration = new ConfigurationBuilder().AddCommandLine(["--SeedLoadTestAndExit=true"]).Build();

        var requested = LoadTestSeedCommand.IsRequested(configuration);

        requested.Should().BeTrue();
    }

    [Fact]
    public void IsRequested_FlagMissing_ReturnsFalse()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection([]).Build();

        var requested = LoadTestSeedCommand.IsRequested(configuration);

        requested.Should().BeFalse();
    }

    [Fact]
    public async Task RunAsync_ProductionEnvironment_Returns1AndSeedsNothing()
    {
        var key = NewKey();

        var exitCode = await RunAsync(key, "2", Password, "Production");

        exitCode.Should().Be(1);
        (await CountSubjectsAsync(key)).Should().Be(0);
    }

    [Fact]
    public async Task RunAsync_PasswordMissing_Returns1AndSeedsNothing()
    {
        var key = NewKey();

        var exitCode = await RunAsync(key, "2", null, Staging);

        exitCode.Should().Be(1);
        (await CountSubjectsAsync(key)).Should().Be(0);
    }

    [Theory]
    [InlineData(null, "0")]
    [InlineData(null, "501")]
    [InlineData("Bad Key!", "2")]
    public async Task RunAsync_InvalidSettings_Returns1(string? badKey, string count)
    {
        var key = badKey ?? NewKey();

        var exitCode = await RunAsync(key, count, Password, Staging);

        exitCode.Should().Be(1);
        (await CountSubjectsAsync(key)).Should().Be(0);
    }

    [Fact]
    public async Task RunAsync_PasswordRejectedByIdentity_ThrowsLoadTestSeedFailed()
    {
        var key = NewKey();

        var act = () => RunAsync(key, "1", "short", Staging);

        (await act.Should().ThrowAsync<InternalServerErrorCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.LoadTestSeedFailed);
    }

    [Fact]
    public async Task RunAsync_Staging_SeedsPublishedCurriculumWithServableQuestions()
    {
        var key = NewKey();

        var exitCode = await RunAsync(key, "2", Password, Staging);

        exitCode.Should().Be(0);
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var cancellationToken = TestContext.Current.CancellationToken;
        var subject = await context.Subjects.SingleAsync(x => x.Name == LoadTestData.SubjectName(key), cancellationToken);
        var unitIds = await context.Units.Where(x => x.SubjectId == subject.Id).Select(x => x.Id).ToListAsync(cancellationToken);
        var lessons = await context.Lessons.Where(x => unitIds.Contains(x.UnitId)).ToListAsync(cancellationToken);
        var questions = await context.Questions.Where(x => x.SubjectId == subject.Id).ToListAsync(cancellationToken);
        var blueprints = await context.ExamBlueprints.Where(x => x.SubjectId == subject.Id).ToListAsync(cancellationToken);
        var teacher = await context.Users.SingleAsync(x => x.Email == LoadTestData.TeacherEmail(key), cancellationToken);
        unitIds.Should().HaveCount(3);
        lessons.Should().HaveCount(15).And.OnlyContain(x => x.State == LessonState.Published);
        questions.Should().HaveCount(450).And.OnlyContain(x => x.ValidationStatus == QuestionValidationStatus.Approved && x.RetiredAt == null);
        blueprints.Should().HaveCount(3).And.OnlyContain(x => x.UnitId != null && x.QuestionCount == 20);
        (await context.TeacherSubjects.CountAsync(x => x.TeacherId == teacher.Id && x.SubjectId == subject.Id, cancellationToken)).Should().Be(1);
    }

    [Fact]
    public async Task RunAsync_Staging_SeedsOnboardedStudentsWithActiveBaseSubscription()
    {
        var key = NewKey();

        var exitCode = await RunAsync(key, "2", Password, Staging);

        exitCode.Should().Be(0);
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var cancellationToken = TestContext.Current.CancellationToken;
        var subjectId = await context.Subjects.Where(x => x.Name == LoadTestData.SubjectName(key)).Select(x => x.Id).SingleAsync(cancellationToken);
        var emails = new[] { LoadTestData.StudentEmail(key, 1), LoadTestData.StudentEmail(key, 2) };
        var students = await context.Users.Where(x => emails.Contains(x.Email)).ToListAsync(cancellationToken);
        var studentIds = students.Select(x => x.Id).ToList();
        var subscriptions = await context.Subscriptions.Where(x => studentIds.Contains(x.StudentId)).ToListAsync(cancellationToken);
        students.Should().HaveCount(2).And.OnlyContain(x => x.Role == UserRole.Student && x.OnboardedAt != null);
        students.Should().AllSatisfy(x => x.SubjectInterestIds.Should().Equal(subjectId));
        subscriptions.Should().HaveCount(2).And.OnlyContain(x => x.Plan == SubscriptionPlan.Base && x.Status == SubscriptionStatus.Active);
        subscriptions.Select(x => x.StudentId).Should().BeEquivalentTo(studentIds);
    }

    [Fact]
    public async Task RunAsync_RunTwice_AddsNothingTheSecondTime()
    {
        var key = NewKey();
        (await RunAsync(key, "2", Password, Staging)).Should().Be(0);

        var exitCode = await RunAsync(key, "2", Password, Staging);

        exitCode.Should().Be(0);
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var cancellationToken = TestContext.Current.CancellationToken;
        var subjectIds = await context.Subjects.Where(x => x.Name == LoadTestData.SubjectName(key)).Select(x => x.Id).ToListAsync(cancellationToken);
        var studentIds = await context.Users.Where(x => x.Email!.StartsWith($"{key}-student-")).Select(x => x.Id).ToListAsync(cancellationToken);
        subjectIds.Should().ContainSingle();
        (await context.Questions.CountAsync(x => x.SubjectId == subjectIds[0], cancellationToken)).Should().Be(450);
        studentIds.Should().HaveCount(2);
        (await context.Subscriptions.CountAsync(x => studentIds.Contains(x.StudentId), cancellationToken)).Should().Be(2);
    }

    [Fact]
    public async Task SeededStudent_LogsInAndStartsQuiz_Returns10Items()
    {
        var key = NewKey();
        var cancellationToken = TestContext.Current.CancellationToken;
        (await RunAsync(key, "1", Password, Staging)).Should().Be(0);
        var lessonId = await FirstLessonIdAsync(key);
        using var client = AuthTestClient.Create(factory);
        using var login = await client.PostAsJsonAsync("/api/auth/login/email", new { email = LoadTestData.StudentEmail(key, 1), password = Password }, cancellationToken);
        login.StatusCode.Should().Be(HttpStatusCode.OK);
        var accessToken = (await login.Content.ReadFromJsonAsync<JsonElement>(cancellationToken)).GetProperty("accessToken").GetString();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        using var response = await client.PostAsJsonAsync("/api/sessions/quiz", new { lessonId, questionCount = 10 }, cancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken)).GetProperty("items").GetArrayLength().Should().Be(10);
    }

    private static string NewKey() => $"t{Guid.NewGuid():N}"[..13];

    private async Task<int> RunAsync(string key, string count, string? password, string environmentName)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["LoadTestSeed:Key"] = key,
            ["LoadTestSeed:StudentCount"] = count,
            ["LoadTestSeed:StudentPassword"] = password,
        }).Build();
        var environment = Substitute.For<IHostEnvironment>();
        environment.EnvironmentName.Returns(environmentName);
        return await LoadTestSeedCommand.RunAsync(factory.Services, configuration, environment, TestContext.Current.CancellationToken).ConfigureAwait(false);
    }

    private async Task<int> CountSubjectsAsync(string key)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await context.Subjects.CountAsync(x => x.Name == LoadTestData.SubjectName(key), TestContext.Current.CancellationToken).ConfigureAwait(false);
    }

    private async Task<Guid> FirstLessonIdAsync(string key)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var subjectId = await context.Subjects.Where(x => x.Name == LoadTestData.SubjectName(key)).Select(x => x.Id).SingleAsync(TestContext.Current.CancellationToken).ConfigureAwait(false);
        var unitId = await context.Units.Where(x => x.SubjectId == subjectId && x.Order == 1).Select(x => x.Id).SingleAsync(TestContext.Current.CancellationToken).ConfigureAwait(false);
        return await context.Lessons.Where(x => x.UnitId == unitId && x.Order == 1).Select(x => x.Id).SingleAsync(TestContext.Current.CancellationToken).ConfigureAwait(false);
    }
}
