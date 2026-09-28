using Core.Auditing;
using MediatR;
using Microsoft.AspNetCore.Http;

namespace Elmanhg.Application.Questions.ImportQuestions;

public sealed record ImportQuestionsCommand(Guid LessonId, Guid BatchId, IFormFile? File) : IRequest<ImportQuestionsResult>, IAuditableCommand
{
    public string AuditAction => "Question.Import";
    public string AuditResourceType => "QuestionImportBatch";
    public Guid? AuditResourceId => BatchId;
}
