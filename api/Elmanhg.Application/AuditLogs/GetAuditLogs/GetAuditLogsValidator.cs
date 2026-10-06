using Core.Validation.Extensions;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Shared.Options;
using FluentValidation;
using Microsoft.Extensions.Options;

namespace Elmanhg.Application.AuditLogs.GetAuditLogs;

public sealed class GetAuditLogsValidator : AbstractValidator<GetAuditLogsQuery>
{
    public GetAuditLogsValidator(IOptions<AuditLogsOptions> auditLogsOptions)
    {
        var options = auditLogsOptions.Value;

        RuleFor(x => x).ValidatePaging(x => x.PageNumber, x => x.PageSize, options.MaxPageSize, ErrorCodes.AuditLogPageNumberInvalid, ErrorCodes.AuditLogPageSizeInvalid);
        RuleFor(x => x.Actor).ValidateMaxLength(options.FilterMaxLength, ErrorCodes.AuditLogFilterTooLong);
        RuleFor(x => x.ResourceType).ValidateMaxLength(options.FilterMaxLength, ErrorCodes.AuditLogFilterTooLong);
        RuleFor(x => x).ValidateDateRange(x => x.From, x => x.To, ErrorCodes.AuditLogDateRangeInvalid);
    }
}
