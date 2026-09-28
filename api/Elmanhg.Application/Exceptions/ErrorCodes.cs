namespace Elmanhg.Application.Exceptions;

public static class ErrorCodes
{
    // AUTH
    public const string UserNotAuthenticated = "USER_NOT_AUTHENTICATED";
    public const string UserNotFound = "USER_NOT_FOUND";
    public const string UserInvalidLogin = "USER_INVALID_LOGIN";
    public const string UserLockedOut = "USER_LOCKED_OUT";
    public const string UserSuspended = "USER_SUSPENDED";
    public const string UserCreationFailed = "USER_CREATION_FAILED";
    public const string PhoneNumberAlreadyRegistered = "PHONE_NUMBER_ALREADY_REGISTERED";
    public const string PhoneNumberNotRegistered = "PHONE_NUMBER_NOT_REGISTERED";
    public const string EmailAlreadyRegistered = "EMAIL_ALREADY_REGISTERED";
    public const string OtpInvalid = "OTP_INVALID";

    // VALIDATION
    public const string OtpVerificationIdInvalidFormat = "OTP_VERIFICATION_ID_INVALID_FORMAT";
    public const string RefreshTokenIsRequired = "REFRESH_TOKEN_IS_REQUIRED";
    public const string DisplayNameRequired = "DISPLAY_NAME_REQUIRED";
    public const string DisplayNameTooLong = "DISPLAY_NAME_TOO_LONG";
    public const string EmailRequired = "EMAIL_REQUIRED";
    public const string EmailInvalid = "EMAIL_INVALID";
    public const string EmailTooLong = "EMAIL_TOO_LONG";
    public const string PasswordIsRequired = "PASSWORD_IS_REQUIRED";
    public const string PasswordTooShort = "PASSWORD_TOO_SHORT";
    public const string PasswordMustContainDigit = "PASSWORD_MUST_CONTAIN_DIGIT";

    // SUBJECTS & TEACHERS
    public const string SubjectOutOfScope = "SUBJECT_OUT_OF_SCOPE";
    public const string SubjectNotFound = "SUBJECT_NOT_FOUND";
    public const string TeacherSubjectAlreadyAssigned = "TEACHER_SUBJECT_ALREADY_ASSIGNED";
    public const string TeacherSubjectNotAssigned = "TEACHER_SUBJECT_NOT_ASSIGNED";
    public const string TeacherIdRequired = "TEACHER_ID_REQUIRED";
    public const string SubjectIdRequired = "SUBJECT_ID_REQUIRED";
    public const string SubjectNameRequired = "SUBJECT_NAME_REQUIRED";
    public const string SubjectNameTooLong = "SUBJECT_NAME_TOO_LONG";
    public const string SubjectPositionInvalid = "SUBJECT_POSITION_INVALID";

    // UNITS
    public const string UnitNotFound = "UNIT_NOT_FOUND";
    public const string UnitIdRequired = "UNIT_ID_REQUIRED";
    public const string UnitNameRequired = "UNIT_NAME_REQUIRED";
    public const string UnitNameTooLong = "UNIT_NAME_TOO_LONG";
    public const string UnitPositionInvalid = "UNIT_POSITION_INVALID";

    // LESSONS
    public const string LessonNotFound = "LESSON_NOT_FOUND";
    public const string LessonIdRequired = "LESSON_ID_REQUIRED";
    public const string LessonPositionInvalid = "LESSON_POSITION_INVALID";
    public const string LessonNameRequired = "LESSON_NAME_REQUIRED";
    public const string LessonNameTooLong = "LESSON_NAME_TOO_LONG";
    public const string LessonExplanationTooLong = "LESSON_EXPLANATION_TOO_LONG";
    public const string LessonSummaryTooLong = "LESSON_SUMMARY_TOO_LONG";
    public const string LessonVideoUrlInvalid = "LESSON_VIDEO_URL_INVALID";
    public const string LessonVideoUrlTooLong = "LESSON_VIDEO_URL_TOO_LONG";
    public const string LessonObjectivesTooMany = "LESSON_OBJECTIVES_TOO_MANY";
    public const string LessonObjectiveTextRequired = "LESSON_OBJECTIVE_TEXT_REQUIRED";
    public const string LessonObjectiveTextTooLong = "LESSON_OBJECTIVE_TEXT_TOO_LONG";
    public const string LessonObjectiveDuplicate = "LESSON_OBJECTIVE_DUPLICATE";
    public const string LessonImageRequired = "LESSON_IMAGE_REQUIRED";
    public const string LessonImageTypeInvalid = "LESSON_IMAGE_TYPE_INVALID";
    public const string LessonImageTooLarge = "LESSON_IMAGE_TOO_LARGE";

    // AUDIT LOGS
    public const string AuditLogPageNumberInvalid = "AUDIT_LOG_PAGE_NUMBER_INVALID";
    public const string AuditLogPageSizeInvalid = "AUDIT_LOG_PAGE_SIZE_INVALID";
    public const string AuditLogFilterTooLong = "AUDIT_LOG_FILTER_TOO_LONG";
    public const string AuditLogDateRangeInvalid = "AUDIT_LOG_DATE_RANGE_INVALID";

    // PLATFORM
    public const string TooManyRequests = "TOO_MANY_REQUESTS";
    public const string AdminSeedFailed = "ADMIN_SEED_FAILED";
}
