using Core.DDD.Models;
using Core.Notifications.Templates.AddNotificationTemplate;
using Core.Notifications.Templates.DeleteNotificationTemplate;
using Core.Notifications.Templates.GetNotificationTemplate;
using Core.Notifications.Templates.ListNotificationTemplates;
using Core.Notifications.Templates.UpdateNotificationTemplate;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Core.Notifications.Endpoints;

public static class CoreNotificationTemplateEndpoints
{
    public static RouteGroupBuilder MapCoreNotificationTemplateEndpoints(this IEndpointRouteBuilder endpoints, string adminPolicyName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(adminPolicyName);
        var group = endpoints.MapGroup("/c/notification-templates").RequireAuthorization(adminPolicyName);

        group.MapGet("/{code}", async ([FromRoute] string code, IMediator mediator, CancellationToken cancellationToken) =>
        {
            var results = await mediator.Send(new GetNotificationTemplateQuery(code), cancellationToken).ConfigureAwait(false);
            return Results.Ok(results);
        });

        group.MapGet("/", async (IMediator mediator, CancellationToken cancellationToken) =>
        {
            var results = await mediator.Send(new ListNotificationTemplatesQuery(), cancellationToken).ConfigureAwait(false);
            return Results.Ok(results);
        });

        group.MapPost("/", async ([FromBody] AddNotificationTemplateCommand request, IMediator mediator, CancellationToken cancellationToken) =>
        {
            await mediator.Send(request, cancellationToken).ConfigureAwait(false);
            return Results.Created();
        });

        group.MapPut("/{id}", async ([FromRoute] Guid id, [FromBody] UpdateNotificationTemplateRequest request, IMediator mediator, CancellationToken cancellationToken) =>
        {
            var command = new UpdateNotificationTemplateCommand(id, request.Title, request.Content, request.DeepLink, request.ImageUrl);
            await mediator.Send(command, cancellationToken).ConfigureAwait(false);
            return Results.NoContent();
        });

        group.MapDelete("/{id}", async ([FromRoute] Guid id, IMediator mediator, CancellationToken cancellationToken) =>
        {
            await mediator.Send(new DeleteNotificationTemplateCommand(id), cancellationToken).ConfigureAwait(false);
            return Results.NoContent();
        });

        return group;
    }
}

public sealed record UpdateNotificationTemplateRequest(LocalizedText Title, LocalizedText Content, string? DeepLink, string? ImageUrl);
