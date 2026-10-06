using Elmanhg.Api.FileStorage;
using FluentAssertions;

namespace Elmanhg.Tests.Api.FileStorage;

public sealed class MediaStorageExtensionsTests
{
    [Fact]
    public void PrivateFolders_ListsTeacherThreadsAndTrainingExports()
    {
        MediaStorageExtensions.PrivateFolders.Should().Equal("teacher-threads", "training-exports");
    }
}
