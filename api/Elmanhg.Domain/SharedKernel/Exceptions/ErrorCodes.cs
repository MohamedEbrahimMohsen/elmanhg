namespace Elmanhg.Domain.SharedKernel.Exceptions;

public static class ErrorCodes
{
    // USERS
    public const string UserAlreadySuspended = "USER_ALREADY_SUSPENDED";
    public const string UserNotStudent = "USER_NOT_STUDENT";

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
    public const string QuestionAlreadyRetired = "QUESTION_ALREADY_RETIRED";
    public const string QuestionRetired = "QUESTION_RETIRED";
    public const string QuestionVersionChanged = "QUESTION_VERSION_CHANGED";

    // REVIEW SESSIONS
    public const string QuestionNotOpenedInSession = "QUESTION_NOT_OPENED_IN_SESSION";
    public const string ReviewSessionExpired = "REVIEW_SESSION_EXPIRED";

    // SESSIONS
    public const string SessionNoServableQuestions = "SESSION_NO_SERVABLE_QUESTIONS";
    public const string SessionQuestionNotServable = "SESSION_QUESTION_NOT_SERVABLE";
    public const string SessionQuestionDuplicate = "SESSION_QUESTION_DUPLICATE";
    public const string SessionAlreadySubmitted = "SESSION_ALREADY_SUBMITTED";
    public const string SessionQuestionAlreadyAnswered = "SESSION_QUESTION_ALREADY_ANSWERED";

    // EXAM BLUEPRINTS
    public const string ExamBlueprintShortfall = "EXAM_BLUEPRINT_SHORTFALL";
    public const string ExamBlueprintDefaultNotDeletable = "EXAM_BLUEPRINT_DEFAULT_NOT_DELETABLE";

    // EXAMS
    public const string ExamTimeExpired = "EXAM_TIME_EXPIRED";
    public const string ExamShortfall = "EXAM_SHORTFALL";

    // TEACHER THREADS
    public const string TeacherMessageTextRequired = "TEACHER_MESSAGE_TEXT_REQUIRED";
    public const string TeacherThreadAlreadyClaimed = "TEACHER_THREAD_ALREADY_CLAIMED";
    public const string TeacherThreadNotClaimed = "TEACHER_THREAD_NOT_CLAIMED";
    public const string TeacherThreadNotAwaitingReply = "TEACHER_THREAD_NOT_AWAITING_REPLY";

    // SUBSCRIPTIONS
    public const string SubscriptionPeriodInvalid = "SUBSCRIPTION_PERIOD_INVALID";
    public const string SubscriptionNotActive = "SUBSCRIPTION_NOT_ACTIVE";
    public const string SubscriptionEnded = "SUBSCRIPTION_ENDED";
    public const string SubscriptionAlreadyExpired = "SUBSCRIPTION_ALREADY_EXPIRED";
    public const string PaymentAmountInvalid = "PAYMENT_AMOUNT_INVALID";
    public const string PaymentNotPending = "PAYMENT_NOT_PENDING";
    public const string CheckoutPlanAlreadyActive = "CHECKOUT_PLAN_ALREADY_ACTIVE";
    public const string CheckoutRequiresBase = "CHECKOUT_REQUIRES_BASE";
    public const string PaymentAlreadyRefunded = "PAYMENT_ALREADY_REFUNDED";
    public const string PaymentNotRefundable = "PAYMENT_NOT_REFUNDABLE";
    public const string PaymentReviewNotOpen = "PAYMENT_REVIEW_NOT_OPEN";

    // CONTENT RETRIEVAL
    public const string ContentEmbeddingDimensionsInvalid = "CONTENT_EMBEDDING_DIMENSIONS_INVALID";
}
