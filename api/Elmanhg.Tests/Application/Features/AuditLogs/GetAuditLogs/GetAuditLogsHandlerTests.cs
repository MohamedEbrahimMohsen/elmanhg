using Core.Auditing.Entities;
using Core.Auditing.Repositories;
using Core.DDD.Models;
using Elmanhg.Application.AuditLogs.GetAuditLogs;
using Elmanhg.Tests.Builders;
using FluentAssertions;
using NSubstitute;
using System.Linq.Expressions;

namespace Elmanhg.Tests.Application.Features.AuditLogs.GetAuditLogs;

public sealed class GetAuditLogsHandlerTests
{
    private readonly IAuditLogRepository _auditLogRepository = Substitute.For<IAuditLogRepository>();

    [Fact]
    public async Task Handle_ReturnsPageMappedToResultsNewestFirstQuery()
    {
        var entry = new AuditLogBuilder().WithDiff("""[{"change":"Created"}]""").Build();
        _auditLogRepository.FindPaginatedAsync(2, 5, Arg.Any<CancellationToken>(), Arg.Any<Expression<Func<AuditLog, bool>>?>(), Arg.Any<Func<IQueryable<AuditLog>, IQueryable<AuditLog>>?>(), Arg.Any<Func<IQueryable<AuditLog>, IOrderedQueryable<AuditLog>>?>(), true)
            .Returns(new PageData<AuditLog> { Items = [entry], PageNumber = 2, PageSize = 5, TotalItems = 6, TotalPages = 2 });
        var handler = new GetAuditLogsHandler(_auditLogRepository);

        var page = await handler.Handle(new GetAuditLogsQuery(null, null, null, null, 2, 5), TestContext.Current.CancellationToken);

        await _auditLogRepository.Received(1).FindPaginatedAsync(2, 5, Arg.Any<CancellationToken>(), Arg.Any<Expression<Func<AuditLog, bool>>?>(), Arg.Any<Func<IQueryable<AuditLog>, IQueryable<AuditLog>>?>(), Arg.Any<Func<IQueryable<AuditLog>, IOrderedQueryable<AuditLog>>?>(), asNoTracking: true);
        var result = page.Items.Should().ContainSingle().Subject;
        result.Id.Should().Be(entry.Id);
        result.Timestamp.Should().Be(entry.Timestamp);
        result.ActorUserId.Should().Be(entry.ActorUserId);
        result.ActorUserName.Should().Be(entry.ActorUserName);
        result.ActorRole.Should().Be(entry.ActorRole);
        result.Action.Should().Be(entry.Action);
        result.ResourceType.Should().Be(entry.ResourceType);
        result.ResourceId.Should().Be(entry.ResourceId);
        result.Outcome.Should().Be(entry.Outcome);
        result.ErrorCode.Should().Be(entry.ErrorCode);
        result.TraceId.Should().Be(entry.TraceId);
        result.Diff.Should().Be(entry.Diff);
        page.PageNumber.Should().Be(2);
        page.PageSize.Should().Be(5);
        page.TotalItems.Should().Be(6);
        page.TotalPages.Should().Be(2);
    }
}
