using Elmanhg.Tests.Integration.Infrastructure;
using FluentAssertions;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using static Elmanhg.Tests.Integration.Configuration.ConfigurationTestData;

namespace Elmanhg.Tests.Integration.Configuration;

[Collection(RuntimeSettingsCollection.Name)]
public sealed class ConfigurationEndpointTests(ApiFactory factory) : IAsyncLifetime
{
    private const string QuizKey = "plans.freeDailyQuizQuestions";

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync() => await ClearOverridesAsync(factory);

    public async ValueTask DisposeAsync() => await ClearOverridesAsync(factory);

    [Fact]
    public async Task Get_Settings_AsAdmin_ReturnsGroupsWithDefaults()
    {
        using var admin = await AdminClientAsync(factory);

        using var response = await admin.GetAsync(SettingsRoute, CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var raw = await response.Content.ReadAsStringAsync(CancellationToken);
        var groups = JsonDocument.Parse(raw).RootElement;
        groups[0].GetProperty("group").GetString().Should().Be("Features");
        var sla = groups.EnumerateArray().SelectMany(x => x.GetProperty("settings").EnumerateArray()).Single(x => x.GetProperty("key").GetString() == "askTeacher.replySlaHours");
        (sla.GetProperty("value").GetInt32(), sla.GetProperty("defaultValue").GetInt32(), sla.GetProperty("isOverridden").GetBoolean()).Should().Be((24, 24, false));
        sla.GetProperty("labelArabic").GetString().Should().NotBeNullOrWhiteSpace();
        sla.GetProperty("labelEnglish").GetString().Should().NotBeNullOrWhiteSpace();
        raw.Should().NotContain("overrideId");
    }

    [Fact]
    public async Task Get_Settings_AsTeacher_Returns403()
    {
        using var teacher = await TeacherClientAsync(factory);

        using var response = await teacher.GetAsync(SettingsRoute, CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Get_Settings_Anonymous_Returns401()
    {
        using var anonymous = factory.CreateClient();

        using var response = await anonymous.GetAsync(SettingsRoute, CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Put_ValidValue_OverridesAndTakesEffectWithoutRestart()
    {
        using var admin = await AdminClientAsync(factory);
        await ReadFreeQuizLimitAsync();

        using var response = await admin.PutAsJsonAsync(SettingRoute(QuizKey), new { value = 12 }, CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(CancellationToken);
        (body.GetProperty("value").GetInt32(), body.GetProperty("isOverridden").GetBoolean()).Should().Be((12, true));
        (await ReadOverrideAsync(factory, QuizKey))!.Value.Should().Be("12");
        (await ReadFreeQuizLimitAsync()).Should().Be(12);
    }

    [Fact]
    public async Task Put_ValidValue_WritesAuditRowWithOldAndNewValue()
    {
        using var admin = await AdminClientAsync(factory);

        (await admin.PutAsJsonAsync(SettingRoute(QuizKey), new { value = 12 }, CancellationToken)).Dispose();
        (await admin.PutAsJsonAsync(SettingRoute(QuizKey), new { value = 13 }, CancellationToken)).Dispose();

        var row = await ReadOverrideAsync(factory, QuizKey);
        var audits = await ReadAuditsAsync(factory, "RuntimeSetting.Update", row!.Id);
        audits.Should().HaveCount(2).And.OnlyContain(x => x.ResourceType == "RuntimeSetting" && x.Outcome == "Success");
        audits[0].Diff.Should().Contain("\"value\"").And.Contain("12");
        var value = JsonNode.Parse(audits[1].Diff!)![0]!["properties"]!["value"]!;
        (value["before"]!.ToString(), value["after"]!.ToString()).Should().Be(("12", "13"));
    }

    [Fact]
    public async Task Put_OutOfRange_Returns422ValueInvalid()
    {
        using var admin = await AdminClientAsync(factory);

        using var response = await admin.PutAsJsonAsync(SettingRoute(QuizKey), new { value = 5000 }, CancellationToken);

        await ExpectProblemAsync(response, HttpStatusCode.UnprocessableEntity, "RUNTIME_SETTING_VALUE_INVALID");
        (await ReadOverrideAsync(factory, QuizKey)).Should().BeNull();
    }

    [Fact]
    public async Task Put_UnknownKey_Returns404()
    {
        using var admin = await AdminClientAsync(factory);

        using var response = await admin.PutAsJsonAsync(SettingRoute("nope.key"), new { value = 1 }, CancellationToken);

        await ExpectProblemAsync(response, HttpStatusCode.NotFound, "RUNTIME_SETTING_NOT_FOUND");
    }

    [Fact]
    public async Task Put_SecondReminderAfterSla_Returns400ReminderOrderInvalid()
    {
        using var admin = await AdminClientAsync(factory);

        using var response = await admin.PutAsJsonAsync(SettingRoute("askTeacher.secondReminderAfterHours"), new { value = 30 }, CancellationToken);

        await ExpectProblemAsync(response, HttpStatusCode.BadRequest, "ASK_TEACHER_REMINDER_ORDER_INVALID");
    }

    [Fact]
    public async Task Get_Settings_AsAdmin_ListsOutOfAppReminderSettings()
    {
        using var admin = await AdminClientAsync(factory);

        var groups = await admin.GetFromJsonAsync<JsonElement>(SettingsRoute, CancellationToken);

        var askTeacher = groups.EnumerateArray().Single(x => x.GetProperty("group").GetString() == "AskTeacher").GetProperty("settings").EnumerateArray().ToDictionary(x => x.GetProperty("key").GetString()!);
        askTeacher["askTeacher.outOfAppReminderEnabled"].GetProperty("value").GetBoolean().Should().BeTrue();
        askTeacher["askTeacher.outOfAppReminderChannels"].GetProperty("value").GetString().Should().Be("Both");
        askTeacher["askTeacher.outOfAppReminderStage"].GetProperty("value").GetString().Should().Be("SecondReminder");
        askTeacher["askTeacher.outOfAppReminderChannels"].GetProperty("allowedValues").EnumerateArray().Select(x => x.GetString()).Should().Equal("WhatsApp", "Email", "Both");
        askTeacher["askTeacher.outOfAppReminderStage"].GetProperty("allowedValues").EnumerateArray().Select(x => x.GetString()).Should().Equal("FirstReminder", "SecondReminder");
    }

    [Fact]
    public async Task Put_OutOfAppReminderStageBreach_Returns422ValueInvalid()
    {
        using var admin = await AdminClientAsync(factory);

        using var response = await admin.PutAsJsonAsync(SettingRoute("askTeacher.outOfAppReminderStage"), new { value = "Breach" }, CancellationToken);

        await ExpectProblemAsync(response, HttpStatusCode.UnprocessableEntity, "RUNTIME_SETTING_VALUE_INVALID");
        (await ReadOverrideAsync(factory, "askTeacher.outOfAppReminderStage")).Should().BeNull();
    }

    [Fact]
    public async Task Put_AsTeacher_Returns403()
    {
        using var teacher = await TeacherClientAsync(factory);

        using var response = await teacher.PutAsJsonAsync(SettingRoute(QuizKey), new { value = 12 }, CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task PostReset_Overridden_RestoresDefaultAndAudits()
    {
        using var admin = await AdminClientAsync(factory);
        (await admin.PutAsJsonAsync(SettingRoute(QuizKey), new { value = 12 }, CancellationToken)).Dispose();

        using var response = await admin.PostAsync($"{SettingRoute(QuizKey)}/reset", null, CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(CancellationToken);
        (body.GetProperty("value").GetInt32(), body.GetProperty("isOverridden").GetBoolean()).Should().Be((10, false));
        var row = await ReadOverrideAsync(factory, QuizKey);
        row!.Value.Should().BeNull();
        (await ReadFreeQuizLimitAsync()).Should().Be(10);
        (await ReadAuditsAsync(factory, "RuntimeSetting.Reset", row.Id)).Should().ContainSingle().Which.Outcome.Should().Be("Success");
    }

    [Fact]
    public async Task PostReset_NotOverridden_Returns200Unchanged()
    {
        using var admin = await AdminClientAsync(factory);

        using var response = await admin.PostAsync($"{SettingRoute(QuizKey)}/reset", null, CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadFromJsonAsync<JsonElement>(CancellationToken)).GetProperty("isOverridden").GetBoolean().Should().BeFalse();
        (await ReadOverrideAsync(factory, QuizKey)).Should().BeNull();
    }

    [Fact]
    public async Task Get_Infrastructure_AsAdmin_ReturnsStatusWithoutSecretValues()
    {
        using var admin = await AdminClientAsync(factory);

        using var response = await admin.GetAsync(InfrastructureRoute, CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var raw = await response.Content.ReadAsStringAsync(CancellationToken);
        var body = JsonDocument.Parse(raw).RootElement;
        body.GetProperty("environment").GetString().Should().Be("Testing");
        body.GetProperty("integrations").EnumerateArray().Single(x => x.GetProperty("integration").GetString() == "payments").GetProperty("mode").GetString().Should().Be("Fake");
        body.GetProperty("aiServiceStatus").GetString().Should().Be("NotUsed");
        var secrets = body.GetProperty("secrets").EnumerateArray().ToList();
        secrets.Should().OnlyContain(x => x.EnumerateObject().Select(property => property.Name).SequenceEqual(new[] { "key", "isSet" }));
        var isSet = secrets.ToDictionary(x => x.GetProperty("key").GetString()!, x => x.GetProperty("isSet").GetBoolean());
        (isSet["Payments:Paymob:HmacSecret"], isSet["CoreJwt:Key"]).Should().Be((true, true));
        raw.Should().NotContain(ApiFactory.TestPaymobHmacSecret).And.NotContain(ApiFactory.TestJwtKey);
    }

    [Fact]
    public async Task Get_Infrastructure_AsTeacher_Returns403()
    {
        using var teacher = await TeacherClientAsync(factory);

        using var response = await teacher.GetAsync(InfrastructureRoute, CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private static async Task ExpectProblemAsync(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        response.StatusCode.Should().Be(status);
        (await response.Content.ReadFromJsonAsync<JsonElement>(CancellationToken)).GetProperty("code").GetString().Should().Be(code);
    }

    private async Task<int> ReadFreeQuizLimitAsync()
    {
        using var anonymous = factory.CreateClient();
        var plans = await anonymous.GetFromJsonAsync<JsonElement>("/api/plans", CancellationToken);
        return plans.GetProperty("free").GetProperty("dailyQuizQuestions").GetInt32();
    }
}
