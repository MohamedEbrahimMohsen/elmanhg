using Elmanhg.Domain.Lessons;
using Elmanhg.Domain.Questions;
using Elmanhg.Domain.Sessions;
using Elmanhg.Domain.Subjects;
using Elmanhg.Domain.Units;

namespace Elmanhg.Application.TeacherThreads.Shared;

public sealed record TeacherThreadContextSources(ILessonRepository Lessons, ICurriculumUnitRepository Units, ISubjectRepository Subjects, IQuestionRepository Questions, ISessionRepository Sessions);
