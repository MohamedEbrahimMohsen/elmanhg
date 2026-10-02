using Core.EntityFrameworkCore.Repositories;
using Elmanhg.Domain.RuntimeSettings;
using Elmanhg.Infrastructure.Data.Context;

namespace Elmanhg.Infrastructure.RuntimeSettings;

public class RuntimeSettingOverrideRepository(AppDbContext context) : Repository<RuntimeSettingOverride>(context), IRuntimeSettingOverrideRepository { }
