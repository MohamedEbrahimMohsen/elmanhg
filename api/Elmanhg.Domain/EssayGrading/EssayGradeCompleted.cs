using Core.DDD.Entities;

namespace Elmanhg.Domain.EssayGrading;

public sealed record EssayGradeCompleted(EssayGrade Grade) : DomainEvent;
