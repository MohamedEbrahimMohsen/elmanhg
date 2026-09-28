namespace Elmanhg.Domain.SharedKernel.Exceptions;

public static class ErrorCodes
{
    // USERS
    public const string UserAlreadySuspended = "USER_ALREADY_SUSPENDED";

    // TEACHERS
    public const string UserNotTeacher = "USER_NOT_TEACHER";

    // CONTENT
    public const string SubjectHasUnits = "SUBJECT_HAS_UNITS";
    public const string ContentOrderInvalid = "CONTENT_ORDER_INVALID";
    public const string UnitHasLessons = "UNIT_HAS_LESSONS";
    public const string LessonObjectiveUnknown = "LESSON_OBJECTIVE_UNKNOWN";
}
