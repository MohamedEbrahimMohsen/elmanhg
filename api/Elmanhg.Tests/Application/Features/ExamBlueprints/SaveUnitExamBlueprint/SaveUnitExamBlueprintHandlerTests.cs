using Core.Errors;
using Core.Identity.Tokens.CurrentUser;
using Elmanhg.Application.ExamBlueprints.SaveUnitExamBlueprint;
using Elmanhg.Application.ExamBlueprints.Shared;
using Elmanhg.Application.Exceptions;
using Elmanhg.Domain.ExamBlueprints;
using Elmanhg.Domain.Questions;
using Elmanhg.Domain.Units;
using Elmanhg.Tests.Builders;
using FluentAssertions;
using NSubstitute;
using System.Linq.Expressions;
using DomainErrorCodes = Elmanhg.Domain.SharedKernel.Exceptions.ErrorCodes;

namespace Elmanhg.Tests.Application.Features.ExamBlueprints.SaveUnitExamBlueprint;

public sealed class SaveUnitExamBlueprintHandlerTests
{
    private readonly ICurriculumUnitRepository _unitRepository = Substitute.For<ICurriculumUnitRepository>();
    private readonly IExamBlueprintRepository _examBlueprintRepository = Substitute.For<IExamBlueprintRepository>();
    private readonly IQuestionRepository _questionRepository = Substitute.For<IQuestionRepository>();
    private readonly ICurrentUserService _currentUserService = Substitute.For<ICurrentUserService>();
    private readonly Guid _currentUserId = Guid.NewGuid();
    private readonly ExamBlueprintBuilder _builder = new();
    private readonly SaveUnitExamBlueprintHandler _handler;

    public SaveUnitExamBlueprintHandlerTests()
    {
        _currentUserService.UserId.Returns(_currentUserId);
        _unitRepository.GetByIdAsync(_builder.Unit.Id, Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<CurriculumUnit>, IQueryable<CurriculumUnit>>?>(), Arg.Any<bool>()).Returns(_builder.Unit);
        ReturnServable(new ServableQuestionCount(_builder.Unit.Id, QuestionType.Mcq, 5));
        _handler = new SaveUnitExamBlueprintHandler(_unitRepository, _examBlueprintRepository, _questionRepository, _currentUserService);
    }

    [Fact]
    public async Task Handle_NoBlueprint_CreatesForUnit()
    {
        var result = await _handler.Handle(Command(2), TestContext.Current.CancellationToken);

        await _examBlueprintRepository.Received(1).AddAsync(Arg.Is<ExamBlueprint>(x => x.UnitId == _builder.Unit.Id && x.SubjectId == _builder.Unit.SubjectId), Arg.Any<CancellationToken>());
        result.UnitId.Should().Be(_builder.Unit.Id);
        result.SubjectId.Should().Be(_builder.Unit.SubjectId);
        await _examBlueprintRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ExistingBlueprint_UpdatesInPlace()
    {
        var existing = _builder.BuildForUnit();
        _examBlueprintRepository.FirstOrDefaultAsync(Arg.Any<Expression<Func<ExamBlueprint, bool>>>(), Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<ExamBlueprint>, IQueryable<ExamBlueprint>>?>(), Arg.Any<Func<IQueryable<ExamBlueprint>, IOrderedQueryable<ExamBlueprint>>?>(), Arg.Any<bool>()).Returns(existing);

        var result = await _handler.Handle(Command(4), TestContext.Current.CancellationToken);

        result.Id.Should().Be(existing.Id);
        existing.QuestionCount.Should().Be(4);
        await _examBlueprintRepository.DidNotReceive().AddAsync(Arg.Any<ExamBlueprint>(), Arg.Any<CancellationToken>());
        await _examBlueprintRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_OtherUnitsQuestions_DoNotCount()
    {
        ReturnServable(new ServableQuestionCount(Guid.NewGuid(), QuestionType.Mcq, 5), new ServableQuestionCount(_builder.Unit.Id, QuestionType.Mcq, 1));

        var act = () => _handler.Handle(Command(2), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<BusinessRuleViolationCoreException>()).Which.ErrorCode.Should().Be(DomainErrorCodes.ExamBlueprintShortfall);
        await _examBlueprintRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_UnitNotFound_ThrowsUnitNotFound()
    {
        var act = () => _handler.Handle(new SaveUnitExamBlueprintCommand(Guid.NewGuid(), Input(1)), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<NotFoundCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.UnitNotFound);
        await _examBlueprintRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_NoCurrentUser_ThrowsUserNotAuthenticated()
    {
        _currentUserService.UserId.Returns((Guid?)null);

        var act = () => _handler.Handle(Command(1), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<UnauthorizedCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.UserNotAuthenticated);
        await _examBlueprintRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    private SaveUnitExamBlueprintCommand Command(int mcq) => new(_builder.Unit.Id, Input(mcq));

    private static ExamBlueprintInput Input(int mcq) => new([new ExamTypeCountInput(QuestionType.Mcq, mcq)], null, 30, 50);

    private void ReturnServable(params ServableQuestionCount[] counts)
    {
        _questionRepository.CountServableByUnitAndTypeAsync(_builder.Unit.SubjectId, Arg.Any<CancellationToken>()).Returns(counts.ToList());
    }
}
