using Elmanhg.Application.Avatar.Shared;
using Elmanhg.Domain.Avatar;
using MediatR;

namespace Elmanhg.Application.Avatar.SendAvatarMessage;

public sealed record SendAvatarMessageCommand(AvatarEntryPoint EntryPoint, Guid? LessonId, Guid? SessionId, Guid? QuestionId, IList<AvatarTurn> History, string Message) : IRequest<AvatarReplyResult>;
