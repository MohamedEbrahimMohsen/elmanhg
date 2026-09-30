using Elmanhg.Application.TrainingExports.RunTrainingExport;
using Elmanhg.Domain.TrainingData;
using FluentAssertions;
using System.Security.Cryptography;
using System.Text;

namespace Elmanhg.Tests.Application.Features.TrainingExports.RunTrainingExport;

public sealed class TrainingExportJsonlWriterTests
{
    private sealed record Line(string StudentText, TeacherThreadTrainingTrigger Trigger, int? Rating);

    [Fact]
    public async Task WriteAsync_TwoLines_WritesNewlineDelimitedCamelCaseJson()
    {
        using var stream = new MemoryStream();
        using var writer = new TrainingExportJsonlWriter(stream);

        await writer.WriteAsync(new Line("a", TeacherThreadTrainingTrigger.Closed, null), TestContext.Current.CancellationToken);
        await writer.WriteAsync(new Line("b", TeacherThreadTrainingTrigger.RatedAfterClose, 5), TestContext.Current.CancellationToken);

        Encoding.UTF8.GetString(stream.ToArray()).Should().Be("{\"studentText\":\"a\",\"trigger\":\"Closed\",\"rating\":null}\n{\"studentText\":\"b\",\"trigger\":\"RatedAfterClose\",\"rating\":5}\n");
        writer.RowCount.Should().Be(2);
    }

    [Fact]
    public async Task Sha256Hex_AfterWrites_MatchesContentHash()
    {
        using var stream = new MemoryStream();
        using var writer = new TrainingExportJsonlWriter(stream);
        await writer.WriteAsync(new Line("a", TeacherThreadTrainingTrigger.Closed, 1), TestContext.Current.CancellationToken);
        await writer.WriteAsync(new Line("b", TeacherThreadTrainingTrigger.Closed, 2), TestContext.Current.CancellationToken);

        var hash = writer.Sha256Hex();

        hash.Should().Be(Convert.ToHexStringLower(SHA256.HashData(stream.ToArray())));
    }

    [Fact]
    public async Task WriteAsync_ArabicText_WritesUnescapedUtf8()
    {
        using var stream = new MemoryStream();
        using var writer = new TrainingExportJsonlWriter(stream);

        await writer.WriteAsync(new Line("ما هو القصور الذاتي؟", TeacherThreadTrainingTrigger.Closed, null), TestContext.Current.CancellationToken);

        var bytes = stream.ToArray();
        Encoding.UTF8.GetString(bytes).Should().Contain("\"studentText\":\"ما هو القصور الذاتي؟\"");
        bytes.Take(3).Should().NotEqual(new byte[] { 0xEF, 0xBB, 0xBF });
    }
}
