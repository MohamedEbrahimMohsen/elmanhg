using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Progress.GetStudentSessionHistory;
using Elmanhg.Application.Shared.Options;
using Elmanhg.Domain.Sessions;
using FluentAssertions;
using Microsoft.Extensions.Options;

namespace Elmanhg.Tests.Application.Features.Progress.GetStudentSessionHistory;

public sealed class GetStudentSessionHistoryValidatorTests
{
    private readonly GetStudentSessionHistoryValidator _validator = new(Options.Create(new ProgressOptions { HistoryMaxPageSize = 50 }));

    [Fact]
    public void Validate_Valid_Passes()
    {
        var result = _validator.Validate(new GetStudentSessionHistoryQuery(Guid.NewGuid(), SessionHistoryKind.Exam));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_EmptyStudentId_FailsWithStudentIdRequired()
    {
        AssertFails(new GetStudentSessionHistoryQuery(Guid.Empty, null), ErrorCodes.StudentIdRequired);
    }

    [Fact]
    public void Validate_PageNumberZero_FailsWithPageNumberInvalid()
    {
        AssertFails(new GetStudentSessionHistoryQuery(Guid.NewGuid(), null, PageNumber: 0), ErrorCodes.SessionHistoryPageNumberInvalid);
    }

    [Fact]
    public void Validate_PageSizeAboveMax_FailsWithPageSizeInvalid()
    {
        AssertFails(new GetStudentSessionHistoryQuery(Guid.NewGuid(), null, PageSize: 51), ErrorCodes.SessionHistoryPageSizeInvalid);
    }

    [Fact]
    public void Validate_UndefinedKind_FailsWithKindInvalid()
    {
        AssertFails(new GetStudentSessionHistoryQuery(Guid.NewGuid(), (SessionHistoryKind)9), ErrorCodes.SessionHistoryKindInvalid);
    }

    private void AssertFails(GetStudentSessionHistoryQuery query, string errorCode)
    {
        _validator.Validate(query).Errors.Select(x => x.ErrorCode).Should().Contain(errorCode);
    }
}
