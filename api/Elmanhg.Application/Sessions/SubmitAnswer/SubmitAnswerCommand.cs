using Elmanhg.Application.Sessions.Shared;
using MediatR;
using System.Text.Json;

namespace Elmanhg.Application.Sessions.SubmitAnswer;

public sealed record SubmitAnswerCommand(Guid SessionId, Guid QuestionId, JsonElement Answer, int? TimeTakenMilliseconds) : IRequest<SessionItemResult>;
