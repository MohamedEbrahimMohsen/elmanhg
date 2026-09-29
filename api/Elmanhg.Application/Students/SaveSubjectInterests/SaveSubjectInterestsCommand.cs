using MediatR;

namespace Elmanhg.Application.Students.SaveSubjectInterests;

public sealed record SaveSubjectInterestsCommand(IList<Guid> SubjectIds) : IRequest;
