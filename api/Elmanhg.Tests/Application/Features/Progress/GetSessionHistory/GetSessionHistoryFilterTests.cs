using Elmanhg.Application.Progress.GetSessionHistory;
using Elmanhg.Domain.Sessions;
using Elmanhg.Tests.Builders;
using FluentAssertions;

namespace Elmanhg.Tests.Application.Features.Progress.GetSessionHistory;

public sealed class GetSessionHistoryFilterTests
{
    private readonly SessionBuilder _builder = new();

    [Fact]
    public void Build_NoKind_MatchesOwnNonTestQuiz()
    {
        var matches = GetSessionHistoryFilter.Build(_builder.StudentId, null).Compile();

        matches(_builder.Build()).Should().BeTrue();
    }

    [Fact]
    public void Build_KindQuiz_MatchesQuiz()
    {
        var matches = GetSessionHistoryFilter.Build(_builder.StudentId, SessionHistoryKind.Quiz).Compile();

        matches(_builder.Build()).Should().BeTrue();
    }

    [Fact]
    public void Build_KindExam_DoesNotMatchQuiz()
    {
        var matches = GetSessionHistoryFilter.Build(_builder.StudentId, SessionHistoryKind.Exam).Compile();

        matches(_builder.Build()).Should().BeFalse();
    }

    [Fact]
    public void Build_OtherStudent_DoesNotMatch()
    {
        var matches = GetSessionHistoryFilter.Build(Guid.NewGuid(), null).Compile();

        matches(_builder.Build()).Should().BeFalse();
    }

    [Fact]
    public void Build_TestModeSession_DoesNotMatch()
    {
        var matches = GetSessionHistoryFilter.Build(_builder.StudentId, null).Compile();

        matches(_builder.Build(isTestMode: true)).Should().BeFalse();
    }
}
