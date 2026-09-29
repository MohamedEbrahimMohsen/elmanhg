using Core.Validation.Extensions;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Shared.Options;
using FluentValidation;
using Microsoft.Extensions.Options;

namespace Elmanhg.Application.Observability.ReportClientError;

public sealed class ReportClientErrorValidator : AbstractValidator<ReportClientErrorCommand>
{
    public ReportClientErrorValidator(IOptions<ClientErrorsOptions> clientErrorsOptions)
    {
        var options = clientErrorsOptions.Value;
        RuleFor(x => x.Message).ValidateRequired(ErrorCodes.ClientErrorMessageRequired).ValidateMaxLength(options.MessageMaxLength, ErrorCodes.ClientErrorMessageTooLong);
        RuleFor(x => x.ErrorName).ValidateMaxLength(options.ErrorNameMaxLength, ErrorCodes.ClientErrorNameTooLong);
        RuleFor(x => x.Stack).ValidateMaxLength(options.StackMaxLength, ErrorCodes.ClientErrorStackTooLong);
        RuleFor(x => x.Path).ValidateMaxLength(options.PathMaxLength, ErrorCodes.ClientErrorPathTooLong).Must(x => x is null || x.StartsWith('/')).WithErrorCode(ErrorCodes.ClientErrorPathInvalid);
        RuleFor(x => x.Source).IsInEnum().WithErrorCode(ErrorCodes.ClientErrorSourceInvalid);
    }
}
