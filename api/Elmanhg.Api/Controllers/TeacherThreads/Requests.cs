namespace Elmanhg.Api.Controllers.TeacherThreads;

public sealed record FollowUpTeacherThreadRequest(string? Text);

public sealed record RateTeacherThreadRequest(int Rating);
