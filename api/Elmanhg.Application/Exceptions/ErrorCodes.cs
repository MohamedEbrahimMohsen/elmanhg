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
    public const string EmailNotRegistered = "EMAIL_NOT_REGISTERED";
    public const string EmailCodeSignInNotAllowed = "EMAIL_CODE_SIGN_IN_NOT_ALLOWED";
    public const string OtpChannelUnavailable = "OTP_CHANNEL_UNAVAILABLE";
    public const string OtpDeliveryFailed = "OTP_DELIVERY_FAILED";

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
    public const string LessonAlreadyOpened = "LESSON_ALREADY_OPENED";

    // QUESTIONS
    public const string QuestionNotFound = "QUESTION_NOT_FOUND";
    public const string QuestionIdRequired = "QUESTION_ID_REQUIRED";
    public const string QuestionTypeRequired = "QUESTION_TYPE_REQUIRED";
    public const string QuestionTypeInvalid = "QUESTION_TYPE_INVALID";
    public const string QuestionStemRequired = "QUESTION_STEM_REQUIRED";
    public const string QuestionStemTooLong = "QUESTION_STEM_TOO_LONG";
    public const string QuestionExplanationTooLong = "QUESTION_EXPLANATION_TOO_LONG";
    public const string QuestionDifficultyRequired = "QUESTION_DIFFICULTY_REQUIRED";
    public const string QuestionDifficultyInvalid = "QUESTION_DIFFICULTY_INVALID";
    public const string QuestionMaxScoreRequired = "QUESTION_MAX_SCORE_REQUIRED";
    public const string QuestionMaxScoreInvalid = "QUESTION_MAX_SCORE_INVALID";
    public const string QuestionTagsTooMany = "QUESTION_TAGS_TOO_MANY";
    public const string QuestionTagRequired = "QUESTION_TAG_REQUIRED";
    public const string QuestionTagTooLong = "QUESTION_TAG_TOO_LONG";
    public const string QuestionBodyInvalid = "QUESTION_BODY_INVALID";
    public const string QuestionGradingSpecInvalid = "QUESTION_GRADING_SPEC_INVALID";
    public const string QuestionOptionsCountInvalid = "QUESTION_OPTIONS_COUNT_INVALID";
    public const string QuestionOptionIdInvalid = "QUESTION_OPTION_ID_INVALID";
    public const string QuestionOptionIdDuplicate = "QUESTION_OPTION_ID_DUPLICATE";
    public const string QuestionOptionTextRequired = "QUESTION_OPTION_TEXT_REQUIRED";
    public const string QuestionOptionTextTooLong = "QUESTION_OPTION_TEXT_TOO_LONG";
    public const string QuestionCorrectOptionInvalid = "QUESTION_CORRECT_OPTION_INVALID";
    public const string QuestionCorrectAnswerRequired = "QUESTION_CORRECT_ANSWER_REQUIRED";
    public const string QuestionBlanksCountInvalid = "QUESTION_BLANKS_COUNT_INVALID";
    public const string QuestionBlankIdInvalid = "QUESTION_BLANK_ID_INVALID";
    public const string QuestionBlankIdDuplicate = "QUESTION_BLANK_ID_DUPLICATE";
    public const string QuestionBlankPlaceholderMissing = "QUESTION_BLANK_PLACEHOLDER_MISSING";
    public const string QuestionBlankAnswersMismatch = "QUESTION_BLANK_ANSWERS_MISMATCH";
    public const string QuestionAcceptedAnswersInvalid = "QUESTION_ACCEPTED_ANSWERS_INVALID";
    public const string QuestionAnswerKindRequired = "QUESTION_ANSWER_KIND_REQUIRED";
    public const string QuestionNumericValueRequired = "QUESTION_NUMERIC_VALUE_REQUIRED";
    public const string QuestionToleranceInvalid = "QUESTION_TOLERANCE_INVALID";
    public const string QuestionAnswerInvalid = "QUESTION_ANSWER_INVALID";
    public const string QuestionPageNumberInvalid = "QUESTION_PAGE_NUMBER_INVALID";
    public const string QuestionPageSizeInvalid = "QUESTION_PAGE_SIZE_INVALID";
    public const string QuestionFilterTooLong = "QUESTION_FILTER_TOO_LONG";
    public const string QuestionVersionFilterInvalid = "QUESTION_VERSION_FILTER_INVALID";
    public const string QuestionStatusInvalid = "QUESTION_STATUS_INVALID";
    public const string QuestionVersionInvalid = "QUESTION_VERSION_INVALID";
    public const string QuestionRejectionReasonTooLong = "QUESTION_REJECTION_REASON_TOO_LONG";
    public const string QuestionAgeFilterInvalid = "QUESTION_AGE_FILTER_INVALID";
    public const string QuestionIdsRequired = "QUESTION_IDS_REQUIRED";
    public const string QuestionIdsTooMany = "QUESTION_IDS_TOO_MANY";
    public const string QuestionIdsDuplicate = "QUESTION_IDS_DUPLICATE";

    // REVIEW SESSIONS
    public const string ReviewSessionNotFound = "REVIEW_SESSION_NOT_FOUND";
    public const string ReviewSessionIdRequired = "REVIEW_SESSION_ID_REQUIRED";

    // SESSIONS
    public const string SessionNotFound = "SESSION_NOT_FOUND";
    public const string SessionIdRequired = "SESSION_ID_REQUIRED";
    public const string SessionQuestionCountInvalid = "SESSION_QUESTION_COUNT_INVALID";
    public const string SessionQuestionNotFound = "SESSION_QUESTION_NOT_FOUND";
    public const string SessionAlreadyInProgress = "SESSION_ALREADY_IN_PROGRESS";
    public const string SessionModifiedConcurrently = "SESSION_MODIFIED_CONCURRENTLY";
    public const string SessionHistoryPageNumberInvalid = "SESSION_HISTORY_PAGE_NUMBER_INVALID";
    public const string SessionHistoryPageSizeInvalid = "SESSION_HISTORY_PAGE_SIZE_INVALID";
    public const string SessionHistoryKindInvalid = "SESSION_HISTORY_KIND_INVALID";
    public const string AttemptAnswerTooLong = "ATTEMPT_ANSWER_TOO_LONG";
    public const string AttemptTimeTakenInvalid = "ATTEMPT_TIME_TAKEN_INVALID";

    // EXAM BLUEPRINTS
    public const string ExamBlueprintNotFound = "EXAM_BLUEPRINT_NOT_FOUND";
    public const string ExamBlueprintIdRequired = "EXAM_BLUEPRINT_ID_REQUIRED";
    public const string ExamBlueprintEmpty = "EXAM_BLUEPRINT_EMPTY";
    public const string ExamBlueprintTooLarge = "EXAM_BLUEPRINT_TOO_LARGE";
    public const string ExamBlueprintTypeDuplicate = "EXAM_BLUEPRINT_TYPE_DUPLICATE";
    public const string ExamBlueprintTypeCountRequired = "EXAM_BLUEPRINT_TYPE_COUNT_REQUIRED";
    public const string ExamBlueprintCountInvalid = "EXAM_BLUEPRINT_COUNT_INVALID";
    public const string ExamBlueprintDifficultyMixInvalid = "EXAM_BLUEPRINT_DIFFICULTY_MIX_INVALID";
    public const string ExamBlueprintTimeLimitInvalid = "EXAM_BLUEPRINT_TIME_LIMIT_INVALID";
    public const string ExamBlueprintPassMarkInvalid = "EXAM_BLUEPRINT_PASS_MARK_INVALID";
    public const string ExamBlueprintModifiedConcurrently = "EXAM_BLUEPRINT_MODIFIED_CONCURRENTLY";

    // EXAMS
    public const string ExamAlreadyInProgress = "EXAM_ALREADY_IN_PROGRESS";
    public const string UnitExamNoBlueprint = "UNIT_EXAM_NO_BLUEPRINT";
    public const string MultiUnitExamUnitsTooFew = "MULTI_UNIT_EXAM_UNITS_TOO_FEW";
    public const string MultiUnitExamUnitDuplicate = "MULTI_UNIT_EXAM_UNIT_DUPLICATE";
    public const string MultiUnitExamSizeInvalid = "MULTI_UNIT_EXAM_SIZE_INVALID";
    public const string MultiUnitExamNoBlueprint = "MULTI_UNIT_EXAM_NO_BLUEPRINT";
    public const string ExamLessonsNotOpened = "EXAM_LESSONS_NOT_OPENED";

    // QUESTION IMPORTS
    public const string QuestionImportFileRequired = "QUESTION_IMPORT_FILE_REQUIRED";
    public const string QuestionImportFileTypeInvalid = "QUESTION_IMPORT_FILE_TYPE_INVALID";
    public const string QuestionImportFileTooLarge = "QUESTION_IMPORT_FILE_TOO_LARGE";
    public const string QuestionImportBatchIdRequired = "QUESTION_IMPORT_BATCH_ID_REQUIRED";
    public const string QuestionImportEmpty = "QUESTION_IMPORT_EMPTY";
    public const string QuestionImportTooManyRows = "QUESTION_IMPORT_TOO_MANY_ROWS";
    public const string QuestionImportHasErrors = "QUESTION_IMPORT_HAS_ERRORS";
    public const string QuestionImportBatchConflict = "QUESTION_IMPORT_BATCH_CONFLICT";
    public const string QuestionImportColumnUnknown = "QUESTION_IMPORT_COLUMN_UNKNOWN";
    public const string QuestionImportColumnDuplicate = "QUESTION_IMPORT_COLUMN_DUPLICATE";
    public const string QuestionImportCellInvalid = "QUESTION_IMPORT_CELL_INVALID";
    public const string QuestionImportObjectiveInvalid = "QUESTION_IMPORT_OBJECTIVE_INVALID";

    // SPREADSHEETS
    public const string SpreadsheetUnreadable = "SPREADSHEET_UNREADABLE";

    // AUDIT LOGS
    public const string AuditLogPageNumberInvalid = "AUDIT_LOG_PAGE_NUMBER_INVALID";
    public const string AuditLogPageSizeInvalid = "AUDIT_LOG_PAGE_SIZE_INVALID";
    public const string AuditLogFilterTooLong = "AUDIT_LOG_FILTER_TOO_LONG";
    public const string AuditLogDateRangeInvalid = "AUDIT_LOG_DATE_RANGE_INVALID";

    // SUBSCRIPTIONS
    public const string PaymentHistoryPageNumberInvalid = "PAYMENT_HISTORY_PAGE_NUMBER_INVALID";
    public const string PaymentHistoryPageSizeInvalid = "PAYMENT_HISTORY_PAGE_SIZE_INVALID";
    public const string CheckoutPlanRequired = "CHECKOUT_PLAN_REQUIRED";
    public const string CheckoutPlanInvalid = "CHECKOUT_PLAN_INVALID";
    public const string CheckoutPeriodRequired = "CHECKOUT_PERIOD_REQUIRED";
    public const string CheckoutPeriodInvalid = "CHECKOUT_PERIOD_INVALID";
    public const string CheckoutPeriodUnavailable = "CHECKOUT_PERIOD_UNAVAILABLE";
    public const string PaymentNotFound = "PAYMENT_NOT_FOUND";
    public const string PaymentGatewayUnavailable = "PAYMENT_GATEWAY_UNAVAILABLE";
    public const string FakeCheckoutUnavailable = "FAKE_CHECKOUT_UNAVAILABLE";
    public const string SubscriptionNotFound = "SUBSCRIPTION_NOT_FOUND";
    public const string SubscriptionIdRequired = "SUBSCRIPTION_ID_REQUIRED";
    public const string SubscriptionModifiedConcurrently = "SUBSCRIPTION_MODIFIED_CONCURRENTLY";
    public const string PaymentModifiedConcurrently = "PAYMENT_MODIFIED_CONCURRENTLY";
    public const string PaymentTransactionAlreadyRecorded = "PAYMENT_TRANSACTION_ALREADY_RECORDED";
    public const string PaymentNotificationMismatch = "PAYMENT_NOTIFICATION_MISMATCH";
    public const string PaymobWebhookSignatureInvalid = "PAYMOB_WEBHOOK_SIGNATURE_INVALID";
    public const string PaymobWebhookPayloadInvalid = "PAYMOB_WEBHOOK_PAYLOAD_INVALID";
    public const string PaymentIdRequired = "PAYMENT_ID_REQUIRED";
    public const string PaymentRefundReasonRequired = "PAYMENT_REFUND_REASON_REQUIRED";
    public const string PaymentRefundReasonTooLong = "PAYMENT_REFUND_REASON_TOO_LONG";
    public const string PaymentRefundIdempotencyKeyRequired = "PAYMENT_REFUND_IDEMPOTENCY_KEY_REQUIRED";
    public const string PaymentRefundDeclined = "PAYMENT_REFUND_DECLINED";
    public const string PaymentNotSettled = "PAYMENT_NOT_SETTLED";
    public const string PaymentLogPageNumberInvalid = "PAYMENT_LOG_PAGE_NUMBER_INVALID";
    public const string PaymentLogPageSizeInvalid = "PAYMENT_LOG_PAGE_SIZE_INVALID";
    public const string PaymentLogStatusInvalid = "PAYMENT_LOG_STATUS_INVALID";
    public const string PaymentLogPlanInvalid = "PAYMENT_LOG_PLAN_INVALID";
    public const string PaymentLogReferenceTooLong = "PAYMENT_LOG_REFERENCE_TOO_LONG";
    public const string PaymentLogDateRangeInvalid = "PAYMENT_LOG_DATE_RANGE_INVALID";

    // FREE TIER
    public const string QuizDailyLimitReached = "QUIZ_DAILY_LIMIT_REACHED";
    public const string LessonLocked = "LESSON_LOCKED";
    public const string ExamRequiresSubscription = "EXAM_REQUIRES_SUBSCRIPTION";

    // STUDENTS
    public const string SubjectInterestsTooMany = "SUBJECT_INTERESTS_TOO_MANY";
    public const string SubjectInterestsDuplicate = "SUBJECT_INTERESTS_DUPLICATE";

    // ASK A TEACHER
    public const string TeacherThreadNotFound = "TEACHER_THREAD_NOT_FOUND";
    public const string AttemptNotFound = "ATTEMPT_NOT_FOUND";
    public const string TeacherThreadExamInProgress = "TEACHER_THREAD_EXAM_IN_PROGRESS";
    public const string AskTeacherRequiresSubscription = "ASK_TEACHER_REQUIRES_SUBSCRIPTION";
    public const string AskTeacherMonthlyLimitReached = "ASK_TEACHER_MONTHLY_LIMIT_REACHED";
    public const string TeacherThreadContextInvalid = "TEACHER_THREAD_CONTEXT_INVALID";
    public const string TeacherThreadTextRequired = "TEACHER_THREAD_TEXT_REQUIRED";
    public const string TeacherThreadTextTooLong = "TEACHER_THREAD_TEXT_TOO_LONG";
    public const string TeacherThreadImageTypeInvalid = "TEACHER_THREAD_IMAGE_TYPE_INVALID";
    public const string TeacherThreadImageTooLarge = "TEACHER_THREAD_IMAGE_TOO_LARGE";
    public const string TeacherThreadPageNumberInvalid = "TEACHER_THREAD_PAGE_NUMBER_INVALID";
    public const string TeacherThreadPageSizeInvalid = "TEACHER_THREAD_PAGE_SIZE_INVALID";
    public const string TeacherThreadModifiedConcurrently = "TEACHER_THREAD_MODIFIED_CONCURRENTLY";
    public const string TeacherThreadReplyTextRequired = "TEACHER_THREAD_REPLY_TEXT_REQUIRED";
    public const string TeacherThreadReplyTextTooLong = "TEACHER_THREAD_REPLY_TEXT_TOO_LONG";
    public const string TeacherInboxFilterInvalid = "TEACHER_INBOX_FILTER_INVALID";

    // ANALYTICS
    public const string FunnelAnonymousIdRequired = "FUNNEL_ANONYMOUS_ID_REQUIRED";
    public const string FunnelEventTypeInvalid = "FUNNEL_EVENT_TYPE_INVALID";

    // OBSERVABILITY
    public const string ClientErrorMessageRequired = "CLIENT_ERROR_MESSAGE_REQUIRED";
    public const string ClientErrorMessageTooLong = "CLIENT_ERROR_MESSAGE_TOO_LONG";
    public const string ClientErrorNameTooLong = "CLIENT_ERROR_NAME_TOO_LONG";
    public const string ClientErrorStackTooLong = "CLIENT_ERROR_STACK_TOO_LONG";
    public const string ClientErrorPathTooLong = "CLIENT_ERROR_PATH_TOO_LONG";
    public const string ClientErrorPathInvalid = "CLIENT_ERROR_PATH_INVALID";
    public const string ClientErrorSourceInvalid = "CLIENT_ERROR_SOURCE_INVALID";

    // AVATAR
    public const string AvatarExamInProgress = "AVATAR_EXAM_IN_PROGRESS";
    public const string AvatarDailyLimitReached = "AVATAR_DAILY_LIMIT_REACHED";
    public const string AvatarQuestionNotAnswered = "AVATAR_QUESTION_NOT_ANSWERED";
    public const string AvatarEntryPointInvalid = "AVATAR_ENTRY_POINT_INVALID";
    public const string AvatarMessageRequired = "AVATAR_MESSAGE_REQUIRED";
    public const string AvatarMessageTooLong = "AVATAR_MESSAGE_TOO_LONG";
    public const string AvatarHistoryTooLong = "AVATAR_HISTORY_TOO_LONG";
    public const string AvatarHistoryInvalid = "AVATAR_HISTORY_INVALID";

    // AI SERVICE
    public const string AiServiceUnavailable = "AI_SERVICE_UNAVAILABLE";

    // CONTENT RETRIEVAL
    public const string ContentSearchQueryRequired = "CONTENT_SEARCH_QUERY_REQUIRED";
    public const string ContentSearchQueryTooLong = "CONTENT_SEARCH_QUERY_TOO_LONG";
    public const string ContentSearchTopInvalid = "CONTENT_SEARCH_TOP_INVALID";

    // PLATFORM
    public const string TooManyRequests = "TOO_MANY_REQUESTS";
    public const string AdminSeedFailed = "ADMIN_SEED_FAILED";
}
