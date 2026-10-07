using Core.Storage;
using Elmanhg.Application.TeacherThreads.CreateTeacherThread;
using Elmanhg.Application.TrainingExports.Shared;
using Microsoft.Extensions.Options;

namespace Elmanhg.Api.FileStorage;

public static class MediaStorageExtensions
{
    public static readonly IReadOnlyList<string> PrivateFolders = [TeacherThreadImageFormats.StorageFolder, TrainingExportFiles.StorageFolder];

    public static WebApplication UseMediaStorage(this WebApplication app)
    {
        var options = app.Services.GetRequiredService<IOptions<FileStorageOptions>>().Value;
        app.UseMiddleware<TeacherThreadMediaMiddleware>(new PathString(options.PublicBaseUrl));
        app.UseCorePublicMedia(PrivateFolders);
        return app;
    }
}
