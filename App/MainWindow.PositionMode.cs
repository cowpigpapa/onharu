using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace FamilyPlanner
{
    public partial class MainWindow
    {
        void UpdateModeButtons()
        {
            if (lockButton == null) return;
            lockButton.Content = positionLocked ? "↔" : "📌";
            lockButton.ToolTip = positionLocked
                ? "달력의 위치와 크기를 조정합니다"
                : "현재 위치와 크기를 저장하고 바탕화면에 고정합니다";
            lockButton.Background = positionLocked ? Brush("#EEF2FF") : Brush("#4F46E5");
            lockButton.Foreground = positionLocked ? Brush("#4338CA") : Brushes.White;
            lockButton.BorderBrush = positionLocked ? Brush("#C7D2FE") : Brush("#4338CA");
            if (positionStatus != null)
            {
                positionStatus.Text = positionLocked ? "📌 고정됨" : "↔ 이동 가능";
                positionStatus.Foreground = positionLocked ? Brush("#16A34A") : Brush("#D97706");
            }
            if (resizeSurface != null && positionLocked) resizeSurface.Cursor = Cursors.Arrow;
            if (trayPositionItem != null) trayPositionItem.Text = positionLocked ? "위치·크기 조정" : "이 위치·크기로 고정";
        }
    }
}
