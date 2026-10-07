using Core.Localization;
using FluentValidation;
using FluentValidation.Results;
using MediatR;

namespace Core.CQRS.Behaviours;

public class ValidationBehaviour<TRequest, TResponse>(IEnumerable<IValidator<TRequest>> validators, ILocalizer localizer) : IPipelineBehavior<TRequest, TResponse> where TRequest : notnull
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        if (validators.Any())
        {
            var failures = new List<ValidationFailure>();
            foreach (var validator in validators)
            {
                var result = await validator.ValidateAsync(new ValidationContext<TRequest>(request), cancellationToken).ConfigureAwait(false);
                failures.AddRange(result.Errors);
            }

            var errorMessages = new List<string>();
            var errorCodes = new List<string>();

            foreach (var error in failures) {
                errorMessages.Add(localizer.GetMessage(error.ErrorCode, error.ErrorMessage));
                if(!string.IsNullOrEmpty(error.ErrorCode))
                    errorCodes.Add(error.ErrorCode);
            }

            if (failures.Count != 0)
            {
                throw new ValidationBehaviourException(errorCodes: errorCodes, errorMessages: errorMessages);
            }
        }
        return await next(cancellationToken).ConfigureAwait(false);
    }
}