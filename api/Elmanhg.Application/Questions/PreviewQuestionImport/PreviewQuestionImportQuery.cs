using MediatR;
using Microsoft.AspNetCore.Http;

namespace Elmanhg.Application.Questions.PreviewQuestionImport;

public sealed record PreviewQuestionImportQuery(Guid LessonId, IFormFile? File) : IRequest<QuestionImportPreviewResult>;
