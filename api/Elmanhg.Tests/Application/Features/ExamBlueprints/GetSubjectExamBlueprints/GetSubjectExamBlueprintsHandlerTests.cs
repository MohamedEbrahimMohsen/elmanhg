using Core.Errors;
using Elmanhg.Application.ExamBlueprints.GetSubjectExamBlueprints;
using Elmanhg.Application.Exceptions;
using Elmanhg.Domain.ExamBlueprints;
using Elmanhg.Domain.Questions;
using Elmanhg.Domain.Subjects;
using Elmanhg.Domain.Units;
using Elmanhg.Tests.Builders;
using FluentAssertions;
using NSubstitute;
using System.Linq.Expressions;

namespace Elmanhg.Tests.Application.Features.ExamBlueprints.GetSubjectExamBlueprints;

public sealed class GetSubjectExamBlueprintsHandlerTests
{
    private readonly ISubjectRepository _subjectRepository = Substitute.For<ISubjectRepository>();
    private readonly ICurriculumUnitRepository _unitRepository = Substitute.For<ICurriculumUnitRepository>();
    private readonly IExamBlueprintRepository _examBlueprintRepository = Substitute.For<IExamBlueprintRepository>();
    private readonly IQuestionRepository _questionRepository = Substitute.For<IQuestionRepository>();
    private readonly ExamBlueprintBuilder _builder = new();
    private readonly CurriculumUnit _unitB;
    private readonly GetSubjectExamBlueprintsHandler _handler;

    public GetSubjectExamBlueprintsHandlerTests()
    {
        _unitB = CurriculumUnit.Create(_builder.Subject, "Waves", 2, Guid.NewGuid());
        _subjectRepository.GetByIdAsync(_builder.Subject.Id, Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<Subject>, IQueryable<Subject>>?>(), Arg.Any<bool>()).Returns(_builder.Subject);
        _unitRepository.FindAsync(Arg.Any<Expression<Func<CurriculumUnit, bool>>>(), Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<CurriculumUnit>, IQueryable<CurriculumUnit>>?>(), Arg.Any<Func<IQueryable<CurriculumUnit>, IOrderedQueryable<CurriculumUnit>>?>(), Arg.Any<bool>()).Returns([_builder.Unit, _unitB]);
        _questionRepository.CountServableByUnitAndTypeAsync(_builder.Subject.Id, Arg.Any<CancellationToken>()).Returns([new ServableQuestionCount(_builder.Unit.Id, QuestionType.Mcq, 2), new ServableQuestionCount(_unitB.Id, QuestionType.Mcq, 1)]);
        _handler = new GetSubjectExamBlueprintsHandler(_subjectRepository, _unitRepository, _examBlueprintRepository, _questionRepository);
    }

    [Fact]
    public async Task Handle_Subject_ReturnsDefaultUnitsAndServableCounts()
    {
        var defaultBlueprint = _builder.BuildDefault();
        var unitBlueprint = _builder.BuildForUnit();
        ReturnBlueprints(defaultBlueprint, unitBlueprint);

        var result = await _handler.Handle(new GetSubjectExamBlueprintsQuery(_builder.Subject.Id), TestContext.Current.CancellationToken);

        result.DefaultBlueprint!.Id.Should().Be(defaultBlueprint.Id);
        result.Units.Select(x => x.UnitId).Should().Equal(_builder.Unit.Id, _unitB.Id);
        result.Units[0].Blueprint!.Id.Should().Be(unitBlueprint.Id);
        result.Units[1].Blueprint.Should().BeNull();
        result.Units[0].Servable.Should().HaveCount(7);
        result.Units[0].Servable.Single(x => x.Type == QuestionType.Mcq).Count.Should().Be(2);
        result.Units[0].Servable.Where(x => x.Type != QuestionType.Mcq).Should().OnlyContain(x => x.Count == 0);
        result.Servable.Single(x => x.Type == QuestionType.Mcq).Count.Should().Be(3);
        result.Servable.Count.Should().Be(7);
    }

    [Fact]
    public async Task Handle_BlueprintOfUnknownUnit_IsIgnored()
    {
        var otherUnit = CurriculumUnit.Create(_builder.Subject, "Optics", 3, Guid.NewGuid());
        ReturnBlueprints(ExamBlueprint.CreateForUnit(otherUnit, ExamBlueprintBuilder.Shape(new ExamTypeCount(QuestionType.Mcq, 1)), ExamBlueprintBuilder.Plenty(), Guid.NewGuid()));

        var result = await _handler.Handle(new GetSubjectExamBlueprintsQuery(_builder.Subject.Id), TestContext.Current.CancellationToken);

        result.Units.Should().OnlyContain(x => x.Blueprint == null);
        result.DefaultBlueprint.Should().BeNull();
    }

    [Fact]
    public async Task Handle_SubjectNotFound_ThrowsSubjectNotFound()
    {
        var act = () => _handler.Handle(new GetSubjectExamBlueprintsQuery(Guid.NewGuid()), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<NotFoundCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.SubjectNotFound);
    }

    private void ReturnBlueprints(params ExamBlueprint[] blueprints)
    {
        _examBlueprintRepository.FindAsync(Arg.Any<Expression<Func<ExamBlueprint, bool>>>(), Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<ExamBlueprint>, IQueryable<ExamBlueprint>>?>(), Arg.Any<Func<IQueryable<ExamBlueprint>, IOrderedQueryable<ExamBlueprint>>?>(), Arg.Any<bool>()).Returns(blueprints.ToList());
    }
}
