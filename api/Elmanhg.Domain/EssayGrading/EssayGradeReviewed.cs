using Core.DDD.Entities;

namespace Elmanhg.Domain.EssayGrading;

public sealed record EssayGradeReviewed(EssayGrade Grade) : DomainEvent;
