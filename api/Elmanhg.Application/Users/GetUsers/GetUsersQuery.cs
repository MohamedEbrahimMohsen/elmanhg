using Core.DDD.Models;
using Elmanhg.Application.Users.Shared;
using Elmanhg.Domain.Identity;
using MediatR;

namespace Elmanhg.Application.Users.GetUsers;

public sealed record GetUsersQuery(UserRole Role, string? Search, UserStatus? Status, int PageNumber = 1, int PageSize = 20) : IRequest<PageData<UserSummaryResult>>;
