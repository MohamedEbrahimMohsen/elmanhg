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
    public const string LessonAlreadyPublished = "LESSON_ALREADY_PUBLISHED";
    public const string LessonAlreadyDraft = "LESSON_ALREADY_DRAFT";
    public const string LessonAlreadyArchived = "LESSON_ALREADY_ARCHIVED";
    public const string LessonNotPublished = "LESSON_NOT_PUBLISHED";
    public const string LessonIsPublished = "LESSON_IS_PUBLISHED";
    public const string LessonHasQuestions = "LESSON_HAS_QUESTIONS";

    // QUESTIONS
    public const string QuestionObjectiveNotInLesson = "QUESTION_OBJECTIVE_NOT_IN_LESSON";
    public const string QuestionTypeImmutable = "QUESTION_TYPE_IMMUTABLE";
    public const string QuestionNotPending = "QUESTION_NOT_PENDING";
    public const string QuestionValidatorNotAssigned = "QUESTION_VALIDATOR_NOT_ASSIGNED";
    public const string QuestionNotRejected = "QUESTION_NOT_REJECTED";
    public const string QuestionRejectionReasonRequired = "QUESTION_REJECTION_REASON_REQUIRED";
}
