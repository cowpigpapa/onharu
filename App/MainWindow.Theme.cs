using System;
using System.Windows.Media;
using System.Collections.Generic;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows;

namespace FamilyPlanner
{
    public partial class MainWindow
    {
        readonly Dictionary<string, SolidColorBrush> themeBrushes = new Dictionary<string, SolidColorBrush>();
        static ControlTemplate colorCheckBoxTemplate;

        Brush T(string role)
        {
            SolidColorBrush brush;
            if (!themeBrushes.TryGetValue(role, out brush))
            {
                brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(OnharuThemePalette.For(settings.ThemeId)[role]));
                themeBrushes[role] = brush;
            }
            return brush;
        }

        void ApplyTheme(string id)
        {
            settings.ThemeId = OnharuThemePalette.Normalize(id);
            if (themeQuickSwitch != null) themeQuickSwitch.SetSelected(settings.ThemeId == "dark" ? 1 : 0, false);
            UpdateThemeQuickSwitchStyle();
            var palette = OnharuThemePalette.For(settings.ThemeId);
            foreach (var entry in themeBrushes) entry.Value.Color = (Color)ColorConverter.ConvertFromString(palette[entry.Key]);
            if (Application.Current != null)
            {
                Application.Current.Resources["OnharuPopupAccent"] = InterfaceAccentBrush();
                Application.Current.Resources["OnharuScrollThumb"] = Brush(OnharuStateColors.ScrollThumb(settings.ThemeId));
            }
            monthTitle.Foreground = BrandBrush(); selectedTitle.Foreground = T("Text");
            // 투명도 슬라이더는 브랜드 글자가 아니라 조작 컨트롤이다. MAIN_HEADER_UI_STANDARD의
            // 인터페이스 강조색 기준을 쓴다. 브랜드 그라데이션은 `온하루 · ONHARU`와 연·월 제목 전용이다.
            if (opacitySlider != null) opacitySlider.Foreground = OpacitySliderBrush();
            if (googleLoginButton != null)
            {
                googleLoginButton.Background = Brush(OnharuStateColors.GoogleButtonSurface(settings.ThemeId));
                googleLoginButton.BorderBrush = googleLoginButton.Background;
                googleLoginButton.Foreground = Brush(OnharuStateColors.GoogleButtonText(settings.ThemeId));
            }
            if (googleAccountCard != null) googleAccountCard.Background = OnharuStateColors.GoogleSurfaceBrush(settings.ThemeId);
            if (accountStatus != null && googleAccountCard != null) UpdateAccountStatus();
            ApplyNeutralSwitchPalette(calendarRangeSwitch);
            ApplyNeutralSwitchPalette(positionModeSwitch);
            ApplyDetailSwitchPalette(detailPeriodSwitch);
            StyleDetailHeaderActionButtons();
            StyleLightHeaderActionButton(searchButton, "⌕"); StyleLightHeaderActionButton(timetableButton, "▦");
            StyleLightHeaderActionButton(diaryButton, "◴"); StyleLightHeaderActionButton(sportsButton, "⚾");
            StyleLightHeaderActionButton(settingsButton, "settings");
            if (collapseSidebarButton != null) collapseSidebarButton.BorderBrush = T("Grid");
            StyleDetailScrollBar();
            foreach (var entry in filters)
            {
                var color = FilterColor(entry.Key, entry.Value);
                StyleVividCheckBox(entry.Value, color);
            }
            RenderAll();
        }

        void UpdateThemeQuickSwitchStyle()
        {
            if (themeQuickSwitch == null) return;
            ApplyNeutralSwitchPalette(themeQuickSwitch);
        }

        string InterfaceAccentColor()
        {
            return OnharuStateColors.ActionAccent(settings.ThemeId);
        }

        Brush InterfaceAccentBrush() { return Brush(InterfaceAccentColor()); }

        // 투명도 슬라이더는 2026-09-03에 브랜드 줄에서 슬라이딩 버튼 줄로 옮겼다.
        // 같은 줄의 중립 스위치와 색을 맞춘다. 보라 강조색은 그 줄에서 혼자 튄다.
        Brush OpacitySliderBrush()
        {
            return new SolidColorBrush(OnharuStateColors.NeutralSwitch(settings.ThemeId, true).Background);
        }

        string SupportAccentColor() { return OnharuStateColors.SupportAccent(settings.ThemeId); }

        void StyleDetailScrollBar()
        {
            if (detailScroll == null) return;
            detailScroll.Resources["OnharuScrollThumb"] = Brush(OnharuStateColors.DetailScrollThumb(settings.ThemeId, detailMode));
            detailScroll.Resources["OnharuScrollTrack"] = Brush(OnharuStateColors.DetailScrollTrack(settings.ThemeId));
            UiRound.SoftenScrollBars(detailScroll, true);
        }

        void ApplyActionSwitchPalette(OnharuSegmentedSwitch control)
        {
            if (control == null) return;
            control.SetPalette(Brush(OnharuStateColors.ActionFill(settings.ThemeId)), Brush(OnharuStateColors.ActionText(settings.ThemeId)),
                T("Button"), T("Text"), Brush(OnharuStateColors.ActionBorder(settings.ThemeId)));
        }

        void ApplyBrandSwitchPalette(OnharuSegmentedSwitch control)
        {
            if (control == null) return;
            control.SetPalette(BrandBrush(), Brushes.White, T("Button"), T("Text"), T("Grid"));
        }

        void ApplyNeutralSwitchPalette(OnharuSegmentedSwitch control)
        {
            if (control == null) return;
            var selected = OnharuStateColors.NeutralSwitch(settings.ThemeId, true);
            var inactive = OnharuStateColors.NeutralSwitch(settings.ThemeId, false);
            control.SetPalette(new SolidColorBrush(selected.Background), new SolidColorBrush(selected.Foreground),
                new SolidColorBrush(inactive.Background), new SolidColorBrush(inactive.Foreground), new SolidColorBrush(selected.Border));
        }

        void ApplyDetailSwitchPalette(OnharuSegmentedSwitch control)
        {
            if (control == null) return;
            var period = ReferenceEquals(control, detailPeriodSwitch);
            // 2026-09-03: 파스텔에서 `#4338CA` 평면 인디고를 하드코딩하고 있었다. 헤더 표준 문서는
            // `선택 상태는 현재 프리셋 대표색`을 규정하는데 프리셋과 무관한 고정값이었고,
            // 이 색은 대표 실행 버튼 면에서 걷어낸 계열이다. 인터페이스 강조색 역할색으로 바꾼다.
            var selected = period ? OnharuStateColors.DetailPeriodTab(settings.ThemeId, true) : OnharuStateColors.DetailTab(settings.ThemeId, true);
            var inactive = period ? OnharuStateColors.DetailPeriodTab(settings.ThemeId, false) : OnharuStateColors.DetailTab(settings.ThemeId, false);
            control.SetPalette(new SolidColorBrush(selected.Background), new SolidColorBrush(selected.Foreground),
                new SolidColorBrush(inactive.Background), new SolidColorBrush(inactive.Foreground), new SolidColorBrush(selected.Border));
            if (period) control.BorderBrush = new SolidColorBrush(OnharuStateColors.NeutralSwitch(settings.ThemeId, true).Border);
        }

        void StyleDetailHeaderActionButtons()
        {
            var border = T("Grid");
            var active = OnharuStateColors.DetailTab(settings.ThemeId, true);
            foreach (var button in new[] { detailAddButton, detailCategoryButton, detailTimeButton, detailIncompleteButton })
            {
                if (button == null) continue;
                button.Width = 18; button.MinWidth = 18; button.Height = 18; button.Padding = new Thickness(0);
                button.HorizontalContentAlignment = HorizontalAlignment.Center; button.VerticalContentAlignment = VerticalAlignment.Center;
                var selected = ReferenceEquals(button, detailIncompleteButton) ? detailIncompleteMode
                    : ReferenceEquals(button, detailTimeButton) ? !detailIncompleteMode && settings.DetailOrderMode == "time"
                    : ReferenceEquals(button, detailCategoryButton) && !detailIncompleteMode && settings.DetailOrderMode != "time";
                button.Background = selected ? new SolidColorBrush(active.Background) : T("Button");
                button.Foreground = selected ? new SolidColorBrush(active.Foreground) : T("Text");
                button.BorderBrush = selected ? new SolidColorBrush(active.Border) : border;
                // 글리프는 기본 크기를 쓴다. 12px로 줄여 봤으나 너무 작았다(2026-09-03 사용자 확인).
                // 네 버튼과 기념일 카드의 `+` 버튼이 모두 같은 18px 버튼에 같은 글리프 크기를 쓴다.
                button.Content = HeaderGlyph(ReferenceEquals(button, detailCategoryButton) ? "detail_category"
                    : ReferenceEquals(button, detailTimeButton) ? "detail_time"
                    : ReferenceEquals(button, detailIncompleteButton) ? "detail_incomplete" : "add", button.Foreground);
            }
        }

        void StyleHeaderActionButton(Button button, string glyph)
        {
            if (button == null) return;
            OnharuPopupChrome.NameFromToolTip(button);
            var foreground = Brush(OnharuStateColors.HeaderText(settings.ThemeId));
            button.Background = Brush(OnharuStateColors.HeaderSurface(settings.ThemeId));
            button.BorderBrush = Brush(OnharuStateColors.HeaderBorder(settings.ThemeId)); button.Foreground = foreground;
            button.Content = HeaderGlyph(glyph, foreground);
        }

        void StyleLightHeaderActionButton(Button button, string glyph)
        {
            if (button == null) return;
            OnharuPopupChrome.NameFromToolTip(button);
            if (settings.ThemeId == "dark") { StyleHeaderActionButton(button, glyph); return; }
            var foreground = Brush("#111827");
            button.Background = Brushes.White; button.BorderBrush = Brush("#D6DCE8"); button.Foreground = foreground;
            button.Content = HeaderGlyph(glyph, foreground);
        }

        string FilterColor(string key, CheckBox box)
        {
            string color;
            if (Colors.TryGetValue(key, out color)) return color;
            color = box == null ? null : box.Tag as string;
            if (!string.IsNullOrWhiteSpace(color))
            {
                try { ColorConverter.ConvertFromString(color); return color; }
                catch { }
            }
            return OnharuThemePalette.For(settings.ThemeId)["Accent"];
        }

        void StyleThemeCheckBox(CheckBox box, string color)
        {
            if (box == null) return;
            box.Template = ColorCheckBoxTemplate();
            box.Background = new SolidColorBrush(CategoryColorSystem.CheckBoxBackground(settings.ThemeId, color));
            box.BorderBrush = box.Background;
            // The label sits on the sidebar, not inside the colored square.
            // Dark skin therefore needs the shell text color even when the
            // optimal text over the checkbox square itself would be dark.
            box.Foreground = settings.ThemeId == "dark" ? T("Text")
                : new SolidColorBrush(CategoryColorSystem.Foreground(settings.ThemeId, color));
        }

        void StyleVividCheckBox(CheckBox box, string color)
        {
            StyleThemeCheckBox(box, OnharuColorPresets.VividColor(color));
        }

        static ControlTemplate ColorCheckBoxTemplate()
        {
            if (colorCheckBoxTemplate == null)
                colorCheckBoxTemplate = (ControlTemplate)XamlReader.Parse(@"<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' TargetType='{x:Type CheckBox}'><StackPanel Orientation='Horizontal'><Border x:Name='Box' Width='14' Height='14' CornerRadius='4' Background='{TemplateBinding Background}' BorderBrush='{TemplateBinding BorderBrush}' BorderThickness='1' VerticalAlignment='Center'><TextBlock x:Name='Tick' Text='✓' Foreground='White' FontWeight='Bold' FontSize='10' Width='14' Height='14' TextAlignment='Center' LineHeight='14' LineStackingStrategy='BlockLineHeight' HorizontalAlignment='Center' VerticalAlignment='Center' Visibility='Collapsed'/></Border><ContentPresenter Margin='4,0,0,0' VerticalAlignment='Center'/></StackPanel><ControlTemplate.Triggers><Trigger Property='IsChecked' Value='True'><Setter TargetName='Tick' Property='Visibility' Value='Visible'/></Trigger><Trigger Property='Tag' Value='Unavailable'><Setter TargetName='Tick' Property='Text' Value='×'/><Setter TargetName='Tick' Property='Margin' Value='0,-1,0,1'/><Setter TargetName='Tick' Property='Visibility' Value='Visible'/><Setter TargetName='Box' Property='BorderBrush' Value='White'/><Setter TargetName='Box' Property='BorderThickness' Value='1'/></Trigger><Trigger Property='IsEnabled' Value='False'><Setter Property='Opacity' Value='.38'/></Trigger></ControlTemplate.Triggers></ControlTemplate>");
            return colorCheckBoxTemplate;
        }

        Brush EventTextBrush(PlannerItem item)
        {
            if (item.Important)
            {
                var background = SafeColor(item.ImportantBackgroundColor, "#FFF1F7");
                var preferred = SafeColor(item.ImportantTextColor, "#F20D7A");
                return new SolidColorBrush(CategoryColorSystem.ReadableEmphasisForeground(background, preferred));
            }
            return new SolidColorBrush(CategoryColorSystem.Foreground(settings.ThemeId, ItemColor(item)));
        }

        Brush EventBackgroundBrush(PlannerItem item)
        {
            if (item.Important) return SafeBrush(item.ImportantBackgroundColor, "#FFF1F7");
            return new SolidColorBrush(CategoryColorSystem.Background(settings.ThemeId, ItemColor(item)));
        }

        // 더보기 팝업은 스킨과 상관없이 흰 팝업 위에 그린다. 달력용 색을 그대로 쓰면
        // 블랙 스킨의 밝은 글자색이 흰 배경에 얹혀 글씨가 사라진다(2026-09-03 사용자 보고).
        // 팝업 안에서는 언제나 밝은 스킨 기준으로 색을 계산한다.
        const string PopupThemeId = "classic";

        Brush PopupEventTextBrush(PlannerItem item)
        {
            if (item.Important)
            {
                var background = SafeColor(item.ImportantBackgroundColor, "#FFF1F7");
                var preferred = SafeColor(item.ImportantTextColor, "#F20D7A");
                return new SolidColorBrush(CategoryColorSystem.ReadableEmphasisForeground(background, preferred));
            }
            return new SolidColorBrush(CategoryColorSystem.Foreground(PopupThemeId, ItemColor(item)));
        }

        Brush PopupEventBackgroundBrush(PlannerItem item)
        {
            if (item.Important) return SafeBrush(item.ImportantBackgroundColor, "#FFF1F7");
            return new SolidColorBrush(CategoryColorSystem.Background(PopupThemeId, ItemColor(item)));
        }

        void StylePopupCheckBox(CheckBox box, string color)
        {
            if (box == null) return;
            box.Template = ColorCheckBoxTemplate();
            box.Background = new SolidColorBrush(CategoryColorSystem.CheckBoxBackground(PopupThemeId, color));
            box.BorderBrush = box.Background;
            box.Foreground = new SolidColorBrush(CategoryColorSystem.Foreground(PopupThemeId, color));
        }

        static Brush SafeBrush(string value, string fallback)
        {
            try { return Brush(string.IsNullOrWhiteSpace(value) ? fallback : value); }
            catch { return Brush(fallback); }
        }

        static Color SafeColor(string value, string fallback)
        {
            try { return (Color)ColorConverter.ConvertFromString(string.IsNullOrWhiteSpace(value) ? fallback : value); }
            catch { return (Color)ColorConverter.ConvertFromString(fallback); }
        }

        static Brush PastelBrush(Color color, double whiteRatio)
        {
            var r = (byte)(color.R + (255 - color.R) * whiteRatio);
            var g = (byte)(color.G + (255 - color.G) * whiteRatio);
            var b = (byte)(color.B + (255 - color.B) * whiteRatio);
            return new SolidColorBrush(Color.FromRgb(r, g, b));
        }

        static Brush PastelBrush(string hex, double whiteRatio)
        {
            return PastelBrush((Color)ColorConverter.ConvertFromString(hex), whiteRatio);
        }

        double Ui(double baseSize) { return baseSize * (settings.FontSize > 0 ? settings.FontSize / 12.0 : 1); }

        static Brush Brush(string hex) { return (Brush)new BrushConverter().ConvertFromString(hex); }
    }
}
