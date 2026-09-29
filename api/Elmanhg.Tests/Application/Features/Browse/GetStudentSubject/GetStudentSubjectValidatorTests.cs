using Elmanhg.Application.Browse.GetStudentSubject;
using Elmanhg.Application.Exceptions;
using FluentAssertions;

namespace Elmanhg.Tests.Application.Features.Browse.GetStudentSubject;

public sealed class GetStudentSubjectValidatorTests
{
    private readonly GetStudentSubjectValidator _validator = new();

    [Fact]
    public void Validate_SubjectId_Passes()
    {
        var result = _validator.Validate(new GetStudentSubjectQuery(Guid.NewGuid()));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_EmptySubjectId_FailsWithSubjectIdRequired()
    {
        var result = _validator.Validate(new GetStudentSubjectQuery(Guid.Empty));

        result.Errors.Select(x => x.ErrorCode).Should().Contain(ErrorCodes.SubjectIdRequired);
    }
}
