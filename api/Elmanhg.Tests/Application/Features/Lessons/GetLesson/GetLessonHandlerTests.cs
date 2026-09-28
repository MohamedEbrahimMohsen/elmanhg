using Core.Errors;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Lessons.GetLesson;
using Elmanhg.Application.Lessons.Shared;
using Elmanhg.Domain.Lessons;
using Elmanhg.Domain.Subjects;
using Elmanhg.Domain.Units;
using FluentAssertions;
using NSubstitute;

namespace Elmanhg.Tests.Application.Features.Lessons.GetLesson;

public sealed class GetLessonHandlerTests
{
    private readonly ILessonRepository _lessonRepository = Substitute.For<ILessonRepository>();
    private readonly GetLessonHandler _handler;

    public GetLessonHandlerTests()
    {
        _handler = new GetLessonHandler(_lessonRepository);
    }

    [Fact]
    public async Task Handle_ExistingLesson_ReturnsDetailWithOrderedObjectives()
    {
        var unit = CurriculumUnit.Create(Subject.Create("Physics", 1, Guid.NewGuid()), "Mechanics", 1, Guid.NewGuid());
        var lesson = Lesson.Create(unit, "Newton's laws", 2, Guid.NewGuid());
        lesson.Update("Newton's laws", "<p>Force</p>", "<p>Summary</p>", "https://example.com/video", [new LessonObjectiveContent(null, "First"), new LessonObjectiveContent(null, "Second")], Guid.NewGuid());
        _lessonRepository.GetWithObjectivesAsync(lesson.Id, true, Arg.Any<CancellationToken>()).Returns(lesson);

        var result = await _handler.Handle(new GetLessonQuery(lesson.Id), TestContext.Current.CancellationToken);

        result.Id.Should().Be(lesson.Id);
        result.UnitId.Should().Be(unit.Id);
        result.Name.Should().Be("Newton's laws");
        result.Order.Should().Be(2);
        result.State.Should().Be("Draft");
        result.Explanation.Should().Be("<p>Force</p>");
        result.Summary.Should().Be("<p>Summary</p>");
        result.VideoUrl.Should().Be("https://example.com/video");
        result.Objectives.Should().Equal(new LessonObjectiveResult(lesson.Objectives[0].Id, "First", 1), new LessonObjectiveResult(lesson.Objectives[1].Id, "Second", 2));
    }

    [Fact]
    public async Task Handle_LessonNotFound_ThrowsLessonNotFound()
    {
        var act = () => _handler.Handle(new GetLessonQuery(Guid.NewGuid()), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<NotFoundCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.LessonNotFound);
    }
}
