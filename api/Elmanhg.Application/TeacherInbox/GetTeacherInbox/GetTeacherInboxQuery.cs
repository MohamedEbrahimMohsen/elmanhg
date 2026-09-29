using Core.DDD.Models;
using Elmanhg.Application.TeacherInbox.Shared;
using MediatR;

namespace Elmanhg.Application.TeacherInbox.GetTeacherInbox;

public sealed record GetTeacherInboxQuery(TeacherInboxFilter Filter = TeacherInboxFilter.All, int PageNumber = 1, int PageSize = 20) : IRequest<PageData<TeacherInboxItemResult>>;
