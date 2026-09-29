using Elmanhg.Application.Shared.Options;
using Elmanhg.Application.TeacherInbox.FailVoiceDraftTranscription;
using Elmanhg.Domain.TeacherThreads;
using FluentAssertions;
using Microsoft.Extensions.Options;
using NSubstitute;
using System.Linq.Expressions;

namespace Elmanhg.Tests.Application.Features.TeacherInbox.FailVoiceDraftTranscription;

public sealed class FailVoiceDraftTranscriptionHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);
    private readonly ITeacherVoiceDraftRepository _teacherVoiceDraftRepository = Substitute.For<ITeacherVoiceDraftRepository>();
    private readonly TimeProvider _timeProvider = Substitute.For<TimeProvider>();
    private readonly List<TeacherVoiceDraft> _drafts = [];

    public FailVoiceDraftTranscriptionHandlerTests()
    {
        _timeProvider.GetUtcNow().Returns(Now);
        _teacherVoiceDraftRepository.FirstOrDefaultAsync(Arg.Any<Expression<Func<TeacherVoiceDraft, bool>>>(), Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<TeacherVoiceDraft>, IQueryable<TeacherVoiceDraft>>?>(), Arg.Any<Func<IQueryable<TeacherVoiceDraft>, IOrderedQueryable<TeacherVoiceDraft>>?>(), Arg.Any<bool>())
            .Returns(call => _drafts.FirstOrDefault(call.Arg<Expression<Func<TeacherVoiceDraft, bool>>>().Compile()));
    }

    [Fact]
    public async Task Handle_UnknownDraft_DoesNothing()
    {
        await Handle(Guid.NewGuid());

        await _teacherVoiceDraftRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_NotPending_DoesNothing()
    {
        var draft = Seed();
        draft.CompleteTranscription("Done.", "whisper-1", Now.AddSeconds(-5));

        await Handle(draft.Id);

        (draft.Status, draft.Attempts).Should().Be((TeacherVoiceDraftStatus.Ready, 1));
        await _teacherVoiceDraftRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_Pending_SchedulesRetry()
    {
        var draft = Seed();

        await Handle(draft.Id);

        (draft.Status, draft.Attempts, draft.NextAttemptAt).Should().Be((TeacherVoiceDraftStatus.Pending, 1, (DateTimeOffset?)Now.AddSeconds(15)));
        await _teacherVoiceDraftRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_LastAttempt_MarksFailed()
    {
        var draft = Seed();

        await Handle(draft.Id, new AskTeacherOptions { TranscriptionMaxAttempts = 1 });

        (draft.Status, draft.NextAttemptAt).Should().Be((TeacherVoiceDraftStatus.Failed, (DateTimeOffset?)null));
        await _teacherVoiceDraftRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    private TeacherVoiceDraft Seed()
    {
        var draft = TeacherVoiceDraft.Record(Guid.NewGuid(), Guid.NewGuid(), "teacher-threads/voice.webm", "/api/media/teacher-threads/voice.webm", 42, Now.AddMinutes(-1));
        _drafts.Add(draft);
        return draft;
    }

    private Task Handle(Guid draftId, AskTeacherOptions? options = null)
    {
        var handler = new FailVoiceDraftTranscriptionHandler(_teacherVoiceDraftRepository, Options.Create(options ?? new AskTeacherOptions()), _timeProvider);
        return handler.Handle(new FailVoiceDraftTranscriptionCommand(draftId), TestContext.Current.CancellationToken);
    }
}
