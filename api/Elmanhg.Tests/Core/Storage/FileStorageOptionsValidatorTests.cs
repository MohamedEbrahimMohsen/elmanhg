using Core.Storage;
using FluentAssertions;

namespace Elmanhg.Tests.Core.Storage;

public sealed class FileStorageOptionsValidatorTests
{
    private readonly FileStorageOptionsValidator _validator = new();

    [Fact]
    public void Validate_Local_Succeeds()
    {
        var result = _validator.Validate(null, new FileStorageOptions { Provider = FileStorageProvider.Local, LocalRootPath = "media", PublicBaseUrl = "/api/media" });

        result.Succeeded.Should().BeTrue();
    }

    [Fact]
    public void Validate_CompleteS3_Succeeds()
    {
        var result = _validator.Validate(null, CompleteS3());

        result.Succeeded.Should().BeTrue();
    }

    [Theory]
    [InlineData(nameof(FileStorageOptions.S3BucketName))]
    [InlineData(nameof(FileStorageOptions.S3AccessKeyId))]
    [InlineData(nameof(FileStorageOptions.S3SecretAccessKey))]
    [InlineData(nameof(FileStorageOptions.S3Region))]
    public void Validate_S3MissingSetting_Fails(string key)
    {
        var options = CompleteS3();
        typeof(FileStorageOptions).GetProperty(key)!.SetValue(options, " ");

        var result = _validator.Validate(null, options);

        result.Failures.Should().ContainSingle().Which.Should().Be($"FileStorage:{key} is required when the S3 provider is selected.");
    }

    [Theory]
    [InlineData("http://account.r2.cloudflarestorage.com")]
    [InlineData("account.r2.cloudflarestorage.com")]
    public void Validate_S3NonHttpsServiceUrl_Fails(string serviceUrl)
    {
        var options = CompleteS3();
        options.S3ServiceUrl = serviceUrl;

        var result = _validator.Validate(null, options);

        result.Failures.Should().ContainSingle().Which.Should().Contain("FileStorage:S3ServiceUrl");
    }

    private static FileStorageOptions CompleteS3() => new()
    {
        Provider = FileStorageProvider.S3,
        LocalRootPath = "media",
        PublicBaseUrl = "/api/media",
        S3ServiceUrl = "https://account.r2.cloudflarestorage.com",
        S3Region = "auto",
        S3BucketName = "elmanhg-media",
        S3AccessKeyId = "not-a-secret-access-key",
        S3SecretAccessKey = "not-a-secret-secret-key",
    };
}
