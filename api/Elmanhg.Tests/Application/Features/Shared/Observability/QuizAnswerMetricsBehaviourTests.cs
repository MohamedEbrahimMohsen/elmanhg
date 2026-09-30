using Elmanhg.Application.Sessions.Shared;
using Elmanhg.Application.Sessions.SubmitAnswer;
using Elmanhg.Application.Shared.Observability;
using FluentAssertions;
using Microsoft.Extensions.Diagnostics.Metrics.Testing;
using System.Diagnostics.Metrics;
using System.Text.Json;

namespace Elmanhg.Tests.Application.Features.Shared.Observability;

public sealed class QuizAnswerMetricsBehaviourTests : IDisposable
{
    private static readonly JsonElement Answer = JsonSerializer.SerializeToElement(new { choice = 1 });
    private static readonly SubmitAnswerCommand Command = new(Guid.CreateVersion7(), Guid.CreateVersion7(), Answer, 1200);
    private readonly IMeterFactory _meterFactory = MeterFactories.Create();
    private readonly MetricCollector<long> _answers;
    private readonly QuizAnswerMetricsBehaviour _behaviour;

    public QuizAnswerMetricsBehaviourTests()
    {
        _answers = new MetricCollector<long>(_meterFactory, ElmanhgTelemetry.SourceName, "elmanhg.quiz.answers");
        _behaviour = new QuizAnswerMetricsBehaviour(new ElmanhgMetrics(_meterFactory));
    }

    [Fact]
    public async Task Handle_AnswerGraded_RecordsAttemptOutcome()
    {
        var graded = Item(new AttemptResult(Guid.CreateVersion7(), Answer, 1, 1, "Correct", null, 1200, new DateTimeOffset(2026, 9, 29, 10, 0, 0, TimeSpan.Zero)));

        var result = await _behaviour.Handle(Command, _ => Task.FromResult(graded), TestContext.Current.CancellationToken);

        result.Should().BeSameAs(graded);
        _answers.GetMeasurementSnapshot().Should().ContainSingle().Which.Tags.Should().Contain(ElmanhgMetrics.OutcomeTag, "Correct");
    }

    [Fact]
    public async Task Handle_ResultWithoutAttempt_RecordsUngraded()
    {
        await _behaviour.Handle(Command, _ => Task.FromResult(Item(null)), TestContext.Current.CancellationToken);

        _answers.GetMeasurementSnapshot().Should().ContainSingle().Which.Tags.Should().Contain(ElmanhgMetrics.OutcomeTag, "Ungraded");
    }

    [Fact]
    public async Task Handle_NextThrows_RecordsNothing()
    {
        var act = () => _behaviour.Handle(Command, _ => throw new InvalidOperationException(), TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<InvalidOperationException>();
        _answers.GetMeasurementSnapshot().Should().BeEmpty();
    }

    public void Dispose() => _answers.Dispose();

    private static SessionItemResult Item(AttemptResult? attempt) => new(1, Command.QuestionId, 1, "Mcq", "stem", Answer, 1, attempt, null, null, null);
}
