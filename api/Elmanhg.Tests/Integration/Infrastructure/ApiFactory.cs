using Core.Localization;
using Core.OTP.Delivery;
using Elmanhg.Infrastructure.Data.Context;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Localization;
using Testcontainers.PostgreSql;

[assembly: AssemblyFixture(typeof(Elmanhg.Tests.Integration.Infrastructure.ApiFactory))]

namespace Elmanhg.Tests.Integration.Infrastructure;

public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private const string PostgresImage = "pgvector/pgvector:pg17";
    private const string TestingEnvironment = "Testing";
    // Signs nothing outside this in-memory host; the JwtBearer options delegate only requires it to be non-empty.
    private const string TestJwtKey = "elmanhg-tests-signing-key-not-a-secret-0123456789";
    // Keys only the HMAC of OTP codes inside this in-memory host; codes are read back from OtpOutbox.
    private const string TestOtpSecret = "elmanhg-tests-otp-secret";

    // Keys only the HMAC of Paymob notifications posted to this in-memory host.
    public const string TestPaymobHmacSecret = "elmanhg-tests-paymob-hmac";

    private readonly PostgreSqlContainer _database = new PostgreSqlBuilder(PostgresImage).Build();

    public OtpOutbox Otp { get; } = new();

    public LessonEventLog LessonEvents { get; } = new();

    public string MediaRoot { get; } = Path.Combine(Path.GetTempPath(), "elmanhg-tests-media", Guid.NewGuid().ToString("N"));

    public async ValueTask InitializeAsync()
    {
        await _database.StartAsync();
        using var scope = Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(TestingEnvironment);
        // AddCoreAuditing reads this flag while Program registers services, before ConfigureAppConfiguration sources are applied.
        builder.UseSetting("CoreAuditing:Enabled", "true");
        // The sweep would race tests that expire sessions on purpose; AutoSubmitExam is exercised directly through the mediator.
        builder.UseSetting("Exams:AutoSubmitEnabled", "false");
        builder.UseSetting("Exams:RequireAllLessonsOpened", "false");
        // The lapse sweep would race tests that seed ended subscriptions; LapseSubscription is exercised directly through the mediator.
        builder.UseSetting("Subscriptions:LapseSweepEnabled", "false");
        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DbConnectionString"] = _database.GetConnectionString(),
            ["CoreJwt:Issuer"] = "Elmanhg.Tests",
            ["CoreJwt:Audience"] = "Elmanhg.Tests",
            ["CoreJwt:Key"] = TestJwtKey,
            ["AuditLogs:MaxPageSize"] = "100",
            ["AuditLogs:FilterMaxLength"] = "256",
            ["Content:SubjectNameMaxLength"] = "100",
            ["Content:UnitNameMaxLength"] = "100",
            ["Content:LessonNameMaxLength"] = "100",
            ["Content:LessonExplanationMaxLength"] = "100000",
            ["Content:LessonSummaryMaxLength"] = "20000",
            ["Content:LessonObjectiveMaxLength"] = "300",
            ["Content:LessonObjectivesMaxCount"] = "20",
            ["Content:LessonVideoUrlMaxLength"] = "2048",
            ["Content:LessonImageMaxSizeInMb"] = "5",
            ["Content:QuestionStemMaxLength"] = "20000",
            ["Content:QuestionExplanationMaxLength"] = "20000",
            ["Content:QuestionOptionsMaxCount"] = "10",
            ["Content:QuestionOptionTextMaxLength"] = "2000",
            ["Content:QuestionBlanksMaxCount"] = "10",
            ["Content:QuestionAcceptedAnswersMaxCount"] = "20",
            ["Content:QuestionAnswerMaxLength"] = "200",
            ["Content:QuestionTagsMaxCount"] = "10",
            ["Content:QuestionTagMaxLength"] = "50",
            ["Content:QuestionMaxScoreMax"] = "100",
            ["Content:QuestionListMaxPageSize"] = "100",
            ["Content:QuestionFilterMaxLength"] = "200",
            ["Content:QuestionImportMaxRows"] = "500",
            ["Content:QuestionImportMaxFileSizeInMb"] = "5",
            ["Content:ServableCountCacheSeconds"] = "60",
            ["QuestionValidation:QueueMaxPageSize"] = "100",
            ["QuestionValidation:QueueMaxAgeDays"] = "365",
            ["QuestionValidation:RejectionReasonMaxLength"] = "1000",
            ["QuestionValidation:BulkApproveMaxCount"] = "50",
            ["QuestionValidation:ReviewSessionLifetimeMinutes"] = "480",
            ["Sessions:DefaultQuizSize"] = "10",
            ["Sessions:MinQuizSize"] = "5",
            ["Sessions:MaxQuizSize"] = "20",
            ["Sessions:AnswerMaxLength"] = "4000",
            ["Mastery:CorrectThreshold"] = "0.8",
            ["Progress:StreakTimeZone"] = "Africa/Cairo",
            ["Progress:StreakMaxDays"] = "365",
            ["Progress:WeakLessonCount"] = "4",
            ["Progress:WeakObjectiveCount"] = "3",
            ["Progress:HistoryMaxPageSize"] = "50",
            ["ExamBlueprints:MaxQuestionCount"] = "100",
            ["ExamBlueprints:MaxTimeLimitMinutes"] = "300",
            ["Exams:DeadlineGraceSeconds"] = "30",
            ["Exams:AutoSubmitIntervalSeconds"] = "60",
            ["Exams:AutoSubmitBatchSize"] = "50",
            ["Exams:WeakestObjectiveCount"] = "3",
            ["Subscriptions:Currency"] = "EGP",
            ["Subscriptions:GracePeriodDays"] = "3",
            ["Subscriptions:FreeDailyQuizQuestions"] = "10",
            ["Subscriptions:FreeDailyAvatarMessages"] = "5",
            ["Subscriptions:FreeOpenLessonsPerUnit"] = "1",
            ["Subscriptions:BaseDailyAvatarMessages"] = "50",
            ["Subscriptions:BasePrices:Monthly:Months"] = "1",
            ["Subscriptions:BasePrices:Monthly:AmountMinor"] = "19900",
            ["Subscriptions:BasePrices:Termly:Months"] = "4",
            ["Subscriptions:BasePrices:Termly:AmountMinor"] = "69900",
            ["Subscriptions:BasePrices:Yearly:Months"] = "12",
            ["Subscriptions:BasePrices:Yearly:AmountMinor"] = "179900",
            ["Subscriptions:AskTeacherMonthlyQuestions"] = "20",
            ["Subscriptions:AskTeacherReplySlaHours"] = "24",
            ["Subscriptions:AskTeacherMonthlyPriceMinor"] = "9900",
            ["Subscriptions:PaymentHistoryMaxPageSize"] = "50",
            ["Subscriptions:RenewalWindowDays"] = "7",
            ["Subscriptions:LapseSweepIntervalSeconds"] = "300",
            ["Subscriptions:LapseSweepBatchSize"] = "100",
            ["Subscriptions:AdminPaymentLogMaxPageSize"] = "100",
            ["Subscriptions:RefundReasonMaxLength"] = "500",
            ["Subscriptions:PaymentLogReferenceMaxLength"] = "100",
            ["Payments:Provider"] = "Fake",
            ["Payments:FakeCheckoutPath"] = "/student/fake-checkout",
            ["Payments:AttemptTimeoutSeconds"] = "10",
            ["Payments:TotalTimeoutSeconds"] = "30",
            ["Payments:Paymob:HmacSecret"] = TestPaymobHmacSecret,
            ["FileStorage:Provider"] = "Local",
            ["FileStorage:LocalRootPath"] = MediaRoot,
            ["FileStorage:PublicBaseUrl"] = "/api/media",
            ["CoreOtp:Secret"] = TestOtpSecret,
            ["OtpDelivery:DefaultPhoneChannel"] = "WhatsApp",
            ["OtpDelivery:CountryCallingCode"] = "20",
            ["OtpDelivery:AttemptTimeoutSeconds"] = "10",
            ["OtpDelivery:TotalTimeoutSeconds"] = "30",
            ["OtpDelivery:WhatsApp:Enabled"] = "true",
            ["OtpDelivery:WhatsApp:Provider"] = "Fake",
            ["OtpDelivery:Email:Enabled"] = "true",
            ["OtpDelivery:Email:Provider"] = "Fake",
            ["OtpDelivery:Sms:Enabled"] = "false",
            ["OtpDelivery:Sms:Provider"] = "Fake",
            ["Auth:DisplayNameMaxLength"] = "100",
            ["Auth:EmailMaxLength"] = "256",
            ["Auth:RefreshTokenCookieName"] = "elmanhg_refresh",
            ["Auth:RefreshTokenCookiePath"] = "/api/auth",
            ["Auth:RefreshTokenCookieSecure"] = "true",
            ["Auth:OtpRequestPermitLimit"] = "1000",
            ["Auth:OtpRequestWindowSeconds"] = "600",
            ["Auth:CredentialPermitLimit"] = "1000",
            ["Auth:CredentialWindowSeconds"] = "60",
            ["Students:SubjectInterestsMaxCount"] = "50",
            ["Analytics:FunnelEventPermitLimit"] = "1000",
            ["Analytics:FunnelEventWindowSeconds"] = "60",
            ["IdentityOptions:User:RequireUniqueEmail"] = "false",
            ["IdentityOptions:Password:RequiredLength"] = "8",
            ["IdentityOptions:Password:RequireDigit"] = "true",
            ["IdentityOptions:Password:RequireLowercase"] = "false",
            ["IdentityOptions:Password:RequireUppercase"] = "false",
            ["IdentityOptions:Password:RequireNonAlphanumeric"] = "false",
            ["IdentityOptions:Password:RequiredUniqueChars"] = "1",
            ["IdentityOptions:Lockout:AllowedForNewUsers"] = "true",
            ["IdentityOptions:Lockout:MaxFailedAccessAttempts"] = "5",
            ["IdentityOptions:Lockout:DefaultLockoutTimeSpan"] = "00:15:00",
        }));
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IOtpChannel>();
            services.AddSingleton<IOtpChannel>(new RecordingOtpChannel(OtpChannel.WhatsApp, Otp));
            services.AddSingleton<IOtpChannel>(new RecordingOtpChannel(OtpChannel.Sms, Otp));
            services.AddSingleton<IOtpChannel>(new RecordingOtpChannel(OtpChannel.Email, Otp));
            services.AddSingleton(LessonEvents);
            services.RemoveAll<ILocalizer>();
            services.AddScoped<ILocalizer>(x => new Localizer(new ApiResourceStringLocalizerFactory(x.GetRequiredService<IStringLocalizerFactory>())));
            services.AddControllers().AddApplicationPart(typeof(ApiFactory).Assembly);
            services.AddMediatR(configuration => configuration.RegisterServicesFromAssembly(typeof(ApiFactory).Assembly));
        });
    }

    public new async ValueTask DisposeAsync()
    {
        await _database.DisposeAsync();
        if (Directory.Exists(MediaRoot))
        {
            Directory.Delete(MediaRoot, recursive: true);
        }

        await base.DisposeAsync();
    }
}
