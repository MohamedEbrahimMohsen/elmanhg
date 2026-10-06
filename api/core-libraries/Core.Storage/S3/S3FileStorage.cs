using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Options;
using System.Net;

namespace Core.Storage.S3;

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
            ContentType = StorageContentTypes.FromKey(key),
            DisablePayloadSigning = true,
        };
        await s3.PutObjectAsync(request, cancellationToken).ConfigureAwait(false);
        return GetPublicUrl(key);
    }

    public string GetPublicUrl(string key) => fileStorageOptions.Value.GetPublicUrl(key);

    public async Task<StoredFile?> OpenReadAsync(string key, CancellationToken cancellationToken)
    {
        try
        {
            var response = await s3.GetObjectAsync(fileStorageOptions.Value.S3BucketName, key, cancellationToken).ConfigureAwait(false);
            return new StoredFile(response.ResponseStream, response.ContentLength, StorageContentTypes.FromKey(key));
        }
        catch (AmazonS3Exception exception) when (exception.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task DeleteAsync(string key, CancellationToken cancellationToken)
    {
        await s3.DeleteObjectAsync(fileStorageOptions.Value.S3BucketName, key, cancellationToken).ConfigureAwait(false);
    }
}
