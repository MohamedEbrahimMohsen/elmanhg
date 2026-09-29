using Elmanhg.Application.ContentRetrieval.RebuildContentIndex;
using Elmanhg.Domain.ContentRetrieval;
using FluentAssertions;
using NSubstitute;

namespace Elmanhg.Tests.Application.Features.ContentRetrieval.RebuildContentIndex;

public sealed class RebuildContentIndexHandlerTests
{
    private readonly ILessonContentIndexRepository _indexRepository = Substitute.For<ILessonContentIndexRepository>();

    [Fact]
    public async Task Handle_DeletesAllIndexState()
    {
        await new RebuildContentIndexHandler(_indexRepository).Handle(new RebuildContentIndexCommand(), TestContext.Current.CancellationToken);

        await _indexRepository.Received(1).DeleteAllAsync(Arg.Any<CancellationToken>());
        await _indexRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public void Command_AuditMetadata_IsContentIndexRebuild()
    {
        var command = new RebuildContentIndexCommand();

        command.AuditAction.Should().Be("ContentIndex.Rebuild");
        command.AuditResourceType.Should().Be("ContentIndex");
        command.AuditResourceId.Should().BeNull();
    }
}
