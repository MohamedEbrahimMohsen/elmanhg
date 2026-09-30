using MediatR;

namespace Elmanhg.Application.EssayGrading.GradeEssay;

public sealed record GradeEssayCommand(Guid EssayGradeId) : IRequest;
