namespace Elmanhg.Application.TeacherThreads.Shared;

public static class TeacherThreadContextRules
{
    public static bool HasExactlyOne(Guid? lessonId, Guid? questionId, Guid? attemptId)
    {
        Guid?[] ids = [lessonId, questionId, attemptId];
        return ids.Count(x => x is not null) == 1 && ids.All(x => x != Guid.Empty);
    }
}
