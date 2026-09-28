using Core.Auditing;
using MediatR;
using Microsoft.AspNetCore.Http;

namespace Elmanhg.Application.Lessons.UploadLessonImage;

public sealed record UploadLessonImageCommand(Guid LessonId, IFormFile? File) : IRequest<UploadLessonImageResult>, IAuditableCommand
{
    public string AuditAction => "Lesson.UploadImage";
    public string AuditResourceType => "Lesson";
    public Guid? AuditResourceId => LessonId;
}
