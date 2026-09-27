using Elmanhg.Application.Shared.Authorization;
using MediatR;

namespace Elmanhg.Tests.Integration.Infrastructure.SubjectScopeProbe;

public sealed record SubjectContentProbeResult(Guid SubjectId);

public sealed record ReadSubjectContentProbeQuery(Guid SubjectId) : IRequest<SubjectContentProbeResult>, ISubjectScopedRequest;

public sealed record MutateSubjectContentProbeCommand(Guid SubjectId) : IRequest<SubjectContentProbeResult>, ISubjectScopedRequest;

public sealed class ReadSubjectContentProbeHandler : IRequestHandler<ReadSubjectContentProbeQuery, SubjectContentProbeResult>
{
    public Task<SubjectContentProbeResult> Handle(ReadSubjectContentProbeQuery request, CancellationToken cancellationToken) => Task.FromResult(new SubjectContentProbeResult(request.SubjectId));
}

public sealed class MutateSubjectContentProbeHandler : IRequestHandler<MutateSubjectContentProbeCommand, SubjectContentProbeResult>
{
    public Task<SubjectContentProbeResult> Handle(MutateSubjectContentProbeCommand request, CancellationToken cancellationToken) => Task.FromResult(new SubjectContentProbeResult(request.SubjectId));
}
