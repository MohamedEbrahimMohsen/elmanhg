using Elmanhg.Domain.ContentRetrieval;
using MediatR;

namespace Elmanhg.Application.ContentRetrieval.RebuildContentIndex;

public sealed class RebuildContentIndexHandler(ILessonContentIndexRepository lessonContentIndexRepository) : IRequestHandler<RebuildContentIndexCommand>
{
    public async Task Handle(RebuildContentIndexCommand request, CancellationToken cancellationToken)
    {
        await lessonContentIndexRepository.DeleteAllAsync(cancellationToken).ConfigureAwait(false);
    }
}
