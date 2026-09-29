using Elmanhg.Application.TeacherThreads.Shared;
using Elmanhg.Tests.Builders;
using FluentAssertions;

namespace Elmanhg.Tests.Application.Features.TeacherThreads.Shared;

public sealed class TeacherThreadResultGeneratorTests
{
    private readonly Guid _teacherId = Guid.NewGuid();

    [Fact]
    public void Generate_AnsweredThread_OffersFollowUpAndRating()
    {
        var thread = new TeacherThreadBuilder().AnsweredBy(_teacherId).Build();

        var result = TeacherThreadResultGenerator.Generate(thread, thread.SubmittedAt.AddHours(2));

        (result.CanFollowUp, result.CanRate, result.Rating, result.ClosedAt).Should().Be((true, true, (int?)null, (DateTimeOffset?)null));
    }

    [Fact]
    public void Generate_RatedThread_ReturnsRatingWithoutActions()
    {
        var thread = new TeacherThreadBuilder().AnsweredBy(_teacherId).Rated(3).Build();

        var result = TeacherThreadResultGenerator.Generate(thread, thread.SubmittedAt.AddHours(5));

        (result.Rating, result.ClosedAt, result.CanFollowUp, result.CanRate).Should().Be(((int?)3, thread.ClosedAt, false, false));
        result.ClosedAt.Should().NotBeNull();
    }
}
