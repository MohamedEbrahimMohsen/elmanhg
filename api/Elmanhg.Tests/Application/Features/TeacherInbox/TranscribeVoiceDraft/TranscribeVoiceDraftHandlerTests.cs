using Core.Errors;
using Core.Storage;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Shared.AiService;
using Elmanhg.Application.Shared.Options;
using Elmanhg.Application.TeacherInbox.TranscribeVoiceDraft;
using Elmanhg.Domain.TeacherThreads;
using FluentAssertions;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using System.Linq.Expressions;

namespace Elmanhg.Tests.Application.Features.TeacherInbox.TranscribeVoiceDraft;

public sealed class TranscribeVoiceDraftHandlerTests
{
    private const string AudioKey = "teacher-threads/voice.webm";
    private static readonly DateTimeOffset Now = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly byte[] AudioBytes = [0x1A, 0x45, 0xDF, 0xA3, 0x01, 0x02];
    private readonly ITeacherVoiceDraftRepository _teacherVoiceDraftRepository = Substitute.For<ITeacherVoiceDraftRepository>();
    private readonly IFileStorage _fileStorage = Substitute.For<IFileStorage>();
    private readonly IAiTranscriptionClient _transcriptionClient = Substitute.For<IAiTranscriptionClient>();
    private readonly TimeProvider _timeProvider = Substitute.For<TimeProvider>();
    private readonly List<TeacherVoiceDraft> _drafts = [];
    private readonly TranscribeVoiceDraftHandler _handler;

    public TranscribeVoiceDraftHandlerTests()
    {
        _timeProvider.GetUtcNow().Returns(Now);
        _teacherVoiceDraftRepository.FirstOrDefaultAsync(Arg.Any<Expression<Func<TeacherVoiceDraft, bool>>>(), Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<TeacherVoiceDraft>, IQueryable<TeacherVoiceDraft>>?>(), Arg.Any<Func<IQueryable<TeacherVoiceDraft>, IOrderedQueryable<TeacherVoiceDraft>>?>(), Arg.Any<bool>())
            .Returns(call => _drafts.FirstOrDefault(call.Arg<Expression<Func<TeacherVoiceDraft, bool>>>().Compile()));
        _fileStorage.OpenReadAsync(AudioKey, Arg.Any<CancellationToken>()).Returns(_ => new StoredFile(new MemoryStream(AudioBytes), AudioBytes.Length, "audio/webm"));
        _transcriptionClient.TranscribeAsync(Arg.Any<AiTranscriptionRequest>(), Arg.Any<CancellationToken>()).Returns(new AiTranscriptionResult("  The transcript.  ", "whisper-1", "ar"));
        _handler = new TranscribeVoiceDraftHandler(_teacherVoiceDraftRepository, _fileStorage, _transcriptionClient, Options.Create(new AskTeacherOptions()), _timeProvider);
    }

    [Fact]
    public async Task Handle_UnknownDraft_DoesNothing()
    {
        await Handle(Guid.NewGuid());

        await ShouldNotTranscribe();
    }

    [Fact]
    public async Task Handle_DraftNotDue_DoesNothing()
    {
        var draft = Seed(Now.AddSeconds(-1));
        draft.FailAttempt(Now.AddSeconds(-1), 4, TimeSpan.FromSeconds(15));

        await Handle(draft.Id);

        await ShouldNotTranscribe();
        draft.Status.Should().Be(TeacherVoiceDraftStatus.Pending);
    }

    [Fact]
    public async Task Handle_ReadyDraft_DoesNothing()
    {
        var draft = Seed(Now.AddMinutes(-1));
        draft.CompleteTranscription("Done.", "whisper-1", Now.AddSeconds(-30));

        await Handle(draft.Id);

        await ShouldNotTranscribe();
    }

    [Fact]
    public async Task Handle_AudioMissing_ThrowsAudioNotFound()
    {
        var draft = Seed(Now.AddMinutes(-1));
        _fileStorage.OpenReadAsync(AudioKey, Arg.Any<CancellationToken>()).Returns((StoredFile?)null);

        var act = () => Handle(draft.Id);

        (await act.Should().ThrowAsync<NotFoundCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.TeacherVoiceAudioNotFound);
        await _teacherVoiceDraftRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_DueDraft_TranscribesAudioAndStoresTranscript()
    {
        var draft = Seed(Now.AddMinutes(-1));

        await Handle(draft.Id);

        await _transcriptionClient.Received(1).TranscribeAsync(Arg.Is<AiTranscriptionRequest>(x => x.Audio.SequenceEqual(AudioBytes) && x.ContentType == "audio/webm" && x.Language == "ar" && x.DurationSeconds == 42), Arg.Any<CancellationToken>());
        (draft.Status, draft.Transcript, draft.TranscriptionModel, draft.TranscribedAt).Should().Be((TeacherVoiceDraftStatus.Ready, "The transcript.", "whisper-1", (DateTimeOffset?)Now));
        await _teacherVoiceDraftRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ClientUnavailable_PropagatesWithoutSaving()
    {
        var draft = Seed(Now.AddMinutes(-1));
        _transcriptionClient.TranscribeAsync(Arg.Any<AiTranscriptionRequest>(), Arg.Any<CancellationToken>()).ThrowsAsync(new ServiceUnavailableCoreException(ErrorCodes.AiServiceUnavailable));

        var act = () => Handle(draft.Id);

        (await act.Should().ThrowAsync<ServiceUnavailableCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.AiServiceUnavailable);
        draft.Status.Should().Be(TeacherVoiceDraftStatus.Pending);
        await _teacherVoiceDraftRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    private TeacherVoiceDraft Seed(DateTimeOffset recordedAt)
    {
        var draft = TeacherVoiceDraft.Record(Guid.NewGuid(), Guid.NewGuid(), AudioKey, "/api/media/teacher-threads/voice.webm", 42, recordedAt);
        _drafts.Add(draft);
        return draft;
    }

    private Task Handle(Guid draftId) => _handler.Handle(new TranscribeVoiceDraftCommand(draftId), TestContext.Current.CancellationToken);

    private async Task ShouldNotTranscribe()
    {
        await _transcriptionClient.DidNotReceive().TranscribeAsync(Arg.Any<AiTranscriptionRequest>(), Arg.Any<CancellationToken>());
        await _teacherVoiceDraftRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
