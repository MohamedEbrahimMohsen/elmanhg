using Elmanhg.Domain.Lessons;
using Elmanhg.Domain.Questions;

namespace Elmanhg.Application.LoadTesting.SeedLoadTestData;

public static class LoadTestLessonContent
{
    private const int SectionCount = 3;
    private const int ParagraphsPerSection = 4;
    private const string OptionIds = "abcd";
    private const string Sentence = "تتناول هذه الفقرة مفهوما أساسيا من مفاهيم الدرس، وتشرح العلاقة بين الكميات الفيزيائية بأمثلة من الحياة اليومية حتى يسهل على الطالب فهمها وتطبيقها.";
    private const string InlineFormula = """<p>القانون الثاني: <span data-type="inline-math" data-latex="F = m a"></span></p>""";
    private const string BlockFormula = """<div data-type="block-math" data-latex="E_k = \frac{1}{2} m v^2"></div>""";

    public static string Explanation(int lessonOrder)
    {
        var sections = Enumerable.Range(1, SectionCount)
            .Select(section => $"<h2>القسم {section}</h2>" + string.Concat(Enumerable.Repeat($"<p>{Sentence}</p>", ParagraphsPerSection)));
        var math = lessonOrder % 2 == 0 ? InlineFormula + BlockFormula : string.Empty;
        return string.Concat(sections) + math;
    }

    public static string Summary() => $"<p>{Sentence}</p>";

    public static IReadOnlyList<LessonObjectiveContent> Objectives() =>
    [
        new LessonObjectiveContent(null, "يفهم الطالب المفهوم الأساسي للدرس."),
        new LessonObjectiveContent(null, "يطبق الطالب القانون على مسائل بسيطة."),
        new LessonObjectiveContent(null, "يحلل الطالب النتائج ويفسرها."),
    ];

    public static QuestionContent Mcq(int index)
    {
        var options = string.Join(",", OptionIds.Select(id => $$"""{"id":"{{id}}","text":"الاختيار {{id}}"}"""));
        return new QuestionContent($"<p>سؤال {index + 1}: اختر الإجابة الصحيحة.</p>", $$"""{"options":[{{options}}]}""", $$"""{"correctOptionId":"{{OptionIds[index % OptionIds.Length]}}"}""", "<p>راجع شرح الدرس لمعرفة السبب.</p>", 1);
    }

    public static QuestionDifficulty Difficulty(int index) => (QuestionDifficulty)(index % Enum.GetValues<QuestionDifficulty>().Length);
}
