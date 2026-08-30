using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace FamilyPlanner
{
    public partial class MainWindow
    {
        void UpdateModeButtons()
        {
            if (positionModeSwitch == null) return;
            positionModeSwitch.ToolTip = positionLocked
                ? "달력의 위치와 크기를 조정합니다"
                : "현재 위치와 크기를 저장하고 바탕화면에 고정합니다";
            ApplyNeutralSwitchPalette(positionModeSwitch);
            var targetMode = positionLocked ? 1 : 0;
            // The fixed Explorer frame is a snapshot. Animation would publish its
            // first frame and leave the thumb behind until the next calendar click.
            positionModeSwitch.SetSelected(targetMode, false);
            TemporarySegmentPaletteTool.ApplyOverride(positionModeSwitch);
            if (resizeSurface != null && positionLocked) resizeSurface.Cursor = Cursors.Arrow;
            if (trayPositionItem != null) trayPositionItem.Text = positionLocked ? "위치·크기 조정" : "이 위치·크기로 고정";
            if (windowMaximizeButton != null) { windowMaximizeButton.IsEnabled = !positionLocked; windowMaximizeButton.Opacity = positionLocked ? .4 : 1; }
        }
    }
}
