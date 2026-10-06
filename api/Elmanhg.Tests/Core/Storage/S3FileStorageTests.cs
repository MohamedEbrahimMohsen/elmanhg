using Amazon.S3;
using Amazon.S3.Model;
using Core.Storage;
using Core.Storage.S3;
using FluentAssertions;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using System.Net;

namespace Elmanhg.Tests.Core.Storage;

public sealed class S3FileStorageTests
{
    private const string Bucket = "elmanhg-media";
    private readonly IAmazonS3 _s3 = Substitute.For<IAmazonS3>();
    private readonly S3FileStorage _storage;

    public S3FileStorageTests()
    {
        _storage = new S3FileStorage(_s3, Options.Create(new FileStorageOptions { Provider = FileStorageProvider.S3, PublicBaseUrl = "/api/media/", S3BucketName = Bucket }));
    }

    [Fact]
    public async Task SaveAsync_PutsObjectInBucketAndReturnsPublicUrl()
    {
        using var content = new MemoryStream([0x89, 0x50]);

        var url = await _storage.SaveAsync(content, "lessons/a/b.png", TestContext.Current.CancellationToken);

        url.Should().Be("/api/media/lessons/a/b.png");
        await _s3.Received(1).PutObjectAsync(Arg.Is<PutObjectRequest>(x => x.BucketName == Bucket && x.Key == "lessons/a/b.png" && x.ContentType == "image/png" && x.DisablePayloadSigning == true && x.InputStream == content && x.AutoCloseStream == false), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task OpenReadAsync_ExistingObject_ReturnsStreamLengthAndType()
    {
        var stream = new MemoryStream([1, 2, 3]);
        _s3.GetObjectAsync(Bucket, "teacher-threads/a.ogg", Arg.Any<CancellationToken>()).Returns(new GetObjectResponse { ResponseStream = stream, ContentLength = 3 });

        await using var file = await _storage.OpenReadAsync("teacher-threads/a.ogg", TestContext.Current.CancellationToken);

        file!.Content.Should().BeSameAs(stream);
        (file.Length, file.ContentType).Should().Be((3L, "audio/ogg"));
    }

    [Fact]
    public async Task OpenReadAsync_MissingObject_ReturnsNull()
    {
        _s3.GetObjectAsync(Bucket, "teacher-threads/missing.ogg", Arg.Any<CancellationToken>()).ThrowsAsync(new AmazonS3Exception("missing") { StatusCode = HttpStatusCode.NotFound });

        var file = await _storage.OpenReadAsync("teacher-threads/missing.ogg", TestContext.Current.CancellationToken);

        file.Should().BeNull();
    }

    [Fact]
    public async Task OpenReadAsync_OtherS3Error_Propagates()
    {
        _s3.GetObjectAsync(Bucket, "teacher-threads/a.ogg", Arg.Any<CancellationToken>()).ThrowsAsync(new AmazonS3Exception("denied") { StatusCode = HttpStatusCode.Forbidden });

        var act = () => _storage.OpenReadAsync("teacher-threads/a.ogg", TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<AmazonS3Exception>()).Which.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task DeleteAsync_DeletesObjectFromBucket()
    {
        await _storage.DeleteAsync("training-exports/a.jsonl", TestContext.Current.CancellationToken);

        await _s3.Received(1).DeleteObjectAsync(Bucket, "training-exports/a.jsonl", Arg.Any<CancellationToken>());
    }
}
