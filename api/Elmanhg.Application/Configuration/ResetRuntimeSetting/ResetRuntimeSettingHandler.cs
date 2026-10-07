using Core.Errors;
using Core.Identity.Tokens.CurrentUser;
using Core.Settings;
using Elmanhg.Application.Configuration.Shared;
using Elmanhg.Application.Exceptions;
using Elmanhg.Domain.RuntimeSettings;
using MediatR;
using Microsoft.Extensions.Caching.Memory;

namespace Elmanhg.Application.Configuration.ResetRuntimeSetting;

public sealed class ResetRuntimeSettingHandler(IRuntimeSettingOverrideRepository runtimeSettingOverrideRepository, RuntimeSettingRegistry registry, ICurrentUserService currentUserService, IMemoryCache memoryCache) : IRequestHandler<ResetRuntimeSettingCommand, RuntimeSettingResult>
{
    public async Task<RuntimeSettingResult> Handle(ResetRuntimeSettingCommand request, CancellationToken cancellationToken)
    {
        var userId = currentUserService.GetRequiredUserId(ErrorCodes.UserNotAuthenticated);
        var definition = registry.Find(request.Key) ?? throw new NotFoundCoreException(ErrorCodes.RuntimeSettingNotFound);
        var rows = await runtimeSettingOverrideRepository.GetAllAsync(cancellationToken).ConfigureAwait(false) ?? [];
        var values = RuntimeSettingValues.From(registry, rows);
        var row = rows.FirstOrDefault(x => x.Key == definition.Key);
        if (row?.Value is null)
        {
            return RuntimeSettingResultGenerator.Generate(definition, values, row);
        }

        var candidate = values.With(definition.Key, definition.DefaultValue);
        registry.EnsureConstraintsHold(candidate);
        row.Reset(userId);

        await runtimeSettingOverrideRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        memoryCache.Remove(RuntimeSettingsCache.Key);
        return RuntimeSettingResultGenerator.Generate(definition, candidate, row);
    }
}
