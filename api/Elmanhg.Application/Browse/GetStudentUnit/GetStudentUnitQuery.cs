using Elmanhg.Application.Browse.Shared;
using MediatR;

namespace Elmanhg.Application.Browse.GetStudentUnit;

public sealed record GetStudentUnitQuery(Guid UnitId) : IRequest<StudentUnitResult>;
