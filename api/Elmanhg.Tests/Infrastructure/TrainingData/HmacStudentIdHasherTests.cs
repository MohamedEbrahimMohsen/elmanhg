using Elmanhg.Infrastructure.TrainingData;
using FluentAssertions;
using Microsoft.Extensions.Options;
using System.Security.Cryptography;
using System.Text;

namespace Elmanhg.Tests.Infrastructure.TrainingData;

public sealed class HmacStudentIdHasherTests
{
    private const string Key = "elmanhg-tests-hasher-key-0123456789abcdef";
    private static readonly Guid StudentId = Guid.Parse("7b0f3c1e-2d4a-4f6b-8c9d-0e1f2a3b4c5d");

    [Fact]
    public void Hash_SameIdTwice_ReturnsSameValue()
    {
        var hasher = Hasher(Key);

        var first = hasher.Hash(StudentId);

        hasher.Hash(StudentId).Should().Be(first);
    }

    [Fact]
    public void Hash_Always_Returns64LowerCaseHexCharacters()
    {
        var hash = Hasher(Key).Hash(StudentId);

        hash.Should().MatchRegex("^[0-9a-f]{64}$");
    }

    [Fact]
    public void Hash_DifferentKeys_ReturnDifferentValues()
    {
        var hash = Hasher(Key).Hash(StudentId);

        hash.Should().NotBe(Hasher(Key + "-rotated").Hash(StudentId));
    }

    [Fact]
    public void Hash_Always_DiffersFromUnkeyedSha256OfId()
    {
        var hash = Hasher(Key).Hash(StudentId);

        hash.Should().NotBe(Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(StudentId.ToString("D")))));
    }

    [Fact]
    public void Hash_EmptyKey_UsesDevelopmentKey()
    {
        var hash = Hasher(string.Empty).Hash(StudentId);

        hash.Should().Be(Hasher(TrainingDataOptions.DevelopmentStudentIdHashKey).Hash(StudentId));
    }

    [Fact]
    public void HashSourceId_KnownKey_MatchesHmacOfScopeAndId()
    {
        var hash = Hasher(Key).HashSourceId("teacher-thread", StudentId);

        hash.Should().Be(Convert.ToHexStringLower(HMACSHA256.HashData(Encoding.UTF8.GetBytes(Key), Encoding.UTF8.GetBytes("teacher-thread:7b0f3c1e-2d4a-4f6b-8c9d-0e1f2a3b4c5d"))));
    }

    [Fact]
    public void HashSourceId_SameIdDifferentScopes_Differ()
    {
        var hasher = Hasher(Key);

        var thread = hasher.HashSourceId("teacher-thread", StudentId);

        thread.Should().NotBe(hasher.HashSourceId("avatar-conversation", StudentId)).And.NotBe(hasher.Hash(StudentId));
    }

    private static HmacStudentIdHasher Hasher(string key) => new(Options.Create(new TrainingDataOptions { StudentIdHashKey = key }));
}
