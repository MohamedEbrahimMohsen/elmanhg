using Core.DDD.Models;
using Elmanhg.Application.Avatar.Shared;
using Elmanhg.Domain.Avatar;
using MediatR;

namespace Elmanhg.Application.Avatar.GetAvatarConversations;

public sealed record GetAvatarConversationsQuery(string? Search, AvatarEntryPoint? EntryPoint, DateTimeOffset? From, DateTimeOffset? To, int PageNumber = 1, int PageSize = 20) : IRequest<PageData<AdminAvatarConversationResult>>;
