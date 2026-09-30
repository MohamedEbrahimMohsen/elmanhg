using Core.Auditing;
using MediatR;
using Microsoft.AspNetCore.Http;

namespace Elmanhg.Application.Lessons.UploadDiagramImage;

public sealed record UploadDiagramImageCommand(Guid LessonId, IFormFile? File) : IRequest<UploadDiagramImageResult>, IAuditableCommand
{
    public string AuditAction => "Lesson.UploadDiagramImage";
    public string AuditResourceType => "Lesson";
    public Guid? AuditResourceId => LessonId;
}
