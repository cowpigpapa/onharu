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
            positionModeSwitch.SetAccent(positionLocked ? "#22C55E" : "#F59E0B");
            var targetMode = positionLocked ? 1 : 0;
            positionModeSwitch.SetSelected(targetMode, positionModeSwitch.SelectedIndex != targetMode);
            if (resizeSurface != null && positionLocked) resizeSurface.Cursor = Cursors.Arrow;
            if (trayPositionItem != null) trayPositionItem.Text = positionLocked ? "위치·크기 조정" : "이 위치·크기로 고정";
        }
    }
}
