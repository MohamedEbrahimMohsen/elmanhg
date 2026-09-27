using Core.Localization;
using Microsoft.Extensions.Hosting;

namespace Core.Exceptions;

public sealed class ErrorResponseHandler(ILocalizer localizer, IHostEnvironment hostEnvironment) : IErrorResponseHandler
{
    public ErrorResponse GenerateErrorResponse(string code, string message)
    {
        return new ErrorResponse
        {
            Code = code,
            Message = localizer.GetMessage(code, message)
        };
    }
    public ErrorResponse<T> GenerateErrorResponse<T>(string code, string message, T data)
    {
        return new ErrorResponse<T>
        {
            Code = code,
            Message = localizer.GetMessage(code, message),
            Data = data
        };
    }
    public ErrorResponse<string> GenerateErrorResponse(ExceptionDetails exceptionDetails)
    {
        ArgumentNullException.ThrowIfNull(exceptionDetails);

        return new ErrorResponse<string>
        {
            Code = exceptionDetails.Code,
            Message = localizer.GetMessage(exceptionDetails.Code, exceptionDetails.Message, exceptionDetails.Context),
            Data = hostEnvironment.IsDevelopment() ? $"{exceptionDetails.Exception?.Message} - {exceptionDetails.Exception?.StackTrace}" : null
        };
    }
}