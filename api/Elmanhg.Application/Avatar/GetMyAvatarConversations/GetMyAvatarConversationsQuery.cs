using Core.DDD.Models;
using Elmanhg.Application.Avatar.Shared;
using MediatR;

namespace Elmanhg.Application.Avatar.GetMyAvatarConversations;

public sealed record GetMyAvatarConversationsQuery(int PageNumber = 1, int PageSize = 20) : IRequest<PageData<StudentAvatarConversationResult>>;
