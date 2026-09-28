using MediatR;

namespace Elmanhg.Application.Questions.GetQuestionImportTemplate;

public sealed record GetQuestionImportTemplateQuery : IRequest<QuestionImportTemplateResult>;
