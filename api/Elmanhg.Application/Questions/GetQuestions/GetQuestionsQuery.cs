using Core.DDD.Models;
using Elmanhg.Application.Questions.Shared;
using Elmanhg.Domain.Questions;
using MediatR;

namespace Elmanhg.Application.Questions.GetQuestions;

public sealed record GetQuestionsQuery(QuestionValidationStatus? Status, QuestionType? Type, Guid? SubjectId, Guid? LessonId, Guid? TeacherId, int? MinVersion, string? RejectionReason, int PageNumber = 1, int PageSize = 20) : IRequest<PageData<QuestionListItemResult>>;
