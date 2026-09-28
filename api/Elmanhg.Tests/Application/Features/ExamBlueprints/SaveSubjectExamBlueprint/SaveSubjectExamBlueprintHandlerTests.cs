using Core.Errors;
using Core.Identity.Tokens.CurrentUser;
using Elmanhg.Application.ExamBlueprints.SaveSubjectExamBlueprint;
using Elmanhg.Application.ExamBlueprints.Shared;
using Elmanhg.Application.Exceptions;
using Elmanhg.Domain.ExamBlueprints;
using Elmanhg.Domain.Questions;
using Elmanhg.Domain.Subjects;
using Elmanhg.Tests.Builders;
using FluentAssertions;
using NSubstitute;
using System.Linq.Expressions;
using DomainErrorCodes = Elmanhg.Domain.SharedKernel.Exceptions.ErrorCodes;

namespace Elmanhg.Tests.Application.Features.ExamBlueprints.SaveSubjectExamBlueprint;

public sealed class SaveSubjectExamBlueprintHandlerTests
{
    private readonly ISubjectRepository _subjectRepository = Substitute.For<ISubjectRepository>();
    private readonly IExamBlueprintRepository _examBlueprintRepository = Substitute.For<IExamBlueprintRepository>();
    private readonly IQuestionRepository _questionRepository = Substitute.For<IQuestionRepository>();
    private readonly ICurrentUserService _currentUserService = Substitute.For<ICurrentUserService>();
    private readonly Guid _currentUserId = Guid.NewGuid();
    private readonly ExamBlueprintBuilder _builder = new();
    private readonly SaveSubjectExamBlueprintHandler _handler;

    public SaveSubjectExamBlueprintHandlerTests()
    {
        _currentUserService.UserId.Returns(_currentUserId);
        _subjectRepository.GetByIdAsync(_builder.Subject.Id, Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<Subject>, IQueryable<Subject>>?>(), Arg.Any<bool>()).Returns(_builder.Subject);
        ReturnServable(new ServableQuestionCount(_builder.Unit.Id, QuestionType.Mcq, 5));
        _handler = new SaveSubjectExamBlueprintHandler(_subjectRepository, _examBlueprintRepository, _questionRepository, _currentUserService);
    }

    [Fact]
    public async Task Handle_NoDefault_CreatesAndSaves()
    {
        var result = await _handler.Handle(Command(3), TestContext.Current.CancellationToken);

        await _examBlueprintRepository.Received(1).AddAsync(Arg.Is<ExamBlueprint>(x => x.UnitId == null && x.SubjectId == _builder.Subject.Id && x.CreatedBy == _currentUserId), Arg.Any<CancellationToken>());
        result.SubjectId.Should().Be(_builder.Subject.Id);
        result.UnitId.Should().BeNull();
        result.TypeCounts.Should().Equal(new ExamTypeCountResult(QuestionType.Mcq, 3));
        result.QuestionCount.Should().Be(3);
        result.TimeLimitMinutes.Should().Be(45);
        result.PassMark.Should().Be(60);
        await _examBlueprintRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ExistingDefault_UpdatesInPlace()
    {
        var existing = _builder.BuildDefault();
        ReturnExisting(existing);

        var result = await _handler.Handle(Command(4), TestContext.Current.CancellationToken);

        existing.QuestionCount.Should().Be(4);
        existing.UpdatedBy.Should().Be(_currentUserId);
        result.Id.Should().Be(existing.Id);
        await _examBlueprintRepository.DidNotReceive().AddAsync(Arg.Any<ExamBlueprint>(), Arg.Any<CancellationToken>());
        await _examBlueprintRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_CountsAcrossUnits_UsesSubjectPool()
    {
        ReturnServable(new ServableQuestionCount(_builder.Unit.Id, QuestionType.Mcq, 1), new ServableQuestionCount(Guid.NewGuid(), QuestionType.Mcq, 1));

        var result = await _handler.Handle(Command(2), TestContext.Current.CancellationToken);

        result.QuestionCount.Should().Be(2);
        await _examBlueprintRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_Shortfall_ThrowsExamBlueprintShortfall()
    {
        var act = () => _handler.Handle(Command(6), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<BusinessRuleViolationCoreException>()).Which.ErrorCode.Should().Be(DomainErrorCodes.ExamBlueprintShortfall);
        await _examBlueprintRepository.DidNotReceive().AddAsync(Arg.Any<ExamBlueprint>(), Arg.Any<CancellationToken>());
        await _examBlueprintRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_SubjectNotFound_ThrowsSubjectNotFound()
    {
        var act = () => _handler.Handle(new SaveSubjectExamBlueprintCommand(Guid.NewGuid(), Input(1)), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<NotFoundCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.SubjectNotFound);
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

    private SaveSubjectExamBlueprintCommand Command(int mcq) => new(_builder.Subject.Id, Input(mcq));

    private static ExamBlueprintInput Input(int mcq) => new([new ExamTypeCountInput(QuestionType.Mcq, mcq), new ExamTypeCountInput(QuestionType.Fill, 0)], null, 45, 60);

    private void ReturnServable(params ServableQuestionCount[] counts)
    {
        _questionRepository.CountServableByUnitAndTypeAsync(_builder.Subject.Id, Arg.Any<CancellationToken>()).Returns(counts.ToList());
    }

    private void ReturnExisting(ExamBlueprint blueprint)
    {
        _examBlueprintRepository.FirstOrDefaultAsync(Arg.Any<Expression<Func<ExamBlueprint, bool>>>(), Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<ExamBlueprint>, IQueryable<ExamBlueprint>>?>(), Arg.Any<Func<IQueryable<ExamBlueprint>, IOrderedQueryable<ExamBlueprint>>?>(), Arg.Any<bool>()).Returns(blueprint);
    }
}
