using Core.Errors;
using Core.Identity.Tokens.CurrentUser;
using Elmanhg.Api.Realtime;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Shared.Realtime;
using Elmanhg.Application.TeacherInbox.SendVoiceReply;
using Elmanhg.Domain.Identity;
using Elmanhg.Domain.TeacherThreads;
using Elmanhg.Domain.Teachers;
using Elmanhg.Tests.Builders;
using FluentAssertions;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using System.Linq.Expressions;
using System.Security.Claims;
using DomainErrorCodes = Elmanhg.Domain.SharedKernel.Exceptions.ErrorCodes;

namespace Elmanhg.Tests.Application.Features.TeacherInbox.SendVoiceReply;

public sealed class SendVoiceReplyHandlerTests
{
    private const string CorrectedText = "The corrected transcript.";
    private const string AudioUrl = "/api/media/teacher-threads/voice.webm";
    private static readonly DateTimeOffset Now = TeacherThreadBuilder.DefaultSubmittedAt.AddHours(2);
    private readonly ITeacherThreadRepository _teacherThreadRepository = Substitute.For<ITeacherThreadRepository>();
    private readonly ITeacherVoiceDraftRepository _teacherVoiceDraftRepository = Substitute.For<ITeacherVoiceDraftRepository>();
    private readonly ITeacherSubjectRepository _teacherSubjectRepository = Substitute.For<ITeacherSubjectRepository>();
    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private readonly TimeProvider _timeProvider = Substitute.For<TimeProvider>();
    private readonly ICurrentUserService _currentUserService = Substitute.For<ICurrentUserService>();
    private readonly ITeacherThreadNotifier _notifier = Substitute.For<ITeacherThreadNotifier>();
    private readonly User _teacher = User.CreateTeacher("Mohamed", "teacher@example.com");
    private readonly User _student = User.CreateStudentWithEmail("Ahmed", "student@example.com");
    private readonly List<TeacherThread> _threads = [];
    private readonly List<TeacherVoiceDraft> _drafts = [];
    private readonly SendVoiceReplyHandler _handler;

    public SendVoiceReplyHandlerTests()
    {
        User[] users = [_teacher, _student];
        _currentUserService.UserId.Returns(_teacher.Id);
        _currentUserService.GetClaim(ClaimTypes.Role).Returns(nameof(UserRole.Teacher));
        _timeProvider.GetUtcNow().Returns(Now);
        _teacherSubjectRepository.IsAssignedAsync(_teacher.Id, Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(true);
        _teacherThreadRepository.FirstOrDefaultAsync(Arg.Any<Expression<Func<TeacherThread, bool>>>(), Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<TeacherThread>, IQueryable<TeacherThread>>?>(), Arg.Any<Func<IQueryable<TeacherThread>, IOrderedQueryable<TeacherThread>>?>(), Arg.Any<bool>())
            .Returns(call => _threads.FirstOrDefault(call.Arg<Expression<Func<TeacherThread, bool>>>().Compile()));
        _teacherVoiceDraftRepository.FirstOrDefaultAsync(Arg.Any<Expression<Func<TeacherVoiceDraft, bool>>>(), Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<TeacherVoiceDraft>, IQueryable<TeacherVoiceDraft>>?>(), Arg.Any<Func<IQueryable<TeacherVoiceDraft>, IOrderedQueryable<TeacherVoiceDraft>>?>(), Arg.Any<bool>())
            .Returns(call => _drafts.FirstOrDefault(call.Arg<Expression<Func<TeacherVoiceDraft, bool>>>().Compile()));
        _userRepository.FindAsync(Arg.Any<Expression<Func<User, bool>>>(), Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<User>, IQueryable<User>>?>(), Arg.Any<Func<IQueryable<User>, IOrderedQueryable<User>>?>(), Arg.Any<bool>())
            .Returns(call => users.Where(call.Arg<Expression<Func<User, bool>>>().Compile()).ToList());
        _handler = new SendVoiceReplyHandler(_teacherThreadRepository, _teacherVoiceDraftRepository, _teacherSubjectRepository, _userRepository, _timeProvider, _currentUserService, _notifier);
    }

    [Fact]
    public async Task Handle_NoCurrentUser_ThrowsUnauthorized()
    {
        _currentUserService.UserId.Returns((Guid?)null);
        var thread = SeedThread(new TeacherThreadBuilder().ClaimedBy(_teacher.Id));

        await ShouldThrow<UnauthorizedCoreException>(thread.Id, ReadyDraft(thread).Id, ErrorCodes.UserNotAuthenticated);
    }

    [Fact]
    public async Task Handle_UnknownThread_ThrowsNotFound()
    {
        await ShouldThrow<NotFoundCoreException>(Guid.NewGuid(), Guid.NewGuid(), ErrorCodes.TeacherThreadNotFound);
    }

    [Fact]
    public async Task Handle_TeacherOutsideSubject_ThrowsForbidden()
    {
        var thread = SeedThread(new TeacherThreadBuilder().ClaimedBy(_teacher.Id));
        _teacherSubjectRepository.IsAssignedAsync(_teacher.Id, thread.SubjectId, Arg.Any<CancellationToken>()).Returns(false);

        await ShouldThrow<ForbiddenCoreException>(thread.Id, ReadyDraft(thread).Id, ErrorCodes.SubjectOutOfScope);
    }

    [Fact]
    public async Task Handle_UnknownDraft_ThrowsDraftNotFound()
    {
        var thread = SeedThread(new TeacherThreadBuilder().ClaimedBy(_teacher.Id));

        await ShouldThrow<NotFoundCoreException>(thread.Id, Guid.NewGuid(), ErrorCodes.TeacherVoiceDraftNotFound);
    }

    [Fact]
    public async Task Handle_PendingDraft_ThrowsNotReady()
    {
        var thread = SeedThread(new TeacherThreadBuilder().ClaimedBy(_teacher.Id));

        await ShouldThrow<ConflictCoreException>(thread.Id, SeedDraft(thread).Id, DomainErrorCodes.TeacherVoiceDraftNotReady);
    }

    [Fact]
    public async Task Handle_SentDraft_ThrowsAlreadySent()
    {
        var thread = SeedThread(new TeacherThreadBuilder().ClaimedBy(_teacher.Id));
        var draft = ReadyDraft(thread);
        draft.MarkSent(Guid.NewGuid(), Now.AddMinutes(-1));

        await ShouldThrow<ConflictCoreException>(thread.Id, draft.Id, DomainErrorCodes.TeacherVoiceDraftAlreadySent);
    }

    [Fact]
    public async Task Handle_ThreadAnswered_ThrowsNotAwaitingReply()
    {
        var thread = SeedThread(new TeacherThreadBuilder().AnsweredBy(_teacher.Id));
        var draft = ReadyDraft(thread);

        await ShouldThrow<ConflictCoreException>(thread.Id, draft.Id, DomainErrorCodes.TeacherThreadNotAwaitingReply);
        draft.Status.Should().Be(TeacherVoiceDraftStatus.Ready);
    }

    [Fact]
    public async Task Handle_ReadyDraft_SendsVoiceReplyWithCorrectedText()
    {
        var thread = SeedThread(new TeacherThreadBuilder().ClaimedBy(_teacher.Id));
        var draft = ReadyDraft(thread);

        var result = await Handle(thread.Id, draft.Id);

        var message = thread.Messages.Last();
        (message.Kind, message.Text, message.AudioUrl).Should().Be((TeacherMessageKind.Voice, CorrectedText, AudioUrl));
        (draft.Status, draft.SentMessageId).Should().Be((TeacherVoiceDraftStatus.Sent, (Guid?)message.Id));
        thread.Status.Should().Be(TeacherThreadStatus.Answered);
        await _teacherThreadRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        result.CanReply.Should().BeFalse();
        (result.Messages.Last().AudioUrl, result.Messages.Last().AudioDurationSeconds).Should().Be((AudioUrl, (int?)42));
    }

    [Fact]
    public async Task Handle_FailedDraftWithTypedText_Sends()
    {
        var thread = SeedThread(new TeacherThreadBuilder().ClaimedBy(_teacher.Id));
        var draft = SeedDraft(thread);
        draft.FailAttempt(Now.AddMinutes(-5), 1, TimeSpan.FromSeconds(15));

        await Handle(thread.Id, draft.Id);

        draft.Status.Should().Be(TeacherVoiceDraftStatus.Sent);
        await _teacherThreadRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_VoiceReply_NotifiesStudentAfterSaving()
    {
        var thread = SeedThread(new TeacherThreadBuilder().ClaimedBy(_teacher.Id));

        await Handle(thread.Id, ReadyDraft(thread).Id);

        await _notifier.Received(1).NotifyReplyAsync(_student.Id, thread.Id, Arg.Any<CancellationToken>());
        Received.InOrder(() =>
        {
            _teacherThreadRepository.SaveChangesAsync(Arg.Any<CancellationToken>());
            _notifier.NotifyReplyAsync(_student.Id, thread.Id, Arg.Any<CancellationToken>());
        });
    }

    [Fact]
    public async Task Handle_PushFails_StillReturnsSavedVoiceReply()
    {
        var thread = SeedThread(new TeacherThreadBuilder().ClaimedBy(_teacher.Id));
        var draft = ReadyDraft(thread);
        var hubContext = Substitute.For<IHubContext<NotificationsHub>>();
        hubContext.Clients.User(Arg.Any<string>()).SendCoreAsync(Arg.Any<string>(), Arg.Any<object?[]>(), Arg.Any<CancellationToken>()).ThrowsAsync(new IOException("backplane down"));
        var notifier = new SignalRTeacherThreadNotifier(hubContext, NullLogger<SignalRTeacherThreadNotifier>.Instance);
        var handler = new SendVoiceReplyHandler(_teacherThreadRepository, _teacherVoiceDraftRepository, _teacherSubjectRepository, _userRepository, _timeProvider, _currentUserService, notifier);

        var result = await handler.Handle(new SendVoiceReplyCommand(thread.Id, draft.Id, CorrectedText), TestContext.Current.CancellationToken);

        (result.Status, draft.Status).Should().Be((TeacherThreadStatus.Answered, TeacherVoiceDraftStatus.Sent));
        await _teacherThreadRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    private TeacherThread SeedThread(TeacherThreadBuilder builder)
    {
        var thread = builder.ForStudent(_student.Id).Build();
        _threads.Add(thread);
        return thread;
    }

    private TeacherVoiceDraft SeedDraft(TeacherThread thread)
    {
        var draft = TeacherVoiceDraft.Record(thread.Id, _teacher.Id, "teacher-threads/voice.webm", AudioUrl, 42, Now.AddMinutes(-10));
        _drafts.Add(draft);
        return draft;
    }

    private TeacherVoiceDraft ReadyDraft(TeacherThread thread)
    {
        var draft = SeedDraft(thread);
        draft.CompleteTranscription("The machine transcript.", "whisper-1", Now.AddMinutes(-9));
        return draft;
    }

    private Task<Elmanhg.Application.TeacherInbox.Shared.TeacherInboxThreadResult> Handle(Guid threadId, Guid draftId) => _handler.Handle(new SendVoiceReplyCommand(threadId, draftId, CorrectedText), TestContext.Current.CancellationToken);

    private async Task ShouldThrow<TException>(Guid threadId, Guid draftId, string errorCode) where TException : BaseException
    {
        var act = () => Handle(threadId, draftId);

        (await act.Should().ThrowAsync<TException>()).Which.ErrorCode.Should().Be(errorCode);
        await _teacherThreadRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
