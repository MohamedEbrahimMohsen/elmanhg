using Core.Errors;
using Core.Identity.Tokens.CurrentUser;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.TeacherInbox.GetVoiceDraft;
using Elmanhg.Domain.TeacherThreads;
using FluentAssertions;
using NSubstitute;
using System.Linq.Expressions;

namespace Elmanhg.Tests.Application.Features.TeacherInbox.GetVoiceDraft;

public sealed class GetVoiceDraftHandlerTests
{
    private static readonly DateTimeOffset RecordedAt = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);
    private readonly ITeacherVoiceDraftRepository _teacherVoiceDraftRepository = Substitute.For<ITeacherVoiceDraftRepository>();
    private readonly ICurrentUserService _currentUserService = Substitute.For<ICurrentUserService>();
    private readonly Guid _teacherId = Guid.NewGuid();
    private readonly List<TeacherVoiceDraft> _drafts = [];
    private readonly GetVoiceDraftHandler _handler;

    public GetVoiceDraftHandlerTests()
    {
        _currentUserService.UserId.Returns(_teacherId);
        _teacherVoiceDraftRepository.FirstOrDefaultAsync(Arg.Any<Expression<Func<TeacherVoiceDraft, bool>>>(), Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<TeacherVoiceDraft>, IQueryable<TeacherVoiceDraft>>?>(), Arg.Any<Func<IQueryable<TeacherVoiceDraft>, IOrderedQueryable<TeacherVoiceDraft>>?>(), Arg.Any<bool>())
            .Returns(call => _drafts.FirstOrDefault(call.Arg<Expression<Func<TeacherVoiceDraft, bool>>>().Compile()));
        _handler = new GetVoiceDraftHandler(_teacherVoiceDraftRepository, _currentUserService);
    }

    [Fact]
    public async Task Handle_NoCurrentUser_ThrowsUnauthorized()
    {
        _currentUserService.UserId.Returns((Guid?)null);
        var draft = Seed(_teacherId);

        var act = () => Handle(draft.ThreadId, draft.Id);

        (await act.Should().ThrowAsync<UnauthorizedCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.UserNotAuthenticated);
    }

    [Fact]
    public async Task Handle_OwnDraft_ReturnsDraft()
    {
        var draft = Seed(_teacherId);
        draft.CompleteTranscription("The transcript.", "whisper-1", RecordedAt.AddSeconds(5));

        var result = await Handle(draft.ThreadId, draft.Id);

        (result.Id, result.ThreadId, result.Status, result.Transcript, result.AudioDurationSeconds, result.RecordedAt, result.TranscribedAt).Should().Be((draft.Id, draft.ThreadId, TeacherVoiceDraftStatus.Ready, "The transcript.", 42, RecordedAt, (DateTimeOffset?)RecordedAt.AddSeconds(5)));
    }

    [Fact]
    public async Task Handle_OtherTeachersDraft_ThrowsNotFound()
    {
        var draft = Seed(Guid.NewGuid());

        var act = () => Handle(draft.ThreadId, draft.Id);

        (await act.Should().ThrowAsync<NotFoundCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.TeacherVoiceDraftNotFound);
    }

    [Fact]
    public async Task Handle_DraftOfAnotherThread_ThrowsNotFound()
    {
        var draft = Seed(_teacherId);

        var act = () => Handle(Guid.NewGuid(), draft.Id);

        (await act.Should().ThrowAsync<NotFoundCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.TeacherVoiceDraftNotFound);
    }

    private TeacherVoiceDraft Seed(Guid teacherId)
    {
        var draft = TeacherVoiceDraft.Record(Guid.NewGuid(), teacherId, "teacher-threads/a.webm", "/api/media/teacher-threads/a.webm", 42, RecordedAt);
        _drafts.Add(draft);
        return draft;
    }

    private Task<Elmanhg.Application.TeacherInbox.Shared.TeacherVoiceDraftResult> Handle(Guid threadId, Guid draftId) => _handler.Handle(new GetVoiceDraftQuery(threadId, draftId), TestContext.Current.CancellationToken);
}
