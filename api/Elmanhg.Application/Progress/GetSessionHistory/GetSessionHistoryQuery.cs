using Core.DDD.Models;
using Elmanhg.Application.Progress.Shared;
using Elmanhg.Domain.Sessions;
using MediatR;

namespace Elmanhg.Application.Progress.GetSessionHistory;

public sealed record GetSessionHistoryQuery(SessionHistoryKind? Kind, int PageNumber = 1, int PageSize = 20) : IRequest<PageData<SessionHistoryItemResult>>;
