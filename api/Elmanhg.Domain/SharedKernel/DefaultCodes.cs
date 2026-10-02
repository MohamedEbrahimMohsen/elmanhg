namespace Elmanhg.Domain.SharedKernel;

public static class DefaultCodes
{
    // AUTH
    public const string AuthenticatedUser = "AuthenticatedUser";

    // CAPABILITIES (PRD §16)
    public const string ContentBrowse = "Content.Browse";
    public const string AssessmentsTake = "Assessments.Take";
    public const string ContentManage = "Content.Manage";
    public const string LessonsPublish = "Lessons.Publish";
    public const string QuestionsValidate = "Questions.Validate";
    public const string QuestionsChangeDifficulty = "Questions.ChangeDifficulty";
    public const string BlueprintsManage = "Blueprints.Manage";
    public const string AskTeacherSubmit = "AskTeacher.Submit";
    public const string AskTeacherReply = "AskTeacher.Reply";
    public const string AiGradesOverride = "AiGrades.Override";
    public const string ProgressViewOwn = "Progress.ViewOwn";
    public const string ProgressViewAny = "Progress.ViewAny";
    public const string SubscriptionManage = "Subscription.Manage";
    public const string PaymentsManage = "Payments.Manage";
    public const string DashboardsView = "Dashboards.View";
    public const string TeacherStatsViewOwn = "TeacherStats.ViewOwn";
    public const string UsersManage = "Users.Manage";
    public const string AuditLogView = "AuditLog.View";
    public const string TrainingDataExport = "TrainingData.Export";
    public const string AvatarChat = "Avatar.Chat";
    public const string AvatarConversationsView = "AvatarConversations.View";

    // CONFIGURATION
    public const string ConfigurationManage = "Configuration.Manage";
}
