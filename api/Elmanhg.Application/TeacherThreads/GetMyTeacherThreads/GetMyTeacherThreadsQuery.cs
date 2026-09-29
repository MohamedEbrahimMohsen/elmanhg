using Core.DDD.Models;
using Elmanhg.Application.TeacherThreads.Shared;
using MediatR;

namespace Elmanhg.Application.TeacherThreads.GetMyTeacherThreads;

public sealed record GetMyTeacherThreadsQuery(int PageNumber = 1, int PageSize = 20) : IRequest<PageData<TeacherThreadSummaryResult>>;
