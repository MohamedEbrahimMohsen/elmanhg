using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Subjects.GetSubject;
using FluentAssertions;

namespace Elmanhg.Tests.Application.Features.Subjects.GetSubject;

public sealed class GetSubjectValidatorTests
{
    private readonly GetSubjectValidator _validator = new();

    [Fact]
    public void Validate_ValidQuery_Passes()
    {
        var result = _validator.Validate(new GetSubjectQuery(Guid.NewGuid()));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_EmptySubjectId_FailsWithSubjectIdRequired()
    {
        var result = _validator.Validate(new GetSubjectQuery(Guid.Empty));

        result.Errors.Select(x => x.ErrorCode).Should().Contain(ErrorCodes.SubjectIdRequired);
    }
}
