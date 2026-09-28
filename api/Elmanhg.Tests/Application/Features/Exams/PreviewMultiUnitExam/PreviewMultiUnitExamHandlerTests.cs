using Core.Errors;
using Core.Identity.Tokens.CurrentUser;
using Elmanhg.Application.Exams.PreviewMultiUnitExam;
using Elmanhg.Application.Exams.Shared;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Shared.Options;
using Elmanhg.Domain.ExamBlueprints;
using Elmanhg.Domain.Questions;
using Elmanhg.Domain.Subjects;
using Elmanhg.Domain.Units;
using Elmanhg.Tests.Builders;
using FluentAssertions;
using Microsoft.Extensions.Options;
using NSubstitute;
using System.Linq.Expressions;

namespace Elmanhg.Tests.Application.Features.Exams.PreviewMultiUnitExam;

public sealed class PreviewMultiUnitExamHandlerTests
{
    private readonly ISubjectRepository _subjectRepository = Substitute.For<ISubjectRepository>();
    private readonly ICurriculumUnitRepository _unitRepository = Substitute.For<ICurriculumUnitRepository>();
    private readonly IExamBlueprintRepository _examBlueprintRepository = Substitute.For<IExamBlueprintRepository>();
    private readonly IQuestionRepository _questionRepository = Substitute.For<IQuestionRepository>();
    private readonly ICurrentUserService _currentUserService = Substitute.For<ICurrentUserService>();
    private readonly MultiUnitExamBuilder _builder = new();
    private readonly List<ExamBlueprint> _blueprints = [];
    private readonly List<ServableQuestionCount> _counts = [];
    private readonly PreviewMultiUnitExamHandler _handler;

    public PreviewMultiUnitExamHandlerTests()
    {
        _currentUserService.UserId.Returns(_builder.StudentId);
        _subjectRepository.GetByIdAsync(_builder.Subject.Id, Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<Subject>, IQueryable<Subject>>?>(), Arg.Any<bool>()).Returns(_builder.Subject);
        _unitRepository.FindAsync(Arg.Any<Expression<Func<CurriculumUnit, bool>>>(), Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<CurriculumUnit>, IQueryable<CurriculumUnit>>?>(), Arg.Any<Func<IQueryable<CurriculumUnit>, IOrderedQueryable<CurriculumUnit>>?>(), Arg.Any<bool>())
            .Returns(call => _builder.Units.Where(call.Arg<Expression<Func<CurriculumUnit, bool>>>().Compile()).ToList());
        _examBlueprintRepository.FindAsync(Arg.Any<Expression<Func<ExamBlueprint, bool>>>(), Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<ExamBlueprint>, IQueryable<ExamBlueprint>>?>(), Arg.Any<Func<IQueryable<ExamBlueprint>, IOrderedQueryable<ExamBlueprint>>?>(), Arg.Any<bool>())
            .Returns(call => _blueprints.Where(call.Arg<Expression<Func<ExamBlueprint, bool>>>().Compile()).ToList());
        _questionRepository.CountServableByUnitAndTypeAsync(_builder.Subject.Id, Arg.Any<CancellationToken>()).Returns(_ => _counts.ToList());
        _blueprints.Add(_builder.UnitBlueprint(0, 30, 50, Mcq(10)));
        _blueprints.Add(_builder.UnitBlueprint(1, 30, 50, Mcq(10)));
        _counts.AddRange([new ServableQuestionCount(_builder.Units[0].Id, QuestionType.Mcq, 12), new ServableQuestionCount(_builder.Units[1].Id, QuestionType.Mcq, 13), new ServableQuestionCount(Guid.NewGuid(), QuestionType.Mcq, 100)]);
        _handler = new PreviewMultiUnitExamHandler(_subjectRepository, _unitRepository, _examBlueprintRepository, _questionRepository, Options.Create(new ExamBlueprintsOptions()), _currentUserService);
    }

    [Fact]
    public async Task Handle_NoCurrentUser_ThrowsUserNotAuthenticated()
    {
        _currentUserService.UserId.Returns((Guid?)null);

        await AssertThrowsAsync<UnauthorizedCoreException>(Query(), ErrorCodes.UserNotAuthenticated);
    }

    [Fact]
    public async Task Handle_SubjectMissing_ThrowsSubjectNotFound()
    {
        await AssertThrowsAsync<NotFoundCoreException>(new PreviewMultiUnitExamQuery(new MultiUnitExamSelection(Guid.NewGuid(), UnitIds(), 20)), ErrorCodes.SubjectNotFound);
    }

    [Fact]
    public async Task Handle_UnitOfOtherSubject_ThrowsUnitNotFound()
    {
        var other = new MultiUnitExamBuilder();

        await AssertThrowsAsync<NotFoundCoreException>(new PreviewMultiUnitExamQuery(new MultiUnitExamSelection(_builder.Subject.Id, [_builder.Units[0].Id, other.Units[0].Id], 20)), ErrorCodes.UnitNotFound);
    }

    [Fact]
    public async Task Handle_UnitWithoutBlueprint_ThrowsNoBlueprintWithUnitNames()
    {
        _blueprints.RemoveAt(1);

        var act = () => _handler.Handle(Query(), TestContext.Current.CancellationToken);

        var exception = (await act.Should().ThrowAsync<BadRequestCoreException>()).Which;
        exception.ErrorCode.Should().Be(ErrorCodes.MultiUnitExamNoBlueprint);
        exception.Context.Should().ContainKey("units").WhoseValue.Should().Be("Waves");
    }

    [Fact]
    public async Task Handle_TwoUnits_ReturnsMergedCountsWithUnionAvailability()
    {
        var result = await _handler.Handle(Query(), TestContext.Current.CancellationToken);

        result.Blueprint.TypeCounts.Should().Equal(new ExamTypeAvailabilityResult(QuestionType.Mcq, 20, 25));
        (result.Blueprint.TimeLimitMinutes, result.Blueprint.PassMark, result.Blueprint.IsSubjectDefault, result.Size).Should().Be(((int?)60, 50, false, 20));
        result.Units.Select(x => (x.Name, x.QuestionCount)).Should().Equal(("Mechanics", 10), ("Waves", 10));
        result.IsAvailable.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_UnionShort_ReturnsNotAvailable()
    {
        _counts.Clear();
        _counts.AddRange([new ServableQuestionCount(_builder.Units[0].Id, QuestionType.Mcq, 5), new ServableQuestionCount(_builder.Units[1].Id, QuestionType.Mcq, 5)]);

        var result = await _handler.Handle(Query(), TestContext.Current.CancellationToken);

        result.IsAvailable.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_AllUnitsOnDefault_MarksSubjectDefault()
    {
        _blueprints.Clear();
        _blueprints.Add(_builder.DefaultBlueprint(30, 50, Mcq(10)));

        var result = await _handler.Handle(Query(), TestContext.Current.CancellationToken);

        result.Blueprint.IsSubjectDefault.Should().BeTrue();
        result.Units.Should().AllSatisfy(x => x.IsSubjectDefault.Should().BeTrue());
    }

    private static ExamTypeCount Mcq(int count) => new(QuestionType.Mcq, count);

    private List<Guid> UnitIds() => _builder.Units.Select(x => x.Id).ToList();

    private PreviewMultiUnitExamQuery Query() => new(new MultiUnitExamSelection(_builder.Subject.Id, UnitIds(), 20));

    private async Task AssertThrowsAsync<TException>(PreviewMultiUnitExamQuery query, string errorCode) where TException : BaseException
    {
        var act = () => _handler.Handle(query, TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<TException>()).Which.ErrorCode.Should().Be(errorCode);
    }
}
