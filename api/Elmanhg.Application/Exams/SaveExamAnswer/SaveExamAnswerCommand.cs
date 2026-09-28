using Elmanhg.Application.Exams.Shared;
using MediatR;
using System.Text.Json;

namespace Elmanhg.Application.Exams.SaveExamAnswer;

public sealed record SaveExamAnswerCommand(Guid SessionId, Guid QuestionId, JsonElement Answer) : IRequest<ExamAnswerSavedResult>;
