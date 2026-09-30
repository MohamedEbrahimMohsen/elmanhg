using Elmanhg.Domain.ExamBlueprints;
using Elmanhg.Domain.Identity;
using Elmanhg.Domain.Lessons;
using Elmanhg.Domain.Questions;
using Elmanhg.Domain.Subjects;
using Elmanhg.Domain.Subscriptions;
using Elmanhg.Domain.Teachers;
using Elmanhg.Domain.Units;
using MediatR;
using Microsoft.AspNetCore.Identity;

namespace Elmanhg.Application.LoadTesting.SeedLoadTestData;

public sealed class SeedLoadTestDataHandler(UserManager<User> userManager, ISubjectRepository subjectRepository, ICurriculumUnitRepository unitRepository, ILessonRepository lessonRepository, IQuestionRepository questionRepository, ITeacherSubjectRepository teacherSubjectRepository, IExamBlueprintRepository examBlueprintRepository, ISubscriptionRepository subscriptionRepository, TimeProvider timeProvider) : IRequestHandler<SeedLoadTestDataCommand, SeedLoadTestDataResult>
{
    private const int SubscriptionMonths = 12;

    public async Task<SeedLoadTestDataResult> Handle(SeedLoadTestDataCommand request, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var teacher = await LoadTestUsers.EnsureTeacherAsync(userManager, request.Key).ConfigureAwait(false);
        var subject = await subjectRepository.FirstOrDefaultAsync(x => x.Name == LoadTestData.SubjectName(request.Key), cancellationToken, asNoTracking: true).ConfigureAwait(false);
        Guid subjectId;
        var lessonsCreated = 0;
        var questionsCreated = 0;
        if (subject is null)
        {
            var set = LoadTestCurriculum.Build(request.Key, await subjectRepository.CountAsync(cancellationToken).ConfigureAwait(false) + 1, teacher);
            await subjectRepository.AddAsync(set.Subject, cancellationToken).ConfigureAwait(false);
            await teacherSubjectRepository.AddAsync(set.Assignment, cancellationToken).ConfigureAwait(false);
            await unitRepository.AddRangeAsync(set.Units, cancellationToken).ConfigureAwait(false);
            await lessonRepository.AddRangeAsync(set.Lessons, cancellationToken).ConfigureAwait(false);
            await questionRepository.AddRangeAsync(set.Questions, cancellationToken).ConfigureAwait(false);
            await examBlueprintRepository.AddRangeAsync(set.Blueprints, cancellationToken).ConfigureAwait(false);
            subjectId = set.Subject.Id;
            lessonsCreated = set.Lessons.Count;
            questionsCreated = set.Questions.Count;
        }
        else
        {
            subjectId = subject.Id;
        }

        var studentsCreated = 0;
        for (var index = 1; index <= request.StudentCount; index++)
        {
            var student = await LoadTestUsers.CreateStudentAsync(userManager, request.Key, index, request.StudentPassword, subjectId, now).ConfigureAwait(false);
            if (student is not null)
            {
                await subscriptionRepository.AddAsync(Subscription.Start(student.Id, SubscriptionPlan.Base, BillingPeriod.Yearly, SubscriptionMonths, now, LoadTestData.SubscriptionReference, null), cancellationToken).ConfigureAwait(false);
                studentsCreated++;
            }
        }

        await subjectRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return new SeedLoadTestDataResult(subjectId, lessonsCreated, questionsCreated, studentsCreated);
    }
}
