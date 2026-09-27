using Elmanhg.Application.AuditLogs.GetAuditLogs;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Shared.Options;
using FluentAssertions;
using Microsoft.Extensions.Options;

namespace Elmanhg.Tests.Application.Features.AuditLogs.GetAuditLogs;

public sealed class GetAuditLogsValidatorTests
{
    private static readonly DateTimeOffset From = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private readonly GetAuditLogsValidator _validator = new(Options.Create(new AuditLogsOptions { MaxPageSize = 100, FilterMaxLength = 256 }));

    [Fact]
    public void Validate_ValidQuery_Passes()
    {
        var result = _validator.Validate(new GetAuditLogsQuery("admin", "Teacher", From, From.AddDays(1), 1, 100));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_PageNumberZero_FailsWithPageNumberInvalid()
    {
        var result = _validator.Validate(Query() with { PageNumber = 0 });

        result.Errors.Select(x => x.ErrorCode).Should().Contain(ErrorCodes.AuditLogPageNumberInvalid);
    }

    [Fact]
    public void Validate_PageSizeZero_FailsWithPageSizeInvalid()
    {
        var result = _validator.Validate(Query() with { PageSize = 0 });

        result.Errors.Select(x => x.ErrorCode).Should().Contain(ErrorCodes.AuditLogPageSizeInvalid);
    }

    [Fact]
    public void Validate_PageSizeAboveMax_FailsWithPageSizeInvalid()
    {
        var result = _validator.Validate(Query() with { PageSize = 101 });

        result.Errors.Select(x => x.ErrorCode).Should().Contain(ErrorCodes.AuditLogPageSizeInvalid);
    }

    [Fact]
    public void Validate_ActorTooLong_FailsWithFilterTooLong()
    {
        var result = _validator.Validate(Query() with { Actor = new string('a', 257) });

        result.Errors.Select(x => x.ErrorCode).Should().Contain(ErrorCodes.AuditLogFilterTooLong);
    }

    [Fact]
    public void Validate_ResourceTypeTooLong_FailsWithFilterTooLong()
    {
        var result = _validator.Validate(Query() with { ResourceType = new string('a', 257) });

        result.Errors.Select(x => x.ErrorCode).Should().Contain(ErrorCodes.AuditLogFilterTooLong);
    }

    [Fact]
    public void Validate_ToNotAfterFrom_FailsWithDateRangeInvalid()
    {
        var result = _validator.Validate(Query() with { From = From, To = From });

        result.Errors.Select(x => x.ErrorCode).Should().Contain(ErrorCodes.AuditLogDateRangeInvalid);
    }

    [Fact]
    public void Validate_OnlyFrom_Passes()
    {
        var result = _validator.Validate(Query() with { From = From });

        result.IsValid.Should().BeTrue();
    }

    private static GetAuditLogsQuery Query() => new(null, null, null, null);
}
