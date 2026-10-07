using MediatR;

namespace Elmanhg.Application.Shared.Retries;

public interface IFailRetriedWorkCommand : IRequest
{
    Guid WorkId { get; }
    string ErrorCode { get; }
}
