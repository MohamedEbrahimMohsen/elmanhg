namespace Elmanhg.Domain.TrainingExports;

public enum TrainingExportSource { Attempts, Avatar, TeacherThreads, EssayGrades }

public static class TrainingExportSourceExtensions
{
    public static string ToFileSlug(this TrainingExportSource source)
    {
        return source switch
        {
            TrainingExportSource.Attempts => "attempts",
            TrainingExportSource.Avatar => "avatar",
            TrainingExportSource.TeacherThreads => "teacher-threads",
            TrainingExportSource.EssayGrades => "essay-grades",
            _ => throw new ArgumentOutOfRangeException(nameof(source)),
        };
    }
}
