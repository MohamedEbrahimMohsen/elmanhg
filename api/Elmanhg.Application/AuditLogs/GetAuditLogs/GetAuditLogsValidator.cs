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

        RuleFor(x => x.PageNumber).ValidateMin(1, ErrorCodes.AuditLogPageNumberInvalid);
        RuleFor(x => x.PageSize).ValidateRange(1, options.MaxPageSize, ErrorCodes.AuditLogPageSizeInvalid);
        RuleFor(x => x.Actor).ValidateMaxLength(options.FilterMaxLength, ErrorCodes.AuditLogFilterTooLong);
        RuleFor(x => x.ResourceType).ValidateMaxLength(options.FilterMaxLength, ErrorCodes.AuditLogFilterTooLong);
        RuleFor(x => x).Must(x => x.From is null || x.To is null || x.From < x.To).WithErrorCode(ErrorCodes.AuditLogDateRangeInvalid);
    }
}
