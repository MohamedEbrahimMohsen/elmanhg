using Elmanhg.Application.Users.CheckUserActive;
using MediatR;

namespace Elmanhg.Tests.Integration.Infrastructure;

public sealed class ThrowingCheckUserActiveHandler : IRequestHandler<CheckUserActiveQuery, bool>
{
    public Task<bool> Handle(CheckUserActiveQuery request, CancellationToken cancellationToken) => throw new InvalidOperationException("probe database failure");
}
