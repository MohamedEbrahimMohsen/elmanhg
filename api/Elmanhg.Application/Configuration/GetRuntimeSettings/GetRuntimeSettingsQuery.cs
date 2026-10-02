using Elmanhg.Application.Configuration.Shared;
using MediatR;

namespace Elmanhg.Application.Configuration.GetRuntimeSettings;

public sealed record GetRuntimeSettingsQuery : IRequest<List<RuntimeSettingGroupResult>>;
