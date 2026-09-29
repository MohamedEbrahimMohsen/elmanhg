using Core.DDD.Models;
using Elmanhg.Application.TeacherInbox.ClaimTeacherThread;
using Elmanhg.Application.TeacherInbox.GetInboxThread;
using Elmanhg.Application.TeacherInbox.GetTeacherInbox;
using Elmanhg.Application.TeacherInbox.GetTeacherInboxReminders;
using Elmanhg.Application.TeacherInbox.GetVoiceDraft;
using Elmanhg.Application.TeacherInbox.GetVoiceReplySettings;
using Elmanhg.Application.TeacherInbox.RecordVoiceDraft;
using Elmanhg.Application.TeacherInbox.ReplyToTeacherThread;
using Elmanhg.Application.TeacherInbox.SendVoiceReply;
using Elmanhg.Application.TeacherInbox.Shared;
using Elmanhg.Domain.SharedKernel;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Elmanhg.Api.Controllers.TeacherInbox;

[ApiController]
[Route("api/teacher-inbox")]
[Authorize]
public class TeacherInboxController(IMediator mediator) : ControllerBase
{
    [HttpGet(Name = "GetTeacherInbox")]
    [Authorize(Policy = DefaultCodes.AskTeacherReply)]
    [ProducesResponseType<PageData<TeacherInboxItemResult>>(StatusCodes.Status200OK)]
    public async Task<ActionResult> GetTeacherInbox([FromQuery] TeacherInboxFilter filter = TeacherInboxFilter.All, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20, CancellationToken cancellationToken = default)
    {
        var result = await mediator.Send(new GetTeacherInboxQuery(filter, pageNumber, pageSize), cancellationToken);
        return Ok(result);
    }

    [HttpGet("voice-settings", Name = "GetVoiceReplySettings")]
    [Authorize(Policy = DefaultCodes.AskTeacherReply)]
    [ProducesResponseType<VoiceReplySettingsResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult> GetVoiceReplySettings(CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetVoiceReplySettingsQuery(), cancellationToken);
        return Ok(result);
    }

    [HttpGet("reminders", Name = "GetTeacherInboxReminders")]
    [Authorize(Policy = DefaultCodes.AskTeacherReply)]
    [ProducesResponseType<List<TeacherInboxReminderResult>>(StatusCodes.Status200OK)]
    public async Task<ActionResult> GetTeacherInboxReminders(CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetTeacherInboxRemindersQuery(), cancellationToken);
        return Ok(result);
    }

    [HttpGet("{threadId:guid}", Name = "GetInboxThread")]
    [Authorize(Policy = DefaultCodes.AskTeacherReply)]
    [ProducesResponseType<TeacherInboxThreadResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult> GetInboxThread([FromRoute] Guid threadId, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetInboxThreadQuery(threadId), cancellationToken);
        return Ok(result);
    }

    [HttpPost("{threadId:guid}/claim", Name = "ClaimTeacherThread")]
    [Authorize(Policy = DefaultCodes.AskTeacherReply)]
    [ProducesResponseType<TeacherInboxThreadResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult> ClaimTeacherThread([FromRoute] Guid threadId, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new ClaimTeacherThreadCommand(threadId), cancellationToken);
        return Ok(result);
    }

    [HttpPost("{threadId:guid}/replies", Name = "ReplyToTeacherThread")]
    [Authorize(Policy = DefaultCodes.AskTeacherReply)]
    [ProducesResponseType<TeacherInboxThreadResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult> ReplyToTeacherThread([FromRoute] Guid threadId, [FromBody] ReplyToTeacherThreadRequest request, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new ReplyToTeacherThreadCommand(threadId, request.Text), cancellationToken);
        return Ok(result);
    }

    [HttpPost("{threadId:guid}/voice-drafts", Name = "RecordTeacherVoiceDraft")]
    [Authorize(Policy = DefaultCodes.AskTeacherReply)]
    [Consumes("multipart/form-data")]
    [ProducesResponseType<TeacherVoiceDraftResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult> RecordTeacherVoiceDraft([FromRoute] Guid threadId, IFormFile? audio, [FromForm] int durationSeconds, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new RecordVoiceDraftCommand(threadId, audio, durationSeconds), cancellationToken);
        return Ok(result);
    }

    [HttpGet("{threadId:guid}/voice-drafts/{draftId:guid}", Name = "GetTeacherVoiceDraft")]
    [Authorize(Policy = DefaultCodes.AskTeacherReply)]
    [ProducesResponseType<TeacherVoiceDraftResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult> GetTeacherVoiceDraft([FromRoute] Guid threadId, [FromRoute] Guid draftId, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetVoiceDraftQuery(threadId, draftId), cancellationToken);
        return Ok(result);
    }

    [HttpPost("{threadId:guid}/voice-replies", Name = "SendTeacherVoiceReply")]
    [Authorize(Policy = DefaultCodes.AskTeacherReply)]
    [ProducesResponseType<TeacherInboxThreadResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult> SendTeacherVoiceReply([FromRoute] Guid threadId, [FromBody] SendVoiceReplyRequest request, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new SendVoiceReplyCommand(threadId, request.DraftId, request.Text), cancellationToken);
        return Ok(result);
    }
}
