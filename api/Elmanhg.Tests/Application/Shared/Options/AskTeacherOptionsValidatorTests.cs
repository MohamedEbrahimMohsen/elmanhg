using Elmanhg.Application.Shared.Options;
using FluentAssertions;

namespace Elmanhg.Tests.Application.Shared.Options;

public sealed class AskTeacherOptionsValidatorTests
{
    private readonly AskTeacherOptionsValidator _validator = new(Microsoft.Extensions.Options.Options.Create(new SubscriptionsOptions { AskTeacherReplySlaHours = 24 }));

    [Fact]
    public void Validate_DefaultStages_Succeeds()
    {
        var result = _validator.Validate(null, new AskTeacherOptions());

        result.Succeeded.Should().BeTrue();
    }

    [Theory]
    [InlineData(20, 12)]
    [InlineData(12, 12)]
    [InlineData(12, 24)]
    [InlineData(12, 30)]
    public void Validate_StagesOutOfOrder_FailsNamingTheRule(int firstHours, int secondHours)
    {
        var options = new AskTeacherOptions { FirstReminderAfterHours = firstHours, SecondReminderAfterHours = secondHours };

        var result = _validator.Validate(null, options);

        result.Failures.Should().ContainSingle().Which.Should().StartWith("AskTeacher:FirstReminderAfterHours");
    }
}
