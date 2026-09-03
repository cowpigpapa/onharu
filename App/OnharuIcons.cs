using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace FamilyPlanner
{
    // 아이콘 도형의 단일 기준. 메인 헤더와 팝업 제목이 같은 도형을 쓴다.
    // 이전에는 메인이 벡터, 팝업 제목이 글꼴 기호라 같은 기능인데 서로 다른 그림이 나왔다.
    // design-onharu 9장의 `17×17px 벡터 경로와 약 1.8px 둥근 획`, `같은 기능은 메인과 팝업에서
    // 같은 아이콘을 사용한다`를 이 파일 하나로 지킨다.
    internal static class OnharuIcons
    {
        // 18×18 좌표계에 그린다. Viewbox가 실제 표시 크기로 맞춘다.
        const double DefaultThickness = 1.6;

        internal static bool IsFeature(string glyph)
        {
            return glyph == "▦" || glyph == "✎" || glyph == "◴" || glyph == "⚾";
        }

        internal static string Geometry(string glyph)
        {
            switch (glyph)
            {
                case "‹": case "❮": return "M11.5,3.5 L6.5,9 L11.5,14.5";
                case "›": case "❯": return "M6.5,3.5 L11.5,9 L6.5,14.5";
                case "«": return "M9,3.5 L4,9 L9,14.5 M14,3.5 L9,9 L14,14.5";
                case "»": return "M4,3.5 L9,9 L4,14.5 M9,3.5 L14,9 L9,14.5";
                case "window_minimize": return "M4,11.5 L14,11.5";
                case "window_maximize": return "M4.5,4.5 L13.5,4.5 L13.5,13.5 L4.5,13.5 Z";
                case "window_close": return "M5,5 L13,13 M13,5 L5,13";
                // 십자는 18칸 안에서 5~13(8칸)만 써서 형제 아이콘(3~15, 12칸)보다 작게 보였다.
                // 상세 헤더의 도구 셋과 기념일 카드의 만들기 버튼이 모두 같은 18px 버튼을 쓰므로
                // 획이 차지하는 넓이를 맞춘다(2026-09-03 사용자 확인). 4~14로 넓혔다.
                case "add": return "M9,4 L9,14 M4,9 L14,9";
                // 접기·펼치기 표시. 이전에는 사이드바 필터 그룹이 글꼴 문자 `▲`·`▼`를 썼다.
                case "chevron_up": return "M4.5,11 L9,6.5 L13.5,11";
                case "chevron_down": return "M4.5,7 L9,11.5 L13.5,7";
                case "detail_category": return "M3,4 L7,4 M9,4 L15,4 M3,9 L7,9 M9,9 L15,9 M3,14 L7,14 M9,14 L15,14";
                case "detail_time": return "M9,2.5 A6.5,6.5 0 1 0 9,15.5 A6.5,6.5 0 1 0 9,2.5 M9,5.5 L9,9 L12,10.5";
                case "detail_incomplete": return "M3,3.5 L7,3.5 L7,7.5 L3,7.5 Z M10,5.5 L15,5.5 M3,10.5 L7,10.5 L7,14.5 L3,14.5 Z M10,12.5 L15,12.5";
                case "range": return "M3,2 L15,9 L3,16 Z";
                case "important_day": return "M9,1.8 L11.1,6.4 L16.1,7 L12.4,10.4 L13.4,15.3 L9,12.8 L4.6,15.3 L5.6,10.4 L1.9,7 L6.9,6.4 Z";

                // 부가기능 아이콘. 획을 줄이고 안쪽 선을 덜어 작은 크기에서도 형태가 뭉치지 않게 한다.
                case "⌕": return "M8,3 A5,5 0 1 0 8,13 A5,5 0 1 0 8,3 M11.7,11.7 L15.6,15.6";
                // 시간표: 바깥 틀 + 머리줄 + 왼쪽 시간열 구분선 + 아래쪽 세로 한 줄.
                // 이전에는 머리줄 아래 세로 두 줄뿐이라 시간표가 아니라 그냥 3칸짜리 표로 읽혔다.
                // 시간열은 틀 위아래를 관통해야 시간표 구조가 드러나므로 3.5부터 15까지 긋는다.
                // 가로줄을 더 넣는 안은 21px에서 칸이 여섯이 되어 뭉치므로 쓰지 않는다.
                case "▦": return "M3,3.5 L15,3.5 L15,15 L3,15 Z M3,7.2 L15,7.2 M6.6,3.5 L6.6,15 M10.8,7.2 L10.8,15";
                // 알람: 둥근 종 둘 + 시계 + 바늘 + 다리 둘. 이전에는 종 귀가 가는 대각선이라
                // 종이 아니라 안테나처럼 붙어 보였고 다리가 없어 알람시계로 읽히지 않았다.
                // 종을 호로 그리면 21px에서도 덩어리로 보여 형태가 살고, 다리는 시계 원의 45도 지점에서 뻗는다.
                case "◴": return "M9,4.6 A5.6,5.6 0 1 0 9,15.8 A5.6,5.6 0 1 0 9,4.6"
                    + " M3.6,5.4 A2.6,2.6 0 0 1 7.0,2.9 M14.4,5.4 A2.6,2.6 0 0 0 11.0,2.9"
                    + " M5.04,14.16 L3.4,16.4 M12.96,14.16 L14.6,16.4"
                    + " M9,7.2 L9,10.4 L11.4,11.6";
                // 야구공: 원 + 좌우 실밥. 이전에는 벡터가 없어 글꼴 기호로 떨어져 혼자 달라 보였다.
                case "⚾": return "M9,2.8 A6.2,6.2 0 1 0 9,15.2 A6.2,6.2 0 1 0 9,2.8 M5.1,4.2 A6.6,6.6 0 0 1 5.1,13.8 M12.9,4.2 A6.6,6.6 0 0 0 12.9,13.8";
                // 일기: 펜촉. 일기장은 현재 기능에서 제외했지만 도형 기준은 남겨 둔다.
                case "✎": return "M4,14 L4,11.2 L12.2,3 L15,5.8 L6.8,14 Z M10.4,4.8 L13.2,7.6";
                // 설정: 톱니바퀴. 이전 도형은 바깥 링에 가는 선을 방사형으로 붙인 형태라
                // 톱니바퀴가 아니라 조타륜이나 햇살처럼 읽혔다. 실제 톱니를 가진 닫힌 윤곽으로 다시 그렸다.
                // 톱니는 여섯이다. 여덟이면 21px에서 톱니 하나가 2.5px까지 좁아져 1.6px 획에 뭉친다.
                // 이빨 뿌리 반지름 5.3, 이끝 반지름 7.4, 가운데 구멍 2.6. 좌표는 중심 (9,9) 기준 계산값이다.
                case "⚙": case "settings": return "M7.01,4.09 L7.21,1.82 L10.79,1.82 L10.99,4.09"
                    + " L12.26,4.82 L14.32,3.86 L16.11,6.96 L14.25,8.26 L14.25,9.74 L16.11,11.04"
                    + " L14.32,14.14 L12.26,13.18 L10.99,13.91 L10.79,16.18 L7.21,16.18 L7.01,13.91"
                    + " L5.74,13.18 L3.68,14.14 L1.89,11.04 L3.75,9.74 L3.75,8.26 L1.89,6.96"
                    + " L3.68,3.86 L5.74,4.82 Z"
                    + " M9,6.4 A2.6,2.6 0 1 0 9,11.6 A2.6,2.6 0 1 0 9,6.4";

                // 인쇄: 윗종이 + 본체 + 나오는 종이. 이전에는 SettingsWindow 안에 단일 실루엣으로
                // 하드코딩돼 있었고 안쪽 사각형이 본체와 겹쳐 형태가 뭉갰다.
                case "print": return "M5,7 L5,2.2 L13,2.2 L13,7"
                    + " M5,13.2 L2.2,13.2 L2.2,7 L15.8,7 L15.8,13.2 L13,13.2"
                    + " M5,10.8 L13,10.8 L13,15.8 L5,15.8 Z";

                // 달력: 틀 + 머리줄 + 위쪽 고리 둘. 일정 등록·수정 창의 `날짜 변경`이 쓴다.
                // 이전에는 컬러 이모지 `📅`라 스킨과 따로 놀았다.
                case "calendar": return "M3,4.5 L15,4.5 L15,15.5 L3,15.5 Z M3,8 L15,8"
                    + " M6.2,2.5 L6.2,6 M11.8,2.5 L11.8,6";

                // 정보: 원 + i. 이전에는 Segoe UI 글자 `i`를 TextBlock으로 그려 혼자 글꼴이었다.
                // 점은 둥근 끝 획이 그대로 점이 되도록 아주 짧은 선분으로 둔다.
                case "info": return "M9,2.2 A6.8,6.8 0 1 0 9,15.8 A6.8,6.8 0 1 0 9,2.2"
                    + " M9,5.5 L9,5.6 M9,8.3 L9,12.7";
                default: return null;
            }
        }

        static double Thickness(string glyph)
        {
            return glyph == "range" ? 1.2 : DefaultThickness;
        }

        // 메인 헤더 기준 크기. 헤더에 나란히 서는 다섯 아이콘은 모두 21px로 같다.
        // 설정만 17px이던 때가 있었는데 혼자 작아 보였다. 이동·달력 화살표 같은 보조 기호는 17px이다.
        internal static double HeaderSize(string glyph)
        {
            if (glyph == "range") return 19;
            return IsFeature(glyph) || glyph == "⌕" || glyph == "settings" || glyph == "⚙"
                || glyph == "print" || glyph == "info" || glyph == "calendar" ? 21 : 17;
        }

        internal static FrameworkElement Draw(string glyph, Brush foreground)
        {
            return Draw(glyph, foreground, HeaderSize(glyph));
        }

        // 도형이 없는 기호는 글꼴로 그린다. 새 아이콘은 여기 도형을 먼저 추가해 글꼴로 떨어지지 않게 한다.
        internal static FrameworkElement Draw(string glyph, Brush foreground, double size)
        {
            var geometry = Geometry(glyph);
            if (geometry == null)
                return new Viewbox
                {
                    Width = size, Height = size, Stretch = Stretch.Uniform,
                    HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
                    Child = new TextBlock { Text = glyph, Foreground = foreground, FontFamily = new FontFamily("Segoe UI Symbol"),
                        FontWeight = FontWeights.SemiBold, Padding = new Thickness(0), Margin = new Thickness(0),
                        TextAlignment = TextAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center }
                };

            var path = new Path
            {
                Data = System.Windows.Media.Geometry.Parse(geometry), Stroke = foreground, StrokeThickness = Thickness(glyph),
                StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round,
                StrokeLineJoin = PenLineJoin.Round, Fill = glyph == "important_day" ? foreground : Brushes.Transparent,
                Width = 18, Height = 18, Stretch = Stretch.None
            };
            return new Viewbox
            {
                Width = size, Height = size, Stretch = Stretch.Uniform, Child = path,
                RenderTransform = glyph == "range" ? new TranslateTransform(0, 1) : null,
                HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center
            };
        }
    }
}
