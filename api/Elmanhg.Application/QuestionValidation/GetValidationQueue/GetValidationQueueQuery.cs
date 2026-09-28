using Core.DDD.Models;
using Elmanhg.Application.QuestionValidation.Shared;
using Elmanhg.Domain.Questions;
using MediatR;

namespace Elmanhg.Application.QuestionValidation.GetValidationQueue;

public sealed record GetValidationQueueQuery(Guid? UnitId, Guid? LessonId, QuestionType? Type, QuestionDifficulty? Difficulty, int? MinAgeDays, Guid? ReviewSessionId, int PageNumber = 1, int PageSize = 20) : IRequest<PageData<ValidationQueueItemResult>>;
