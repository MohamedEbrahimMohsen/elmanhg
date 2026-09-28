using Elmanhg.Application.Exams.Shared;
using MediatR;

namespace Elmanhg.Application.Exams.PreviewMultiUnitExam;

public sealed record PreviewMultiUnitExamQuery(MultiUnitExamSelection Selection) : IRequest<MultiUnitExamPreviewResult>;
