using Core.CQRS.Behaviours;
using Core.Localization;
using FluentAssertions;
using FluentValidation;
using MediatR;
using NSubstitute;

namespace Elmanhg.Tests.Core.CQRS;

public sealed record ValidationProbeRequest : IRequest<string>;

public sealed class GatedValidator : AbstractValidator<ValidationProbeRequest>
{
    public GatedValidator(Task gate) => RuleFor(x => x).MustAsync(async (_, _) => { await gate; return true; });
}

public sealed class RecordingValidator : AbstractValidator<ValidationProbeRequest>
{
    public RecordingValidator() => RuleFor(x => x).Must(_ => { Ran = true; return true; });

    public bool Ran { get; private set; }
}

public sealed class FailingValidator : AbstractValidator<ValidationProbeRequest>
{
    public FailingValidator(string code) => RuleFor(x => x).Must(_ => false).WithErrorCode(code);
}

public sealed class ValidationBehaviourTests
{
    private readonly ILocalizer _localizer = Substitute.For<ILocalizer>();
    private bool _nextCalled;

    public ValidationBehaviourTests() => _localizer.GetMessage(Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<Dictionary<string, object>?>()).Returns(call => call.ArgAt<string?>(1) ?? string.Empty);

    [Fact]
    public async Task Handle_TwoValidators_RunsSecondOnlyAfterFirstCompletes()
    {
        var gate = new TaskCompletionSource();
        var recording = new RecordingValidator();
        var handling = Behaviour([new GatedValidator(gate.Task), recording]).Handle(new ValidationProbeRequest(), Next, TestContext.Current.CancellationToken);

        recording.Ran.Should().BeFalse();

        gate.SetResult();
        await handling;
        recording.Ran.Should().BeTrue();
        _nextCalled.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_FailuresFromEveryValidator_ThrowsWithCodesInValidatorOrder()
    {
        var act = () => Behaviour([new FailingValidator("A_CODE"), new FailingValidator("B_CODE")]).Handle(new ValidationProbeRequest(), Next, TestContext.Current.CancellationToken);

        var exception = (await act.Should().ThrowAsync<ValidationBehaviourException>()).Which;
        (exception.ErrorCode, exception.StatusCode).Should().Be(("A_CODE,B_CODE", 422));
        _nextCalled.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_NoValidators_CallsNext()
    {
        var response = await Behaviour([]).Handle(new ValidationProbeRequest(), Next, TestContext.Current.CancellationToken);

        response.Should().Be("handled");
    }

    private ValidationBehaviour<ValidationProbeRequest, string> Behaviour(IEnumerable<IValidator<ValidationProbeRequest>> validators) => new(validators, _localizer);

    private Task<string> Next(CancellationToken cancellationToken)
    {
        _nextCalled = true;
        return Task.FromResult("handled");
    }
}
