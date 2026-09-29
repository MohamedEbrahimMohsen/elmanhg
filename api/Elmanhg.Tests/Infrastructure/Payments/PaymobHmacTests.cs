using Elmanhg.Infrastructure.Payments.Paymob;
using Elmanhg.Tests.Fixtures.Paymob;
using FluentAssertions;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Elmanhg.Tests.Infrastructure.Payments;

public sealed class PaymobHmacTests
{
    private const string KnownAnswerKey = "paymob-known-answer-key";
    // Computed outside the code under test with Python's hmac/hashlib over the recorded payload (plan: "Known-answer digest").
    private const string ExpectedDigest = "7c23176724ebe1f1feffd884a6eab0a573e2e93ca0a9e19955f1e355b08ad40391f06d4dd4e34fc5ab0d2e968716b5b6de8b5d0735f468f6a4b62cef7a04a0cd";

    [Fact]
    public void Compute_RecordedTransaction_MatchesKnownDigest()
    {
        var digest = PaymobHmac.Compute(Transaction(PaymobPayloads.Read(PaymobPayloads.Succeeded)), KnownAnswerKey);

        digest.Should().Be(ExpectedDigest);
    }

    [Fact]
    public void IsValid_UpperCaseSignature_IsTrue()
    {
        var valid = PaymobHmac.IsValid(Transaction(PaymobPayloads.Read(PaymobPayloads.Succeeded)), KnownAnswerKey, ExpectedDigest.ToUpperInvariant());

        valid.Should().BeTrue();
    }

    [Fact]
    public void IsValid_TamperedAmount_IsFalse()
    {
        var payload = JsonNode.Parse(PaymobPayloads.Read(PaymobPayloads.Succeeded))!;
        payload["obj"]!["amount_cents"] = 100;

        var valid = PaymobHmac.IsValid(Transaction(payload.ToJsonString()), KnownAnswerKey, ExpectedDigest);

        valid.Should().BeFalse();
    }

    [Fact]
    public void IsValid_BlankSecret_IsFalse()
    {
        var transaction = Transaction(PaymobPayloads.Read(PaymobPayloads.Succeeded));

        var valid = PaymobHmac.IsValid(transaction, " ", PaymobHmac.Compute(transaction, " "));

        valid.Should().BeFalse();
    }

    [Fact]
    public void IsValid_MissingSignature_IsFalse()
    {
        var valid = PaymobHmac.IsValid(Transaction(PaymobPayloads.Read(PaymobPayloads.Succeeded)), KnownAnswerKey, null);

        valid.Should().BeFalse();
    }

    private static JsonElement Transaction(string payload) => JsonDocument.Parse(payload).RootElement.GetProperty("obj").Clone();
}
