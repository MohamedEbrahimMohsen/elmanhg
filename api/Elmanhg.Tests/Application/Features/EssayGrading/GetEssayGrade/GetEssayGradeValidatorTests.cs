using Elmanhg.Application.EssayGrading.GetEssayGrade;
using Elmanhg.Application.Exceptions;
using FluentAssertions;

namespace Elmanhg.Tests.Application.Features.EssayGrading.GetEssayGrade;

public sealed class GetEssayGradeValidatorTests
{
    private readonly GetEssayGradeValidator _validator = new();

    [Fact]
    public void Validate_Valid_HasNoErrors()
    {
        _validator.Validate(new GetEssayGradeQuery(Guid.NewGuid(), Guid.NewGuid())).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_EmptySessionId_HasSessionIdRequired()
    {
        _validator.Validate(new GetEssayGradeQuery(Guid.Empty, Guid.NewGuid())).Errors.Select(x => x.ErrorCode).Should().Equal(ErrorCodes.SessionIdRequired);
    }

    [Fact]
    public void Validate_EmptyQuestionId_HasQuestionIdRequired()
    {
        _validator.Validate(new GetEssayGradeQuery(Guid.NewGuid(), Guid.Empty)).Errors.Select(x => x.ErrorCode).Should().Equal(ErrorCodes.QuestionIdRequired);
    }
}
