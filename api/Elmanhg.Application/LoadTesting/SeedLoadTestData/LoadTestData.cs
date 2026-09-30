namespace Elmanhg.Application.LoadTesting.SeedLoadTestData;

public static class LoadTestData
{
    public const string EmailDomain = "loadtest.example.com";
    public const string SubscriptionReference = "loadtest";

    public static string SubjectName(string key) => $"Load test {key}";

    public static string TeacherEmail(string key) => $"{key}-teacher@{EmailDomain}";

    public static string StudentEmail(string key, int index) => $"{key}-student-{index:D3}@{EmailDomain}";

    public static string StudentDisplayName(int index) => $"طالب اختبار {index}";
}
