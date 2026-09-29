using Amazon.S3;
using Amazon.S3.Model;
using Elmanhg.Application.Shared.Storage;
using Microsoft.Extensions.Options;
using System.Net;

namespace Elmanhg.Infrastructure.Storage;

public sealed class S3FileStorage(IAmazonS3 s3, IOptions<FileStorageOptions> fileStorageOptions) : IFileStorage
{
    public async Task<string> SaveAsync(Stream content, string key, CancellationToken cancellationToken)
    {
        var options = fileStorageOptions.Value;
        var request = new PutObjectRequest
        {
            BucketName = options.S3BucketName,
            Key = key,
            InputStream = content,
            AutoCloseStream = false,
            ContentType = MediaContentTypes.FromKey(key),
            DisablePayloadSigning = true,
        };
        await s3.PutObjectAsync(request, cancellationToken).ConfigureAwait(false);
        return $"{options.PublicBaseUrl.TrimEnd('/')}/{key}";
    }

    public async Task<StoredFile?> OpenReadAsync(string key, CancellationToken cancellationToken)
    {
        try
        {
            var response = await s3.GetObjectAsync(fileStorageOptions.Value.S3BucketName, key, cancellationToken).ConfigureAwait(false);
            return new StoredFile(response.ResponseStream, response.ContentLength, MediaContentTypes.FromKey(key));
        }
        catch (AmazonS3Exception exception) when (exception.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }
}
