using Elmanhg.Application.Questions.Shared;
using Elmanhg.Application.Shared.RichText;
using FluentAssertions;
using NSubstitute;

namespace Elmanhg.Tests.Application.Features.Questions.Shared;

public sealed class QuestionContentFactoryTests
{
    [Fact]
    public void CreateContent_Always_SanitisesStemAndExplanation()
    {
        var sanitizer = Substitute.For<IRichTextSanitizer>();
        sanitizer.Sanitize(Arg.Any<string?>()).Returns(x => $"clean:{x.Arg<string?>()}");

        var content = QuestionContentFactory.CreateContent(QuestionFieldsValidatorTests.ValidMcq() with { MaxScore = 4 }, sanitizer);

        content.Stem.Should().Be("clean:<p>2 + 2 = ?</p>");
        content.Explanation.Should().Be("clean:<p>Add.</p>");
        content.MaxScore.Should().Be(4);
    }

    [Fact]
    public void CreateMetadata_Tags_TrimsAndDeduplicatesIgnoringCase()
    {
        var metadata = QuestionContentFactory.CreateMetadata(QuestionFieldsValidatorTests.ValidMcq() with { Tags = [" Kinematics ", "kinematics", "SI"] });

        metadata.Tags.Should().Equal("Kinematics", "SI");
    }
}
