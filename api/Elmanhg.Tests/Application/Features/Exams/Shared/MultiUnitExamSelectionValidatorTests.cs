using Elmanhg.Application.Exams.Shared;
using Elmanhg.Application.Exceptions;
using FluentAssertions;

namespace Elmanhg.Tests.Application.Features.Exams.Shared;

public sealed class MultiUnitExamSelectionValidatorTests
{
    private readonly MultiUnitExamSelectionValidator _validator = new();

    [Fact]
    public void Validate_ValidSelection_Passes()
    {
        _validator.Validate(Selection()).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_EmptySubjectId_ReturnsSubjectIdRequired()
    {
        Codes(Selection() with { SubjectId = Guid.Empty }).Should().Contain(ErrorCodes.SubjectIdRequired);
    }

    [Fact]
    public void Validate_NullUnitIds_ReturnsUnitsTooFew()
    {
        Codes(Selection() with { UnitIds = null }).Should().Contain(ErrorCodes.MultiUnitExamUnitsTooFew);
    }

    [Fact]
    public void Validate_OneUnit_ReturnsUnitsTooFew()
    {
        Codes(Selection() with { UnitIds = [Guid.NewGuid()] }).Should().Contain(ErrorCodes.MultiUnitExamUnitsTooFew);
    }

    [Fact]
    public void Validate_DuplicateUnit_ReturnsUnitDuplicate()
    {
        var unitId = Guid.NewGuid();

        Codes(Selection() with { UnitIds = [unitId, unitId] }).Should().Contain(ErrorCodes.MultiUnitExamUnitDuplicate);
    }

    [Fact]
    public void Validate_EmptyUnitId_ReturnsUnitIdRequired()
    {
        Codes(Selection() with { UnitIds = [Guid.NewGuid(), Guid.Empty] }).Should().Contain(ErrorCodes.UnitIdRequired);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(10)]
    [InlineData(30)]
    [InlineData(80)]
    public void Validate_SizeNotAllowed_ReturnsSizeInvalid(int size)
    {
        Codes(Selection() with { Size = size }).Should().Contain(ErrorCodes.MultiUnitExamSizeInvalid);
    }

    private static MultiUnitExamSelection Selection() => new(Guid.NewGuid(), [Guid.NewGuid(), Guid.NewGuid()], 20);

    private List<string> Codes(MultiUnitExamSelection selection) => _validator.Validate(selection).Errors.Select(x => x.ErrorCode).ToList();
}
