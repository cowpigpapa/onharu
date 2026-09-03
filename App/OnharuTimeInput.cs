using System;
using System.Linq;
using System.Windows.Controls;
using System.Windows.Input;

namespace FamilyPlanner
{
    // 24시간 시각 입력의 공통 규칙. 시간표 설정의 시작 시각과 알람의 시각 알람이 같은 방식으로 읽는다.
    // 두 화면에서 같은 문제가 생겨 공통으로 뺐다. 새 시각 입력칸도 Attach 한 줄로 같은 동작을 얻는다.
    internal static class OnharuTimeInput
    {
        // 콜론이 있으면 그대로 파싱하고, 숫자만 있으면 자리수로 시·분을 나눈다.
        // `9`→09:00, `900`→09:00, `1000`→10:00, `0930`→09:30.
        // TimeSpan.TryParse는 콜론 없는 `1000`을 1000일로 읽으므로 숫자만 있는 입력에는 쓰지 않는다.
        internal static bool TryParse(string text, out TimeSpan value)
        {
            value = TimeSpan.Zero;
            var raw = (text ?? "").Trim();
            if (raw.Length == 0) return false;
            if (raw.IndexOf(':') >= 0)
                return TimeSpan.TryParse(raw, out value) && value.Days == 0 && value.TotalHours < 24;
            var digits = new string(raw.Where(char.IsDigit).ToArray());
            if (digits.Length == 0 || digits.Length > 4 || digits.Length != raw.Length) return false;
            int hour, minute;
            if (digits.Length <= 2) { hour = int.Parse(digits); minute = 0; }
            else { hour = int.Parse(digits.Substring(0, digits.Length - 2)); minute = int.Parse(digits.Substring(digits.Length - 2)); }
            if (hour > 23 || minute > 59) return false;
            value = new TimeSpan(hour, minute, 0); return true;
        }

        internal static string Format(TimeSpan value)
        {
            return value.Hours.ToString("00") + ":" + value.Minutes.ToString("00");
        }

        // 숫자와 콜론만 받고, 칸을 벗어나거나 Enter를 누르면 곧바로 HH:mm으로 정리한다.
        // 읽을 수 없는 값은 fallback으로 되돌리되 그 결과를 바로 보여 준다.
        internal static void Attach(TextBox box, TimeSpan fallback)
        {
            if (box == null) return;
            box.ToolTip = "24시간 형식. 900, 0900, 9:00, 9 모두 09:00으로 읽습니다.";
            box.PreviewTextInput += delegate(object sender, TextCompositionEventArgs e)
            { e.Handled = e.Text.Any(x => !char.IsDigit(x) && x != ':'); };
            box.LostFocus += delegate { Normalize(box, fallback); };
            box.KeyDown += delegate(object sender, KeyEventArgs e)
            { if (e.Key == Key.Enter) { Normalize(box, fallback); box.SelectAll(); e.Handled = true; } };
        }

        internal static TimeSpan Normalize(TextBox box, TimeSpan fallback)
        {
            TimeSpan parsed;
            if (!TryParse(box.Text, out parsed)) parsed = fallback;
            box.Text = Format(parsed);
            return parsed;
        }
    }
}
