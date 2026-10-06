using Core.DDD.Repositories;
using Core.Errors;
using Core.Identity.Tokens.CurrentUser;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Teachers.Shared;
using Elmanhg.Domain.Identity;
using Elmanhg.Domain.Subjects;
using Elmanhg.Domain.Teachers;
using MediatR;
using Microsoft.AspNetCore.Identity;

namespace Elmanhg.Application.Teachers.AssignTeacherSubject;

public sealed class AssignTeacherSubjectHandler(UserManager<User> userManager, ISubjectRepository subjectRepository, ITeacherSubjectRepository teacherSubjectRepository, ICurrentUserService currentUserService) : IRequestHandler<AssignTeacherSubjectCommand, TeacherSubjectResult>
{
    public async Task<TeacherSubjectResult> Handle(AssignTeacherSubjectCommand request, CancellationToken cancellationToken)
    {
        var userId = currentUserService.GetRequiredUserId(ErrorCodes.UserNotAuthenticated);

        var teacher = await userManager.FindByIdAsync(request.TeacherId.ToString()).ConfigureAwait(false);
        if (teacher is null)
        {
            throw new NotFoundCoreException(ErrorCodes.UserNotFound);
        }

        var subject = await subjectRepository.GetRequiredAsync(request.SubjectId, ErrorCodes.SubjectNotFound, cancellationToken, asNoTracking: true).ConfigureAwait(false);

        if (await teacherSubjectRepository.IsAssignedAsync(request.TeacherId, request.SubjectId, cancellationToken).ConfigureAwait(false))
        {
            throw new ConflictCoreException(ErrorCodes.TeacherSubjectAlreadyAssigned);
        }

        var teacherSubject = TeacherSubject.Create(teacher, subject, userId);

        await teacherSubjectRepository.AddAsync(teacherSubject, cancellationToken).ConfigureAwait(false);
        await teacherSubjectRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return new TeacherSubjectResult(teacherSubject.TeacherId, teacherSubject.SubjectId, teacherSubject.CreationDate);
    }
}
