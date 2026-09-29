using Core.Auditing;
using MediatR;

namespace Elmanhg.Application.ContentRetrieval.RebuildContentIndex;

public sealed record RebuildContentIndexCommand : IRequest, IAuditableCommand
{
    public string AuditAction => "ContentIndex.Rebuild";
    public string AuditResourceType => "ContentIndex";
    public Guid? AuditResourceId => null;
}
