namespace Elmanhg.Api.Controllers.TeacherInbox;

public sealed record ReplyToTeacherThreadRequest(string? Text);

public sealed record SendVoiceReplyRequest(Guid DraftId, string? Text);
