using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Shared.Options;
using Elmanhg.Application.Students.SaveSubjectInterests;
using FluentAssertions;
using Microsoft.Extensions.Options;

namespace Elmanhg.Tests.Application.Features.Students.SaveSubjectInterests;

public sealed class SaveSubjectInterestsValidatorTests
{
    private readonly SaveSubjectInterestsValidator _validator = new(Options.Create(new StudentsOptions { SubjectInterestsMaxCount = 2 }));

    [Fact]
    public void Validate_DistinctIds_Passes()
    {
        _validator.Validate(new SaveSubjectInterestsCommand([Guid.NewGuid(), Guid.NewGuid()])).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_EmptyList_Passes()
    {
        _validator.Validate(new SaveSubjectInterestsCommand([])).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_OverMaxCount_FailsTooMany()
    {
        Codes(new SaveSubjectInterestsCommand([Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()])).Should().Contain(ErrorCodes.SubjectInterestsTooMany);
    }

    [Fact]
    public void Validate_DuplicateIds_FailsDuplicate()
    {
        var subjectId = Guid.NewGuid();

        Codes(new SaveSubjectInterestsCommand([subjectId, subjectId])).Should().Contain(ErrorCodes.SubjectInterestsDuplicate);
    }

    [Fact]
    public void Validate_EmptyGuid_FailsSubjectIdRequired()
    {
        Codes(new SaveSubjectInterestsCommand([Guid.Empty])).Should().Contain(ErrorCodes.SubjectIdRequired);
    }

    private List<string> Codes(SaveSubjectInterestsCommand command) => _validator.Validate(command).Errors.Select(x => x.ErrorCode).ToList();
}
