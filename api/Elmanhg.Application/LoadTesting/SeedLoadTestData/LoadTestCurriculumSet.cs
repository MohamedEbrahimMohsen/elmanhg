using Elmanhg.Domain.ExamBlueprints;
using Elmanhg.Domain.Lessons;
using Elmanhg.Domain.Questions;
using Elmanhg.Domain.Subjects;
using Elmanhg.Domain.Teachers;
using Elmanhg.Domain.Units;

namespace Elmanhg.Application.LoadTesting.SeedLoadTestData;

public sealed record LoadTestCurriculumSet(Subject Subject, TeacherSubject Assignment, List<CurriculumUnit> Units, List<Lesson> Lessons, List<Question> Questions, List<ExamBlueprint> Blueprints);
