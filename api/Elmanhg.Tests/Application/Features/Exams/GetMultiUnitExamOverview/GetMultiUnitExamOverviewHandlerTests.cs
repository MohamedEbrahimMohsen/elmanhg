using Core.Errors;
using Core.Identity.Tokens.CurrentUser;
using Elmanhg.Application.Exams.GetMultiUnitExamOverview;
using Elmanhg.Application.Exams.Shared;
using Elmanhg.Application.Exceptions;
using Elmanhg.Domain.ExamBlueprints;
using Elmanhg.Domain.Questions;
using Elmanhg.Domain.Sessions;
using Elmanhg.Domain.Subjects;
using Elmanhg.Domain.Units;
using Elmanhg.Tests.Application.Features.Sessions;
using Elmanhg.Tests.Builders;
using FluentAssertions;
using NSubstitute;
using System.Linq.Expressions;

namespace Elmanhg.Tests.Application.Features.Exams.GetMultiUnitExamOverview;

public sealed class GetMultiUnitExamOverviewHandlerTests
{
    private readonly ISubjectRepository _subjectRepository = Substitute.For<ISubjectRepository>();
    private readonly ICurriculumUnitRepository _unitRepository = Substitute.For<ICurriculumUnitRepository>();
    private readonly IExamBlueprintRepository _examBlueprintRepository = Substitute.For<IExamBlueprintRepository>();
    private readonly IQuestionRepository _questionRepository = Substitute.For<IQuestionRepository>();
    private readonly ISessionRepository _sessionRepository = Substitute.For<ISessionRepository>();
    private readonly ICurrentUserService _currentUserService = Substitute.For<ICurrentUserService>();
    private readonly MultiUnitExamBuilder _builder = new();
    private readonly List<ExamBlueprint> _blueprints = [];
    private readonly GetMultiUnitExamOverviewHandler _handler;

    public GetMultiUnitExamOverviewHandlerTests()
    {
        _currentUserService.UserId.Returns(_builder.StudentId);
        _subjectRepository.GetByIdAsync(_builder.Subject.Id, Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<Subject>, IQueryable<Subject>>?>(), Arg.Any<bool>()).Returns(_builder.Subject);
        List<CurriculumUnit> reversed = [_builder.Units[1], _builder.Units[0]];
        _unitRepository.FindAsync(Arg.Any<Expression<Func<CurriculumUnit, bool>>>(), Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<CurriculumUnit>, IQueryable<CurriculumUnit>>?>(), Arg.Any<Func<IQueryable<CurriculumUnit>, IOrderedQueryable<CurriculumUnit>>?>(), Arg.Any<bool>())
            .Returns(call => reversed.Where(call.Arg<Expression<Func<CurriculumUnit, bool>>>().Compile()).ToList());
        _examBlueprintRepository.FindAsync(Arg.Any<Expression<Func<ExamBlueprint, bool>>>(), Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<ExamBlueprint>, IQueryable<ExamBlueprint>>?>(), Arg.Any<Func<IQueryable<ExamBlueprint>, IOrderedQueryable<ExamBlueprint>>?>(), Arg.Any<bool>())
            .Returns(call => _blueprints.Where(call.Arg<Expression<Func<ExamBlueprint, bool>>>().Compile()).ToList());
        _questionRepository.CountServableByUnitAndTypeAsync(_builder.Subject.Id, Arg.Any<CancellationToken>())
            .Returns([new ServableQuestionCount(_builder.Units[0].Id, QuestionType.Mcq, 5), new ServableQuestionCount(_builder.Units[0].Id, QuestionType.Fill, 7), new ServableQuestionCount(_builder.Units[1].Id, QuestionType.Mcq, 3)]);
        SessionRepositoryStub.StubFind(_sessionRepository);
        _blueprints.Add(_builder.UnitBlueprint(0, 30, 50, new ExamTypeCount(QuestionType.Mcq, 5)));
        _handler = new GetMultiUnitExamOverviewHandler(_subjectRepository, _unitRepository, _examBlueprintRepository, _questionRepository, _sessionRepository, _currentUserService);
    }

    [Fact]
    public async Task Handle_NoCurrentUser_ThrowsUserNotAuthenticated()
    {
        _currentUserService.UserId.Returns((Guid?)null);

        await AssertThrowsAsync<UnauthorizedCoreException>(_builder.Subject.Id, ErrorCodes.UserNotAuthenticated);
    }

    [Fact]
    public async Task Handle_SubjectMissing_ThrowsSubjectNotFound()
    {
        await AssertThrowsAsync<NotFoundCoreException>(Guid.NewGuid(), ErrorCodes.SubjectNotFound);
    }

    [Fact]
    public async Task Handle_Units_ReturnsOrderWithBlueprintStateAndServableCount()
    {
        _blueprints.Add(_builder.DefaultBlueprint(30, 50, new ExamTypeCount(QuestionType.Mcq, 2)));

        var result = await Handle();

        result.Units.Should().Equal(new MultiUnitExamUnitOptionResult(_builder.Units[0].Id, "Mechanics", true, false, 12), new MultiUnitExamUnitOptionResult(_builder.Units[1].Id, "Waves", true, true, 3));
        result.Sizes.Should().Equal(20, 40, 60);
        (result.SubjectId, result.SubjectName, result.InProgressExam).Should().Be((_builder.Subject.Id, "Physics", (InProgressExamResult?)null));
    }

    [Fact]
    public async Task Handle_NoDefaultAndNoUnitBlueprint_UnitHasNoBlueprint()
    {
        var result = await Handle();

        result.Units.Single(x => x.UnitId == _builder.Units[1].Id).HasBlueprint.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_OpenExam_ReturnsInProgressExam()
    {
        var open = _builder.Build();
        SessionRepositoryStub.StubFind(_sessionRepository, open);

        var result = await Handle();

        result.InProgressExam.Should().Be(new InProgressExamResult(open.Id, false));
    }

    private Task<MultiUnitExamOverviewResult> Handle() => _handler.Handle(new GetMultiUnitExamOverviewQuery(_builder.Subject.Id), TestContext.Current.CancellationToken);

    private async Task AssertThrowsAsync<TException>(Guid subjectId, string errorCode) where TException : BaseException
    {
        var act = () => _handler.Handle(new GetMultiUnitExamOverviewQuery(subjectId), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<TException>()).Which.ErrorCode.Should().Be(errorCode);
    }
}
