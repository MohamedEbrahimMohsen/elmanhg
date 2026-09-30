using Core.Errors;
using Core.Identity.Tokens.CurrentUser;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.TrainingExports.RequestTrainingExport;
using Elmanhg.Domain.Subjects;
using Elmanhg.Domain.TrainingExports;
using FluentAssertions;
using NSubstitute;

namespace Elmanhg.Tests.Application.Features.TrainingExports.RequestTrainingExport;

public sealed class RequestTrainingExportHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 2, 3, 10, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset From = new(2026, 1, 1, 0, 0, 0, TimeSpan.FromHours(2));
    private readonly ITrainingExportRepository _trainingExportRepository = Substitute.For<ITrainingExportRepository>();
    private readonly ISubjectRepository _subjectRepository = Substitute.For<ISubjectRepository>();
    private readonly ICurrentUserService _currentUserService = Substitute.For<ICurrentUserService>();
    private readonly TimeProvider _timeProvider = Substitute.For<TimeProvider>();
    private readonly Subject _subject = Subject.Create("Physics", 1, Guid.NewGuid());
    private readonly Guid _adminId = Guid.NewGuid();
    private readonly RequestTrainingExportHandler _handler;
    private TrainingExport? _added;

    public RequestTrainingExportHandlerTests()
    {
        _timeProvider.GetUtcNow().Returns(Now);
        _currentUserService.UserId.Returns(_adminId);
        _subjectRepository.GetByIdAsync(_subject.Id, Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<Subject>, IQueryable<Subject>>?>(), Arg.Any<bool>()).Returns(_subject);
        _trainingExportRepository.AddAsync(Arg.Do<TrainingExport>(x => _added = x), Arg.Any<CancellationToken>());
        _handler = new RequestTrainingExportHandler(_trainingExportRepository, _subjectRepository, _currentUserService, _timeProvider);
    }

    [Fact]
    public async Task Handle_ValidRequest_AddsPendingExportAndSaves()
    {
        var result = await _handler.Handle(new RequestTrainingExportCommand(TrainingExportSource.Avatar, From, From.AddDays(31), _subject.Id), TestContext.Current.CancellationToken);

        (result.Status, result.Source, result.SubjectId, result.RequestedAt, result.Attempts).Should().Be((TrainingExportStatus.Pending, TrainingExportSource.Avatar, (Guid?)_subject.Id, Now, 0));
        (result.From, result.From.Offset, result.To).Should().Be((From, TimeSpan.Zero, From.AddDays(31)));
        result.FileName.Should().Be("elmanhg-avatar-2025-12-31-2026-01-31.jsonl");
        _added.Should().NotBeNull();
        (_added!.Id, _added.CreatedBy).Should().Be((result.Id, (Guid?)_adminId));
        await _trainingExportRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_NoCurrentUser_ThrowsUnauthorized()
    {
        _currentUserService.UserId.Returns((Guid?)null);

        var act = () => _handler.Handle(new RequestTrainingExportCommand(TrainingExportSource.Avatar, From, From.AddDays(1), null), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<UnauthorizedCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.UserNotAuthenticated);
        await _trainingExportRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_UnknownSubject_ThrowsNotFoundSubjectNotFound()
    {
        var act = () => _handler.Handle(new RequestTrainingExportCommand(TrainingExportSource.Avatar, From, From.AddDays(1), Guid.NewGuid()), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<NotFoundCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.SubjectNotFound);
        await _trainingExportRepository.DidNotReceive().AddAsync(Arg.Any<TrainingExport>(), Arg.Any<CancellationToken>());
        await _trainingExportRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
