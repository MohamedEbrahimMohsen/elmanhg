using Elmanhg.Application.Questions.Shared;
using MediatR;
using System.Text.Json;

namespace Elmanhg.Application.Questions.GradeQuestionDraft;

public sealed record GradeQuestionDraftQuery(QuestionFields Question, JsonElement Answer, Guid? LessonId = null) : IRequest<QuestionGradeResult>;
