using Core.Errors;
using Core.Identity.Tokens.CurrentUser;
using Core.Settings;
using Elmanhg.Application.Configuration.Shared;
using Elmanhg.Application.Exceptions;
using Elmanhg.Domain.RuntimeSettings;
using MediatR;
using Microsoft.Extensions.Caching.Memory;

namespace Elmanhg.Application.Configuration.UpdateRuntimeSetting;

public sealed class UpdateRuntimeSettingHandler(IRuntimeSettingOverrideRepository runtimeSettingOverrideRepository, RuntimeSettingRegistry registry, ICurrentUserService currentUserService, IMemoryCache memoryCache) : IRequestHandler<UpdateRuntimeSettingCommand, RuntimeSettingResult>
{
    public async Task<RuntimeSettingResult> Handle(UpdateRuntimeSettingCommand request, CancellationToken cancellationToken)
    {
        var userId = currentUserService.GetRequiredUserId(ErrorCodes.UserNotAuthenticated);
        var definition = registry.Find(request.Key) ?? throw new NotFoundCoreException(ErrorCodes.RuntimeSettingNotFound);
        var rows = await runtimeSettingOverrideRepository.GetAllAsync(cancellationToken).ConfigureAwait(false) ?? [];
        var candidate = RuntimeSettingValues.From(registry, rows).With(definition.Key, request.Value);
        registry.EnsureConstraintsHold(candidate);

        var row = rows.FirstOrDefault(x => x.Key == definition.Key);
        if (row is null)
        {
            row = RuntimeSettingOverride.Create(definition.Key, request.Value.GetRawText(), userId);
            await runtimeSettingOverrideRepository.AddAsync(row, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            row.Override(request.Value.GetRawText(), userId);
        }

        await runtimeSettingOverrideRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        memoryCache.Remove(RuntimeSettingsCache.Key);
        return RuntimeSettingResultGenerator.Generate(definition, candidate, row);
    }
}
