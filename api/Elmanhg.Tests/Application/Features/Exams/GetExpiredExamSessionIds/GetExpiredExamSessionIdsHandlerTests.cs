using Core.DDD.Models;
using Elmanhg.Application.Exams.GetExpiredExamSessionIds;
using Elmanhg.Application.Shared.Options;
using Elmanhg.Domain.Questions.Grading;
using Elmanhg.Domain.Sessions;
using Elmanhg.Tests.Builders;
using FluentAssertions;
using Microsoft.Extensions.Options;
using NSubstitute;
using System.Linq.Expressions;

namespace Elmanhg.Tests.Application.Features.Exams.GetExpiredExamSessionIds;

public sealed class GetExpiredExamSessionIdsHandlerTests
{
    private readonly ISessionRepository _sessionRepository = Substitute.For<ISessionRepository>();
    private readonly TimeProvider _timeProvider = Substitute.For<TimeProvider>();
    private readonly ExamSessionBuilder _builder = new();

    [Fact]
    public async Task Handle_Sessions_ReturnsOnlyExpiredOpenExamsPastGrace()
    {
        var expired = Start(ExamSessionBuilder.Now, 30);
        var withinGrace = Start(ExamSessionBuilder.Now.AddSeconds(20), 30);
        var submitted = Start(ExamSessionBuilder.Now, 30);
        submitted.SubmitExam(new Dictionary<Guid, QuestionGrade>(), ExamSessionBuilder.Now.AddMinutes(1));
        var untimed = Start(ExamSessionBuilder.Now, null);
        var quiz = new SessionBuilder().Build();
        List<Session> sessions = [expired, withinGrace, submitted, untimed, quiz];
        _timeProvider.GetUtcNow().Returns(ExamSessionBuilder.Now.AddMinutes(30).AddSeconds(40));
        _sessionRepository.FindPaginatedAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>(), Arg.Any<Expression<Func<Session, bool>>?>(), Arg.Any<Func<IQueryable<Session>, IQueryable<Session>>?>(), Arg.Any<Func<IQueryable<Session>, IOrderedQueryable<Session>>?>(), Arg.Any<bool>())
            .Returns(call => new PageData<Session> { Items = sessions.Where(call.Arg<Expression<Func<Session, bool>>?>()!.Compile()).ToList() });
        var handler = new GetExpiredExamSessionIdsHandler(_sessionRepository, Options.Create(new ExamsOptions { AutoSubmitBatchSize = 25 }), _timeProvider);

        var result = await handler.Handle(new GetExpiredExamSessionIdsQuery([]), TestContext.Current.CancellationToken);

        result.Should().Equal(expired.Id);
        await _sessionRepository.Received(1).FindPaginatedAsync(1, 25, Arg.Any<CancellationToken>(), Arg.Any<Expression<Func<Session, bool>>?>(), Arg.Any<Func<IQueryable<Session>, IQueryable<Session>>?>(), Arg.Any<Func<IQueryable<Session>, IOrderedQueryable<Session>>?>(), true);
    }

    [Fact]
    public async Task Handle_ExcludedIds_SkipsThem()
    {
        var excluded = Start(ExamSessionBuilder.Now, 30);
        var expired = Start(ExamSessionBuilder.Now, 30);
        List<Session> sessions = [excluded, expired];
        _timeProvider.GetUtcNow().Returns(ExamSessionBuilder.Now.AddHours(1));
        _sessionRepository.FindPaginatedAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>(), Arg.Any<Expression<Func<Session, bool>>?>(), Arg.Any<Func<IQueryable<Session>, IQueryable<Session>>?>(), Arg.Any<Func<IQueryable<Session>, IOrderedQueryable<Session>>?>(), Arg.Any<bool>())
            .Returns(call => new PageData<Session> { Items = sessions.Where(call.Arg<Expression<Func<Session, bool>>?>()!.Compile()).ToList() });
        var handler = new GetExpiredExamSessionIdsHandler(_sessionRepository, Options.Create(new ExamsOptions()), _timeProvider);

        var result = await handler.Handle(new GetExpiredExamSessionIdsQuery([excluded.Id]), TestContext.Current.CancellationToken);

        result.Should().Equal(expired.Id);
    }

    private Session Start(DateTimeOffset startedAt, int? timeLimitMinutes) => Session.StartUnitExam(_builder.StudentId, _builder.Questions.Unit, _builder.Blueprint(1, timeLimitMinutes), _builder.BuildQuestions(1), [_builder.Questions.Lesson], false, startedAt);
}
