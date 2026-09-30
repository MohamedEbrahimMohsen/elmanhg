using Elmanhg.Application.EssayGrading.Shared;
using Elmanhg.Domain.Lessons;
using Elmanhg.Domain.Subjects;
using Elmanhg.Domain.Units;
using Elmanhg.Tests.Builders;
using FluentAssertions;
using NSubstitute;
using System.Linq.Expressions;

namespace Elmanhg.Tests.Application.Features.EssayGrading.Shared;

public sealed class EssayGradingContextLoaderTests
{
    private readonly ILessonRepository _lessonRepository = Substitute.For<ILessonRepository>();
    private readonly ICurriculumUnitRepository _unitRepository = Substitute.For<ICurriculumUnitRepository>();
    private readonly ISubjectRepository _subjectRepository = Substitute.For<ISubjectRepository>();
    private readonly QuestionBuilder _questions = new();

    public EssayGradingContextLoaderTests()
    {
        _lessonRepository.FirstOrDefaultAsync(Arg.Any<Expression<Func<Lesson, bool>>>(), Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<Lesson>, IQueryable<Lesson>>?>(), Arg.Any<Func<IQueryable<Lesson>, IOrderedQueryable<Lesson>>?>(), Arg.Any<bool>())
            .Returns(call => new[] { _questions.Lesson }.FirstOrDefault(call.Arg<Expression<Func<Lesson, bool>>>().Compile()));
        _unitRepository.FirstOrDefaultAsync(Arg.Any<Expression<Func<CurriculumUnit, bool>>>(), Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<CurriculumUnit>, IQueryable<CurriculumUnit>>?>(), Arg.Any<Func<IQueryable<CurriculumUnit>, IOrderedQueryable<CurriculumUnit>>?>(), Arg.Any<bool>())
            .Returns(call => new[] { _questions.Unit }.FirstOrDefault(call.Arg<Expression<Func<CurriculumUnit, bool>>>().Compile()));
        _subjectRepository.FirstOrDefaultAsync(Arg.Any<Expression<Func<Subject, bool>>>(), Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<Subject>, IQueryable<Subject>>?>(), Arg.Any<Func<IQueryable<Subject>, IOrderedQueryable<Subject>>?>(), Arg.Any<bool>())
            .Returns(call => new[] { _questions.Subject }.FirstOrDefault(call.Arg<Expression<Func<Subject, bool>>>().Compile()));
    }

    [Fact]
    public async Task LoadAsync_FullTree_ReturnsSubjectNameAndOrderedObjectives()
    {
        _questions.Lesson.Objectives.Clear();
        _questions.Lesson.Objectives.Add(LessonObjective.Create(_questions.Lesson.Id, "Second", 2, Guid.NewGuid()));
        _questions.Lesson.Objectives.Add(LessonObjective.Create(_questions.Lesson.Id, "First", 1, Guid.NewGuid()));

        var context = await Load(_questions.Lesson.Id);

        context!.SubjectName.Should().Be("Physics");
        context.Objectives.Should().Equal("First", "Second");
    }

    [Fact]
    public async Task LoadAsync_MissingLesson_ReturnsNull()
    {
        var context = await Load(Guid.NewGuid());

        context.Should().BeNull();
    }

    private Task<EssayGradingContext?> Load(Guid lessonId) => EssayGradingContextLoader.LoadAsync(lessonId, _lessonRepository, _unitRepository, _subjectRepository, TestContext.Current.CancellationToken);
}
