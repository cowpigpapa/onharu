using System;
using System.Threading.Tasks;
using System.Windows;

namespace FamilyPlanner
{
    public partial class MainWindow
    {
        async Task CheckForUpdatesAsync(bool manual)
        {
            if (!manual && (!settings.AutomaticUpdateChecks || DateTime.UtcNow - settings.LastUpdateCheckUtc < TimeSpan.FromHours(24))) return;
            try
            {
                var update = await UpdateService.CheckAsync();
                settings.LastUpdateCheckUtc = DateTime.UtcNow; Store.SaveSettings(settings);
                if (update == null) { if (manual) ShowNotice("현재 최신 버전을 사용하고 있습니다.", false, "업데이트 확인"); return; }
                var window = new UpdateAvailableWindow(update); PlaceCalendarDialog(window);
                ShowBlockingDialog(window);
                if (window.InstallerStarted) ExitApplication();
            }
            catch (Exception ex)
            {
                ErrorLog.Write("Check update", ex);
                if (manual) ShowNotice("업데이트 정보를 확인하지 못했습니다. 잠시 후 다시 시도해 주세요.", true, "업데이트 확인");
            }
        }
    }
}
