using Elmanhg.Application.TrainingExports.Shared;
using FluentAssertions;

namespace Elmanhg.Tests.Application.Features.TrainingExports.Shared;

public sealed class TrainingDataScrubberTests
{
    [Fact]
    public void ScrubText_Email_Replaced()
    {
        TrainingDataScrubber.ScrubText("راسلني على Ahmed.Ali+1@Example.com اليوم").Should().Be("راسلني على [email] اليوم");
    }

    [Fact]
    public void ScrubText_EgyptianMobileWithSpaces_Replaced()
    {
        TrainingDataScrubber.ScrubText("رقمي 010 1234 5678 شكرا").Should().Be("رقمي [number] شكرا");
    }

    [Fact]
    public void ScrubText_ArabicIndicPhone_Replaced()
    {
        TrainingDataScrubber.ScrubText("كلمني ٠١٠١٢٣٤٥٦٧٨").Should().Be("كلمني [number]");
    }

    [Theory]
    [InlineData("۰۱۰۱۲۳۴۵۶۷۸")]
    [InlineData("٠١٠ ١٢٣٤ ٥٦٧٨")]
    [InlineData("٠1٠1234٥678")]
    [InlineData("+٢٠ ١٠ ١٢٣٤ ٥٦٧٨")]
    public void ScrubText_MobileNumberInAnyDigitScript_Replaced(string number)
    {
        TrainingDataScrubber.ScrubText("كلمني " + number).Should().Be("كلمني [number]");
    }

    [Fact]
    public void ScrubText_InternationalPhone_Replaced()
    {
        TrainingDataScrubber.ScrubText("call +20 100 123 4567 now").Should().Be("call [number] now");
    }

    [Fact]
    public void ScrubText_NationalId_Replaced()
    {
        TrainingDataScrubber.ScrubText("الرقم القومي 29801011234567").Should().Be("الرقم القومي [number]");
    }

    [Fact]
    public void ScrubText_Url_Replaced()
    {
        TrainingDataScrubber.ScrubText("شوف https://example.com/a?b=1 و www.site.org/x").Should().Be("شوف [url] و [url]");
    }

    [Fact]
    public void ScrubText_Handle_Replaced()
    {
        TrainingDataScrubber.ScrubText("تابعني @ahmed_2010 على انستجرام").Should().Be("تابعني [handle] على انستجرام");
    }

    [Fact]
    public void ScrubText_ShortNumbersAndArabicText_Unchanged()
    {
        const string text = "الإجابة 9.8 م/ث² في 2024";

        TrainingDataScrubber.ScrubText(text).Should().Be(text);
    }

    [Fact]
    public void ScrubJson_IdentifyingKeysAtAnyDepth_Removed()
    {
        var json = """{"attemptId":"x","questionId":"3fa85f64-5717-4562-b3fc-2c963f66afa6","nested":{"StudentId":"y","items":[{"threadId":"z","keep":1}]}}""";

        TrainingDataScrubber.ScrubJson(json)!.ToJsonString().Should().Be("""{"questionId":"3fa85f64-5717-4562-b3fc-2c963f66afa6","nested":{"items":[{"keep":1}]}}""");
    }

    [Fact]
    public void ScrubJson_NestedStringValues_Scrubbed()
    {
        var json = """{"text":"ابعت على a@b.co","messages":[{"text":"01012345678","sentAt":"2026-01-02T03:04:05.123+00:00"}]}""";

        var scrubbed = TrainingDataScrubber.ScrubJson(json)!;

        scrubbed["text"]!.GetValue<string>().Should().Be("ابعت على [email]");
        scrubbed["messages"]![0]!["text"]!.GetValue<string>().Should().Be("[number]");
        scrubbed["messages"]![0]!["sentAt"]!.GetValue<string>().Should().Be("2026-01-02T03:04:05.123+00:00");
    }

    [Fact]
    public void ScrubJson_IsoShapedStudentText_Scrubbed()
    {
        var json = """{"text":"2990-10-12T34:56:78","messages":[{"text":"2026-01-02T03:04:05Z"}]}""";

        TrainingDataScrubber.ScrubJson(json)!.ToJsonString().Should().Be("""{"text":"[number]T34:56:78","messages":[{"text":"[number]T03:04:05Z"}]}""");
    }

    [Fact]
    public void ScrubJson_TimestampKeyWithOutOfRangeOrPrefixedValue_Scrubbed()
    {
        var json = """{"sentAt":"2990-10-12T34:56:78","askedAt":"2026-01-02T03:04:05Z 01012345678"}""";

        TrainingDataScrubber.ScrubJson(json)!.ToJsonString().Should().Be("""{"sentAt":"[number]T34:56:78","askedAt":"[number]T03:04:05Z [number]"}""");
    }

    [Fact]
    public void ScrubJson_GuidsInArraysAndWholeValues_Kept()
    {
        var json = """{"optionIds":["3fa85f64-5717-4562-b3fc-2c963f66afa6"],"text":"3fa85f64-5717-4562-b3fc-2c963f66afa6"}""";

        TrainingDataScrubber.ScrubJson(json)!.ToJsonString().Should().Be(json);
    }

    [Fact]
    public void ScrubJson_NonStringValues_Unchanged()
    {
        var json = """{"score":12345678,"ok":true,"none":null,"items":[1.5,2]}""";

        TrainingDataScrubber.ScrubJson(json)!.ToJsonString().Should().Be(json);
    }
}
