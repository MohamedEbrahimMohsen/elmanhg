using Core.Errors;
using Core.Identity.Tokens.CurrentUser;
using Core.Storage;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.TeacherInbox.RecordVoiceDraft;
using Elmanhg.Application.TeacherInbox.Shared;
using Elmanhg.Domain.Identity;
using Elmanhg.Domain.TeacherThreads;
using Elmanhg.Domain.Teachers;
using Elmanhg.Tests.Builders;
using FluentAssertions;
using NSubstitute;
using System.Linq.Expressions;
using System.Security.Claims;
using System.Text.RegularExpressions;
using DomainErrorCodes = Elmanhg.Domain.SharedKernel.Exceptions.ErrorCodes;

namespace Elmanhg.Tests.Application.Features.TeacherInbox.RecordVoiceDraft;

public sealed class RecordVoiceDraftHandlerTests
{
    private const string StoredUrl = "/api/media/teacher-threads/stored.webm";
    private static readonly DateTimeOffset Now = TeacherThreadBuilder.DefaultSubmittedAt.AddHours(2);
    private readonly ITeacherThreadRepository _teacherThreadRepository = Substitute.For<ITeacherThreadRepository>();
    private readonly ITeacherVoiceDraftRepository _teacherVoiceDraftRepository = Substitute.For<ITeacherVoiceDraftRepository>();
    private readonly ITeacherSubjectRepository _teacherSubjectRepository = Substitute.For<ITeacherSubjectRepository>();
    private readonly IFileStorage _fileStorage = Substitute.For<IFileStorage>();
    private readonly TimeProvider _timeProvider = Substitute.For<TimeProvider>();
    private readonly ICurrentUserService _currentUserService = Substitute.For<ICurrentUserService>();
    private readonly Guid _teacherId = Guid.NewGuid();
    private readonly List<TeacherThread> _threads = [];
    private readonly RecordVoiceDraftHandler _handler;

    public RecordVoiceDraftHandlerTests()
    {
        _currentUserService.UserId.Returns(_teacherId);
        _currentUserService.GetClaim(ClaimTypes.Role).Returns(nameof(UserRole.Teacher));
        _timeProvider.GetUtcNow().Returns(Now);
        _teacherSubjectRepository.IsAssignedAsync(_teacherId, Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(true);
        _fileStorage.SaveAsync(Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(StoredUrl);
        _teacherThreadRepository.FirstOrDefaultAsync(Arg.Any<Expression<Func<TeacherThread, bool>>>(), Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<TeacherThread>, IQueryable<TeacherThread>>?>(), Arg.Any<Func<IQueryable<TeacherThread>, IOrderedQueryable<TeacherThread>>?>(), Arg.Any<bool>())
            .Returns(call => _threads.FirstOrDefault(call.Arg<Expression<Func<TeacherThread, bool>>>().Compile()));
        _handler = new RecordVoiceDraftHandler(_teacherThreadRepository, _teacherVoiceDraftRepository, _teacherSubjectRepository, _fileStorage, _timeProvider, _currentUserService);
    }

    [Fact]
    public async Task Handle_NoCurrentUser_ThrowsUnauthorized()
    {
        _currentUserService.UserId.Returns((Guid?)null);

        await ShouldThrow<UnauthorizedCoreException>(Seed(new TeacherThreadBuilder().ClaimedBy(_teacherId)).Id, ErrorCodes.UserNotAuthenticated);
    }

    [Fact]
    public async Task Handle_UnknownThread_ThrowsNotFound()
    {
        await ShouldThrow<NotFoundCoreException>(Guid.NewGuid(), ErrorCodes.TeacherThreadNotFound);
    }

    [Fact]
    public async Task Handle_TeacherOutsideSubject_ThrowsForbidden()
    {
        var thread = Seed(new TeacherThreadBuilder().ClaimedBy(_teacherId));
        _teacherSubjectRepository.IsAssignedAsync(_teacherId, thread.SubjectId, Arg.Any<CancellationToken>()).Returns(false);

        await ShouldThrow<ForbiddenCoreException>(thread.Id, ErrorCodes.SubjectOutOfScope);
    }

    [Fact]
    public async Task Handle_ThreadClaimedByOther_ThrowsAlreadyClaimed()
    {
        await ShouldThrow<ConflictCoreException>(Seed(new TeacherThreadBuilder().ClaimedBy(Guid.NewGuid())).Id, DomainErrorCodes.TeacherThreadAlreadyClaimed);
    }

    [Fact]
    public async Task Handle_ThreadAnswered_ThrowsNotAwaitingReply()
    {
        await ShouldThrow<ConflictCoreException>(Seed(new TeacherThreadBuilder().AnsweredBy(_teacherId)).Id, DomainErrorCodes.TeacherThreadNotAwaitingReply);
    }

    [Fact]
    public async Task Handle_ClaimerOnOpenThread_StoresAudioAndCreatesPendingDraft()
    {
        var thread = Seed(new TeacherThreadBuilder().ClaimedBy(_teacherId));

        var result = await Handle(thread.Id);

        await _fileStorage.Received(1).SaveAsync(Arg.Any<Stream>(), Arg.Is<string>(key => Regex.IsMatch(key, "^teacher-threads/[0-9a-f]{32}\\.webm$")), Arg.Any<CancellationToken>());
        await _teacherVoiceDraftRepository.Received(1).AddAsync(Arg.Is<TeacherVoiceDraft>(x => x.Status == TeacherVoiceDraftStatus.Pending && x.AudioUrl == StoredUrl && x.AudioDurationSeconds == 12 && x.ThreadId == thread.Id && x.TeacherId == _teacherId), Arg.Any<CancellationToken>());
        await _teacherVoiceDraftRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        (result.Status, result.ThreadId, result.AudioDurationSeconds, result.RecordedAt).Should().Be((TeacherVoiceDraftStatus.Pending, thread.Id, 12, Now));
    }

    [Fact]
    public async Task Handle_AdminClaimer_SkipsSubjectAssignment()
    {
        _currentUserService.GetClaim(ClaimTypes.Role).Returns(nameof(UserRole.Admin));
        var thread = Seed(new TeacherThreadBuilder().ClaimedBy(_teacherId));

        var result = await Handle(thread.Id);

        await _teacherSubjectRepository.DidNotReceive().IsAssignedAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        result.Status.Should().Be(TeacherVoiceDraftStatus.Pending);
        await _teacherVoiceDraftRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    private TeacherThread Seed(TeacherThreadBuilder builder)
    {
        var thread = builder.Build();
        _threads.Add(thread);
        return thread;
    }

    private Task<TeacherVoiceDraftResult> Handle(Guid threadId) => _handler.Handle(new RecordVoiceDraftCommand(threadId, TeacherVoiceSignatures.FormFile(TeacherVoiceSignatures.Webm), 12), TestContext.Current.CancellationToken);

    private async Task ShouldThrow<TException>(Guid threadId, string errorCode) where TException : BaseException
    {
        var act = () => Handle(threadId);

        (await act.Should().ThrowAsync<TException>()).Which.ErrorCode.Should().Be(errorCode);
        await _fileStorage.DidNotReceive().SaveAsync(Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _teacherVoiceDraftRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
