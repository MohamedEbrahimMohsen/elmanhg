using Elmanhg.Application.Shared.Messaging;
using Elmanhg.Application.TeacherThreads.ProcessTeacherThreadSla;
using Elmanhg.Domain.Identity;
using Elmanhg.Domain.TeacherThreads;
using Elmanhg.Infrastructure.Data.Context;
using Elmanhg.Tests.Builders;
using Elmanhg.Tests.Integration.Auth;
using Elmanhg.Tests.Integration.Authorization;
using Elmanhg.Tests.Integration.Infrastructure;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static Elmanhg.Tests.Integration.TeacherInbox.TeacherInboxTestData;

namespace Elmanhg.Tests.Integration.TeacherThreads;

public sealed class TeacherThreadSlaSweepTests(ApiFactory factory)
{
    private static readonly TimeSpan ReplySla = TimeSpan.FromHours(24);

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Process_OverdueThread_RecordsEachStageOnce()
    {
        var thread = await SeedAsync(new TeacherThreadBuilder().SubmittedAt(DateTimeOffset.UtcNow.AddHours(-25)));

        await ProcessAsync(thread.Id);
        await ProcessAsync(thread.Id);

        using var scope = factory.Services.CreateScope();
        var events = await scope.ServiceProvider.GetRequiredService<AppDbContext>().TeacherThreadSlaEvents.AsNoTracking().Where(x => x.ThreadId == thread.Id).ToListAsync(CancellationToken);
        events.Select(x => x.Kind).Should().BeEquivalentTo([TeacherThreadSlaEventKind.FirstReminder, TeacherThreadSlaEventKind.SecondReminder, TeacherThreadSlaEventKind.Breach]);
        events.Should().OnlyContain(x => x.SlaDueAt == thread.SlaDueAt);
    }

    [Fact]
    public async Task GetSlaDueIds_ListsDueThreadsAndSkipsRecordedAndAnswered()
    {
        var submittedAt = DateTimeOffset.UtcNow.AddDays(-3);
        var teacher = await ScopeTestData.SeedTeacherAsync(factory, CancellationToken);
        var due = await SeedAsync(new TeacherThreadBuilder().SubmittedAt(submittedAt));
        var recorded = await SeedAsync(new TeacherThreadBuilder().SubmittedAt(submittedAt));
        var answered = await SeedAsync(new TeacherThreadBuilder().SubmittedAt(submittedAt).AnsweredBy(teacher.Id));
        using (var seedScope = factory.Services.CreateScope())
        {
            var context = seedScope.ServiceProvider.GetRequiredService<AppDbContext>();
            context.TeacherThreadSlaEvents.Add(TeacherThreadSlaEvent.Record(recorded.Id, TeacherThreadSlaEventKind.FirstReminder, recorded.SlaDueAt, null, submittedAt.AddHours(12)));
            await context.SaveChangesAsync(CancellationToken);
        }

        using var scope = factory.Services.CreateScope();
        var ids = await scope.ServiceProvider.GetRequiredService<ITeacherThreadRepository>().GetSlaDueIdsAsync(due.SubmittedAt.AddHours(13), ReplySla, TimeSpan.FromHours(12), TimeSpan.FromHours(20), [], 100000, CancellationToken);

        ids.Should().Contain(due.Id);
        ids.Should().NotContain([recorded.Id, answered.Id]);
    }

    [Fact]
    public async Task Process_OverdueThreadProcessedTwice_SendsOutOfAppReminderOncePerChannel()
    {
        var (thread, teacher) = await SeedAssignedAsync("01012345678");

        await ProcessAsync(thread.Id);
        await ProcessAsync(thread.Id);

        var reminders = factory.Messages.RemindersFor(thread.Id);
        reminders.Select(x => x.Channel).Should().BeEquivalentTo([MessageChannel.WhatsApp, MessageChannel.Email]);
        reminders.Should().OnlyContain(x => x.Message.RecipientUserId == teacher.Id);
        reminders.Single(x => x.Channel == MessageChannel.WhatsApp).Message.Address.Should().Be("01012345678");
        (await MarkerCountAsync(thread.Id)).Should().Be(1);
    }

    [Fact]
    public async Task Process_TwoConcurrentSweeps_SendOutOfAppReminderOnce()
    {
        var (thread, _) = await SeedAssignedAsync("01012345678");

        var first = ProcessAsync(thread.Id);
        var second = ProcessAsync(thread.Id);
        await Task.WhenAll(first.ContinueWith(_ => { }, TaskScheduler.Default), second.ContinueWith(_ => { }, TaskScheduler.Default));

        factory.Messages.RemindersFor(thread.Id).Where(x => x.Channel == MessageChannel.Email).Should().ContainSingle();
        (await MarkerCountAsync(thread.Id)).Should().Be(1);
    }

    [Fact]
    public async Task Process_TeacherWithoutPhone_SendsEmailOnly()
    {
        var (thread, teacher) = await SeedAssignedAsync(phoneNumber: null);

        await ProcessAsync(thread.Id);

        factory.Messages.RemindersFor(thread.Id).Should().ContainSingle().Which.Should().Match<(MessageChannel Channel, TeacherThreadReminderMessage Message)>(x => x.Channel == MessageChannel.Email && x.Message.Address == teacher.Email);
    }

    private async Task<(TeacherThread Thread, User Teacher)> SeedAssignedAsync(string? phoneNumber)
    {
        var subjectId = await ScopeTestData.SeedSubjectAsync(factory, $"Physics {Guid.NewGuid():N}", CancellationToken);
        var teacher = User.CreateTeacher("Teacher", AuthTestClient.NewEmail());
        teacher.SetContactPhoneNumber(phoneNumber, Guid.NewGuid());
        await AuthTestClient.SeedUserAsync(factory, teacher, ScopeTestData.Password, suspended: false, CancellationToken);
        await ScopeTestData.AssignAsync(factory, teacher.Id, subjectId, CancellationToken);
        var student = await ScopeTestData.SeedStudentAsync(factory, CancellationToken);
        var thread = await SeedThreadAsync(factory, new TeacherThreadBuilder().SubmittedAt(DateTimeOffset.UtcNow.AddHours(-25)).ForStudent(student.Id).WithContext(ContextFor(subjectId)).Build());
        return (thread, teacher);
    }

    private async Task<int> MarkerCountAsync(Guid threadId)
    {
        using var scope = factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().TeacherThreadOutOfAppReminders.AsNoTracking().CountAsync(x => x.ThreadId == threadId, CancellationToken);
    }

    private async Task<TeacherThread> SeedAsync(TeacherThreadBuilder builder)
    {
        var subjectId = await ScopeTestData.SeedSubjectAsync(factory, $"Physics {Guid.NewGuid():N}", CancellationToken);
        var student = await ScopeTestData.SeedStudentAsync(factory, CancellationToken);
        return await SeedThreadAsync(factory, builder.ForStudent(student.Id).WithContext(ContextFor(subjectId)).Build());
    }

    private async Task ProcessAsync(Guid threadId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<ISender>().Send(new ProcessTeacherThreadSlaCommand(threadId), CancellationToken);
    }
}
