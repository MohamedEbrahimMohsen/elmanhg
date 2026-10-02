using Core.DDD.Models;
using Elmanhg.Application.Shared.Options;
using Microsoft.Extensions.Options;

namespace Elmanhg.Application.Shared.RuntimeSettings.Definitions;

public sealed class FeatureFlagRuntimeSettings(IOptions<ExamsOptions> examsOptions) : IRuntimeSettingDefinitions
{
    public static readonly RuntimeSettingKey<bool> ExamsRequireAllLessonsOpened = new("features.examsRequireAllLessonsOpened");
    public static readonly RuntimeSettingKey<bool> RefundsEnabled = new("features.refundsEnabled");
    public static readonly RuntimeSettingKey<bool> StudentsCanDeleteAvatarChats = new("features.studentsCanDeleteAvatarChats");

    public IReadOnlyList<RuntimeSettingDefinition> Definitions =>
    [
        RuntimeSettingDefinition.ForBoolean(ExamsRequireAllLessonsOpened, RuntimeSettingGroup.Features, examsOptions.Value.RequireAllLessonsOpened, new LocalizedText("اشتراط فتح كل دروس الوحدة قبل امتحانها", "Require opening every lesson before a unit exam"), new LocalizedText("عند التفعيل، لا يبدأ الطالب امتحان الوحدة حتى يفتح كل دروسها.", "When on, a student cannot start a unit exam until they have opened every lesson in the unit.")),
        RuntimeSettingDefinition.ForBoolean(RefundsEnabled, RuntimeSettingGroup.Features, false, new LocalizedText("السماح بالاسترداد من سجل المدفوعات", "Allow refunds from the payment log"), new LocalizedText("عند الإيقاف، لا يستطيع المدير استرداد أي دفع من التطبيق ويظهر زر الاسترداد معطّلًا. عمليات الاسترداد التي تتم من لوحة Paymob تُسجَّل دائمًا.", "When off, admins cannot refund a payment from the app and the Refund button is disabled. Refunds made in the Paymob dashboard are always recorded.")),
        RuntimeSettingDefinition.ForBoolean(StudentsCanDeleteAvatarChats, RuntimeSettingGroup.Features, true, new LocalizedText("السماح للطلاب بحذف محادثات المساعد", "Students can delete assistant chats"), new LocalizedText("عند الإيقاف، يرى الطالب محادثاته السابقة ويكملها لكن لا يستطيع حذفها. المحادثات المحذوفة سابقًا لا تعود.", "When off, students can still see and continue their past chats but cannot delete them. Chats already deleted do not come back.")),
    ];
}
