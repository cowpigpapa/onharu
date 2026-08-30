using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Runtime.CompilerServices;
using System.Collections.Generic;
using System.Windows.Shapes;

namespace FamilyPlanner
{
    // Development-only color probe. Attach/detach with one line per control.
    static class TemporarySegmentPaletteTool
    {
        sealed class IconColors { internal DependencyObject Element; internal Brush Foreground; internal Brush Stroke; internal Brush Fill; }
        sealed class OriginalColors
        {
            internal Brush Background; internal Brush Foreground; internal Brush Border;
            internal readonly List<IconColors> Icons = new List<IconColors>();
        }
        sealed class ColorOverride { internal Brush Background; internal Brush Foreground; internal Brush Border; internal string ThemeId; }
        static readonly ConditionalWeakTable<DependencyObject, OriginalColors> Originals = new ConditionalWeakTable<DependencyObject, OriginalColors>();
        static readonly ConditionalWeakTable<DependencyObject, ColorOverride> Overrides = new ConditionalWeakTable<DependencyObject, ColorOverride>();
        static readonly Dictionary<string, ColorOverride> GroupOverrides = new Dictionary<string, ColorOverride>();
        static PlannerSettings paletteSettings;
        static bool initialized;
        static Popup activePopup;
        internal static bool Enabled { get; set; }

        internal static void Initialize(PlannerSettings settings)
        {
            paletteSettings = settings;
            Enabled = settings != null && settings.EnableButtonColorTool;
            if (initialized) return;
            initialized = true;
            EventManager.RegisterClassHandler(typeof(Button), UIElement.PreviewMouseRightButtonUpEvent,
                new MouseButtonEventHandler(ButtonRightClick), true);
            EventManager.RegisterClassHandler(typeof(Window), UIElement.PreviewMouseDownEvent,
                new MouseButtonEventHandler(ClosePaletteOutside), true);
        }

        static void ClosePaletteOutside(object sender, MouseButtonEventArgs e)
        {
            if (activePopup == null || !activePopup.IsOpen) return;
            var source = e.OriginalSource as DependencyObject;
            for (var current = source; current != null; current = VisualTreeHelper.GetParent(current))
                if (ReferenceEquals(current, activePopup.Child)) return;
            activePopup.IsOpen = false;
        }

        internal static void PromoteStableId(FrameworkElement control, string automationId)
        {
            if (control == null || string.IsNullOrWhiteSpace(automationId)) return;
            var oldKey = PersistentKey(control);
            System.Windows.Automation.AutomationProperties.SetAutomationId(control, automationId);
            var newKey = PersistentKey(control);
            string stored;
            if (oldKey != null && newKey != null && oldKey != newKey && paletteSettings != null
                && paletteSettings.ButtonColorOverrides != null
                && paletteSettings.ButtonColorOverrides.TryGetValue(oldKey, out stored)
                && !paletteSettings.ButtonColorOverrides.ContainsKey(newKey))
            {
                paletteSettings.ButtonColorOverrides[newKey] = stored;
                paletteSettings.ButtonColorOverrides.Remove(oldKey);
                Store.SaveSettings(paletteSettings);
            }
        }

        internal static void Attach(OnharuSegmentedSwitch control)
        {
            if (control == null) return;
            // Open after the context click has completed. Opening on button-down
            // made the following button-up look like an outside click to Popup,
            // so the palette disappeared unless the button stayed held down.
            control.PreviewMouseRightButtonUp += delegate(object sender, MouseButtonEventArgs e)
            {
                if (!Enabled) return;
                Open(control); e.Handled = true;
            };
        }

        internal static void Attach(Slider control)
        {
            if (control == null) return;
            control.PreviewMouseRightButtonUp += delegate(object sender, MouseButtonEventArgs e)
            {
                if (!Enabled) return;
                var original = Originals.GetValue(control, delegate { return new OriginalColors { Background = control.Foreground, Foreground = Brushes.White, Border = control.Foreground }; });
                Open(control, control.Foreground, Brushes.White, control.Foreground,
                    delegate(Brush background, Brush foreground, Brush border) { control.Foreground = background; }, original);
                e.Handled = true;
            };
            ApplyOverride(control);
        }

        internal static void Attach(Button control)
        {
            if (control == null) return;
            ApplyOverride(control);
            ColorOverride saved;
            if (!TryPersistentOverride(control, out saved)) return;
            ApplyButtonSurface(control, saved.Background, saved.Foreground, saved.Border);
        }

        internal static void Attach(ScrollViewer control)
        {
            if (control == null) return;
            control.AddHandler(UIElement.PreviewMouseRightButtonUpEvent, new MouseButtonEventHandler(delegate(object sender, MouseButtonEventArgs e)
            {
                if (!Enabled) return;
                if (IsWithinDetailCard(e.OriginalSource as DependencyObject)) return;
                if (FindIndependentSurface(e.OriginalSource as DependencyObject) != null) return;
                var current = control.Resources["OnharuScrollThumb"] as Brush
                    ?? (Application.Current == null ? null : Application.Current.Resources["OnharuScrollThumb"] as Brush)
                    ?? Brush("#8794A8");
                var original = Originals.GetValue(control, delegate { return new OriginalColors { Background = current, Foreground = Brushes.White, Border = current }; });
                Open(control, current, Brushes.White, current,
                    delegate(Brush background, Brush foreground, Brush border) { control.Resources["OnharuScrollThumb"] = background; }, original);
                e.Handled = true;
            }), true);
        }

        internal static void Attach(Border control, TextBlock text)
        {
            if (control == null || text == null) return;
            control.PreviewMouseRightButtonUp += delegate(object sender, MouseButtonEventArgs e)
            {
                if (!Enabled) return;
                if (FindButton(e.OriginalSource as DependencyObject) != null) return;
                var independentTarget = FindIndependentSurface(e.OriginalSource as DependencyObject);
                if (independentTarget != null && !ReferenceEquals(independentTarget, control)) return;
                for (DependencyObject current = e.OriginalSource as DependencyObject; current != null && current != control; current = VisualTreeHelper.GetParent(current))
                    if (current is Button) return;
                var original = Originals.GetValue(control, delegate { return CaptureOriginal(control); });
                Open(control, control.Background, text.Foreground, control.BorderBrush,
                    delegate(Brush background, Brush foreground, Brush border)
                    {
                        var independent = IsIndependentSurface(control);
                        control.Background = independent ? background : Brushes.Transparent;
                        control.BorderBrush = border; text.Foreground = foreground; PaintIcons(control, foreground);
                        var surface = control.Child as Border; if (surface != null) surface.Background = background;
                    }, original);
                e.Handled = true;
            };
            if (!(control.Tag as string ?? "").StartsWith("OnharuDetailCard:", StringComparison.Ordinal)) ApplyOverride(control);
            ColorOverride saved;
            if (TryPersistentOverride(control, out saved))
            {
                control.Background = saved.Background;
                control.BorderBrush = saved.Border;
                text.Foreground = saved.Foreground;
            }
        }

        internal static void AttachIndependentSurface(Border control, TextBlock text)
        {
            if (control == null || text == null) return;
            control.PreviewMouseRightButtonUp += delegate(object sender, MouseButtonEventArgs e)
            {
                if (!Enabled) return;
                var original = Originals.GetValue(control, delegate { return CaptureOriginal(control); });
                Open(control, control.Background, text.Foreground, control.BorderBrush,
                    delegate(Brush background, Brush foreground, Brush border)
                    {
                        control.Background = background;
                        control.BorderBrush = border;
                        text.Foreground = foreground;
                    }, original);
                e.Handled = true;
            };
            ApplyOverride(control);
            ColorOverride saved;
            if (TryPersistentOverride(control, out saved))
            {
                control.Background = saved.Background;
                control.BorderBrush = saved.Border;
                text.Foreground = saved.Foreground;
            }
        }

        internal static void AttachDetailCard(Border control, TextBlock text, string stableId)
        {
            if (control == null || text == null) return;
            PromoteStableId(control, "OnharuDetailCard:" + stableId);
            control.PreviewMouseRightButtonUp += delegate(object sender, MouseButtonEventArgs e)
            {
                if (!Enabled) return;
                if (FindButton(e.OriginalSource as DependencyObject) != null) return;
                if (FindIndependentSurface(e.OriginalSource as DependencyObject) != null) return;
                var surface = control.Child as Border;
                var currentBackground = surface != null ? surface.Background : control.Background;
                var original = Originals.GetValue(control, delegate { return new OriginalColors { Background = currentBackground, Foreground = text.Foreground, Border = control.BorderBrush }; });
                Open(control, currentBackground, text.Foreground, control.BorderBrush,
                    delegate(Brush background, Brush foreground, Brush border)
                    {
                        control.Background = Brushes.Transparent; if (surface != null) surface.Background = background;
                        control.BorderBrush = border; text.Foreground = foreground; PaintIcons(control, foreground); PaintDetailBorders(control, border); PaintDetailForegrounds(control, foreground);
                    }, original);
                e.Handled = true;
            };
            ApplyOverride(control);
            ColorOverride saved;
            if (TryPersistentOverride(control, out saved))
            {
                text.Foreground = saved.Foreground;
                var surface = control.Child as Border;
                if (surface != null) surface.Background = saved.Background;
                control.Background = Brushes.Transparent;
                control.BorderBrush = saved.Border;
                PaintDetailBorders(control, saved.Border);
            }
        }

        static bool IsWithinDetailCard(DependencyObject source)
        {
            for (var current = source; current != null; current = VisualTreeHelper.GetParent(current))
            {
                var element = current as FrameworkElement;
                if (element == null) continue;
                var id = System.Windows.Automation.AutomationProperties.GetAutomationId(element);
                if (!string.IsNullOrWhiteSpace(id) && id.StartsWith("OnharuDetailCard:", StringComparison.Ordinal)) return true;
            }
            return false;
        }

        static void ButtonRightClick(object sender, MouseButtonEventArgs e)
        {
            if (!Enabled || e.ChangedButton != MouseButton.Right) return;
            var button = sender as Button;
            if (button == null || FindSegment(button) != null || activePopup != null && activePopup.IsOpen) return;
            var sourceButton = e.OriginalSource as DependencyObject;
            while (sourceButton != null && !(sourceButton is Button)) sourceButton = VisualTreeHelper.GetParent(sourceButton);
            if (!ReferenceEquals(sourceButton, button)) return;
            Open(button); e.Handled = true;
        }

        static OnharuSegmentedSwitch FindSegment(DependencyObject element)
        {
            for (var current = element; current != null; current = VisualTreeHelper.GetParent(current))
            {
                var segment = current as OnharuSegmentedSwitch;
                if (segment != null) return segment;
            }
            return null;
        }

        static void Open(OnharuSegmentedSwitch control)
        {
            var original = Originals.GetValue(control, delegate { return new OriginalColors { Background = control.SelectedBackground, Foreground = control.SelectedForeground, Border = control.BorderBrush }; });
            Open(control, control.SelectedBackground, control.SelectedForeground, control.BorderBrush,
                delegate(Brush background, Brush foreground, Brush border) { control.SetAccent(background, foreground); control.BorderBrush = border; }, original);
        }

        static void Open(Button control)
        {
            var original = Originals.GetValue(control, delegate { return CaptureOriginal(control); });
            Open(control, control.Background, control.Foreground, control.BorderBrush,
                delegate(Brush background, Brush foreground, Brush border)
                {
                    ApplyButtonSurface(control, background, foreground, border);
                }, original);
        }

        static void ApplyButtonSurface(Button control, Brush background, Brush foreground, Brush border)
        {
            control.Background = background; control.Foreground = foreground; control.BorderBrush = border; PaintIcons(control, foreground);
            var face = control.Content as Border;
            if (face != null)
            {
                face.Background = background; face.BorderBrush = border;
                var label = face.Child as TextBlock;
                if (label != null) label.Foreground = foreground;
            }
        }

        static void Open(FrameworkElement control, Brush currentBackground, Brush currentForeground, Brush currentBorder,
            Action<Brush, Brush, Brush> setColors, OriginalColors original)
        {
            var originalBackground = original.Background;
            var originalForeground = original.Foreground;
            var selectedBackground = Hex(currentBackground, "#A985D8");
            var selectedForeground = Hex(currentForeground, "#FFFFFF");
            var openingBackground = currentBackground; var openingForeground = currentForeground; var openingBorder = currentBorder;
            var keepBorder = HasVisibleBorder(currentBorder);
            var selectionMode = 0; // 0: 배경+글씨, 1: 배경만, 2: 글씨만

            var popup = new Popup { PlacementTarget = control, Placement = PlacementMode.Bottom,
                VerticalOffset = 5, AllowsTransparency = true, StaysOpen = false, PopupAnimation = PopupAnimation.Fade };
            var root = new StackPanel { Margin = new Thickness(12) };
            var chrome = new Border { Background = Brush("#FFFFFF"), BorderBrush = Brush("#B8A9EA"),
                BorderThickness = new Thickness(1.2), CornerRadius = new CornerRadius(12), Child = root,
                Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 12, ShadowDepth = 3, Opacity = .18 } };
            var dragging = false; var dragPoint = new Point();
            chrome.MouseLeftButtonDown += delegate(object sender, MouseButtonEventArgs e)
            {
                if (e.OriginalSource != chrome && e.OriginalSource != root) return;
                dragging = true; dragPoint = e.GetPosition(chrome); chrome.CaptureMouse(); e.Handled = true;
            };
            chrome.MouseMove += delegate(object sender, MouseEventArgs e)
            {
                if (!dragging) return;
                var p = e.GetPosition(chrome); popup.HorizontalOffset += p.X - dragPoint.X; popup.VerticalOffset += p.Y - dragPoint.Y;
            };
            chrome.MouseLeftButtonUp += delegate { if (dragging) { dragging = false; chrome.ReleaseMouseCapture(); } };

            root.Children.Add(new TextBlock { Text = "ONHARU 버튼 색상 도구", FontSize = 13, FontWeight = FontWeights.Bold,
                Foreground = Brush("#273147"), Margin = new Thickness(0, 0, 0, 8) });
            var modes = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 8) };
            var pairMode = ModeButton("배경+글씨", true);
            var backgroundMode = ModeButton("배경만", false);
            var textMode = ModeButton("글씨만", false);
            modes.Children.Add(pairMode); modes.Children.Add(backgroundMode); modes.Children.Add(textMode); root.Children.Add(modes);

            var preview = new Border { Height = 32, CornerRadius = new CornerRadius(9), Margin = new Thickness(0, 0, 0, 9),
                Background = Brush(selectedBackground), BorderBrush = Brush("#CBD5E1"), BorderThickness = new Thickness(1),
                Child = new TextBlock { Text = "버튼 미리보기", HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center, FontSize = 12, FontWeight = FontWeights.SemiBold,
                    Foreground = Brush(selectedForeground) } };
            root.Children.Add(preview);

            Action refreshModes = delegate
            {
                PaintModeButton(pairMode, selectionMode == 0);
                PaintModeButton(backgroundMode, selectionMode == 1);
                PaintModeButton(textMode, selectionMode == 2);
            };
            Action refreshPreview = delegate
            {
                preview.Background = Brush(selectedBackground);
                var border = keepBorder ? BorderBrush(selectedBackground) : Brushes.Transparent;
                preview.BorderBrush = border;
                ((TextBlock)preview.Child).Foreground = Brush(selectedForeground);
                setColors(Brush(selectedBackground), Brush(selectedForeground), border);
            };
            pairMode.Click += delegate { selectionMode = 0; refreshModes(); };
            backgroundMode.Click += delegate { selectionMode = 1; refreshModes(); };
            textMode.Click += delegate { selectionMode = 2; refreshModes(); };

            var deepPalette = false;
            var paletteModes = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 7) };
            var pastelPalette = ModeButton("파스텔", true); var deepPaletteButton = ModeButton("진한색", false);
            paletteModes.Children.Add(pastelPalette); paletteModes.Children.Add(deepPaletteButton); root.Children.Add(paletteModes);
            var paletteHost = new StackPanel(); root.Children.Add(paletteHost);
            Button copyButton = null;
            Action<string, string> selectColor = delegate(string background, string foreground)
            {
                if (selectionMode == 0) { selectedBackground = background; selectedForeground = foreground; }
                else if (selectionMode == 1) selectedBackground = background;
                else selectedForeground = foreground;
                // A new color selection starts a fresh copy operation.
                // The previous copy result must not remain misleadingly visible.
                refreshPreview();
                copyButton.Content = "색상 정보 복사";
            };
            Action renderPalettes = delegate
            {
                paletteHost.Children.Clear();
                var palettes = TemporaryPaletteRows(deepPalette);
                for (var row = 0; row < palettes.Length; row++)
                {
                    paletteHost.Children.Add(new TextBlock { Text = palettes[row].Item1, FontSize = 10.5,
                        Foreground = Brush("#64748B"), Margin = new Thickness(0, row == 0 ? 0 : 5, 0, 3) });
                    paletteHost.Children.Add(PaletteSwatches(palettes[row].Item2, selectColor));
                }
            };
            pastelPalette.Click += delegate { deepPalette = false; PaintModeButton(pastelPalette, true); PaintModeButton(deepPaletteButton, false); renderPalettes(); };
            deepPaletteButton.Click += delegate { deepPalette = true; PaintModeButton(pastelPalette, false); PaintModeButton(deepPaletteButton, true); renderPalettes(); };
            renderPalettes();
            root.Children.Add(new TextBlock { Text = "중립색", FontSize = 10.5, Foreground = Brush("#64748B"), Margin = new Thickness(0, 5, 0, 3) });
            var neutrals = new UniformGrid { Columns = 12 };
            foreach (var pair in new[] {
                Tuple.Create("#FFFFFF", "#111827"), Tuple.Create("#F8FAFC", "#1F2937"), Tuple.Create("#F1F5F9", "#334155"),
                Tuple.Create("#E5E7EB", "#374151"), Tuple.Create("#D1D5DB", "#4B5563"), Tuple.Create("#9CA3AF", "#FFFFFF"),
                Tuple.Create("#6B7280", "#FFFFFF"), Tuple.Create("#4B5563", "#FFFFFF"), Tuple.Create("#374151", "#FFFFFF"),
                Tuple.Create("#1F2937", "#FFFFFF"), Tuple.Create("#111827", "#FFFFFF"), Tuple.Create("#000000", "#FFFFFF") })
            {
                var capturedBackground = pair.Item1; var capturedForeground = pair.Item2;
                var swatch = new Button { Width = 23, Height = 23, Margin = new Thickness(1), Padding = new Thickness(0),
                    Background = Brush(capturedBackground), BorderBrush = Brush("#CBD5E1"), BorderThickness = new Thickness(1),
                    ToolTip = "배경 " + capturedBackground + " · 글씨 " + capturedForeground, Cursor = Cursors.Hand,
                    Tag = "temporary_palette_internal" };
                swatch.Content = new Border { Width = 9, Height = 9, CornerRadius = new CornerRadius(5), Background = Brush(capturedForeground) };
                swatch.Click += delegate
                {
                    if (selectionMode == 0) { selectedBackground = capturedBackground; selectedForeground = capturedForeground; }
                    else if (selectionMode == 1) selectedBackground = capturedBackground;
                    else selectedForeground = capturedForeground;
                    refreshPreview();
                    copyButton.Content = "색상 정보 복사";
                };
                neutrals.Children.Add(swatch);
            }
            root.Children.Add(neutrals);

            var footer = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 9, 0, 0) };
            var restore = ModeButton("원래 색", false); copyButton = ModeButton("색상 정보 복사", false);
            var applyButton = ModeButton("적용", true); var close = ModeButton("닫기", false);
            restore.Click += delegate
            {
                ClearOverride(control);
                var restoredForeground = ReadableForeground(originalBackground, originalForeground);
                setColors(originalBackground, restoredForeground, original.Border); RestoreIcons(original);
                openingBackground = originalBackground; openingForeground = restoredForeground; openingBorder = original.Border;
            };
            applyButton.Click += delegate
            {
                var background = Brush(selectedBackground); var foreground = Brush(selectedForeground);
                var border = keepBorder ? BorderBrush(selectedBackground) : Brushes.Transparent;
                SaveOverride(control, background, foreground, border); setColors(background, foreground, border);
                ApplyGroup(control); openingBackground = background; openingForeground = foreground; openingBorder = border;
                popup.IsOpen = false;
            };
            copyButton.Click += delegate
            {
                try
                {
                    Clipboard.SetText("배경 " + selectedBackground + " · 글씨 " + selectedForeground
                        + " · 테두리 " + (keepBorder ? Hex(BorderBrush(selectedBackground), selectedBackground) : "없음"));
                    copyButton.Content = "복사 완료";
                }
                catch { copyButton.Content = "복사 실패"; }
            };
            close.Click += delegate { popup.IsOpen = false; };
            footer.Children.Add(restore); footer.Children.Add(copyButton); footer.Children.Add(applyButton); footer.Children.Add(close); root.Children.Add(footer);
            popup.Closed += delegate
            {
                if (ReferenceEquals(activePopup, popup)) activePopup = null;
                setColors(openingBackground, openingForeground, openingBorder);
            };
            refreshModes(); popup.Child = chrome; activePopup = popup; popup.IsOpen = true;
        }

        static UniformGrid PaletteSwatches(Tuple<string, string>[] pairs, Action<string, string> select)
        {
            var swatches = new UniformGrid { Columns = 12 };
            foreach (var pair in pairs)
            {
                var background = pair.Item1; var foreground = pair.Item2;
                var swatch = new Button { Width = 23, Height = 23, Margin = new Thickness(1), Padding = new Thickness(0),
                    Background = Brush(background), BorderBrush = Brush("#CBD5E1"), BorderThickness = new Thickness(1),
                    ToolTip = "배경 " + background + " · 글씨 " + foreground, Cursor = Cursors.Hand, Tag = "temporary_palette_internal",
                    Content = new Border { Width = 9, Height = 9, CornerRadius = new CornerRadius(5), Background = Brush(foreground) } };
                var capturedBackground = background; var capturedForeground = foreground;
                swatch.Click += delegate { select(capturedBackground, capturedForeground); };
                swatches.Children.Add(swatch);
            }
            return swatches;
        }

        static Tuple<string, Tuple<string, string>[]>[] TemporaryPaletteRows(bool deep)
        {
            var official = OnharuColorPresets.Palettes();
            if (!deep)
            {
                var officialPairs = new List<Tuple<string, Tuple<string, string>[]>>();
                foreach (var row in new[] { 0, 2, 1 })
                {
                    var pairs = new List<Tuple<string, string>>();
                    foreach (var foreground in official[row])
                    {
                        string background, text;
                        if (OnharuColorPresets.TryPastelPair(foreground, out background, out text)) pairs.Add(Tuple.Create(background, text));
                    }
                    officialPairs.Add(Tuple.Create(row == 0 ? "화사한 컬러" : row == 2 ? "화사한 라이트" : "점잖은 쿨톤", pairs.ToArray()));
                }
                officialPairs.Add(Tuple.Create("점잖은 웜톤", Pairs(new[] {
                    "#FFF4F2|#A8443A", "#FFF0E1|#9A5A19", "#FFF8D9|#7A6200", "#EFF5D8|#57721B",
                    "#E3F3E8|#356B45", "#DFF3F0|#2A6E66", "#E2F1F5|#346A78", "#E6EEF8|#3F628C",
                    "#EAEBF7|#525989", "#F0E9F5|#6E5082", "#F5E7F0|#844D70", "#F7E8EA|#8E4C58" })));
                return officialPairs.ToArray();
            }
            return new[] {
                Tuple.Create("선명한 클래식", Pairs(new[] { "#C92A2A|#FFF0F0", "#D9480F|#FFF1E8", "#B77900|#FFF6D8", "#5C940D|#F4FFD9", "#0B8F55|#DFFFF0", "#087F8C|#E0FBFF", "#1971C2|#E7F5FF", "#364FC7|#EDF2FF", "#5F3DC4|#F3F0FF", "#862E9C|#F8F0FC", "#A61E73|#FFF0F6", "#C2255C|#FFF0F5" })),
                Tuple.Create("선명한 쥬얼", Pairs(new[] { "#9B1C31|#FFE4E8", "#A63A0A|#FFE9DC", "#8A6500|#FFF1B8", "#3F7808|#E9FFC7", "#08704A|#D5FFEA", "#006D75|#D8FBFF", "#145DA0|#DDEEFF", "#2F3FA8|#E4E8FF", "#4E2B9A|#ECE5FF", "#72238A|#F5DFFF", "#8E1E67|#FFDCF1", "#A5164A|#FFDFE9" })),
                Tuple.Create("차분한 어스", Pairs(new[] { "#7F3F45|#F8E4E6", "#80502E|#F7E8DC", "#75601F|#F5EDCF", "#536B2D|#EDF2DB", "#38644A|#DFEEE5", "#356562|#DFEFED", "#3C6270|#E1EDF1", "#455E7C|#E4EAF1", "#535579|#E8E8F0", "#665074|#EEE7F1", "#774B68|#F1E5EC", "#824C59|#F4E5E8" })),
                Tuple.Create("차분한 나이트", Pairs(new[] { "#5E3037|#F5DDE1", "#63402B|#F4E2D6", "#5D501F|#F1EACB", "#405426|#E9F0D7", "#2F503D|#DDEBE2", "#2B504E|#DCECEA", "#304E5A|#DDE9ED", "#354A65|#E0E7EF", "#42445F|#E5E5ED", "#51405D|#EBE3EE", "#604056|#EFE2EA", "#683F49|#F1E1E4" })) };
        }

        static Tuple<string, string>[] Pairs(string[] values)
        {
            var result = new Tuple<string, string>[values.Length];
            for (var i = 0; i < values.Length; i++)
            {
                var parts = values[i].Split('|'); result[i] = Tuple.Create(parts[0], parts[1]);
            }
            return result;
        }

        static Button ModeButton(string text, bool active)
        {
            var button = new Button { Content = text, Height = 27, MinWidth = 64, Padding = new Thickness(8, 0, 8, 0),
                Margin = new Thickness(0, 0, 5, 0), FontSize = 11, Cursor = Cursors.Hand, Tag = "temporary_palette_internal" };
            PaintModeButton(button, active); UiRound.Apply(button, 9); return button;
        }

        static void PaintModeButton(Button button, bool active)
        {
            button.Background = Brush(active ? "#A985D8" : "#F5F3FF");
            button.Foreground = Brush(active ? "#FFFFFF" : "#5B4A87");
            button.BorderBrush = Brush(active ? "#8C69BE" : "#D9D2F0");
        }

        static string Hex(Brush brush, string fallback)
        {
            var solid = brush as SolidColorBrush;
            if (solid == null) return fallback;
            return "#" + solid.Color.R.ToString("X2") + solid.Color.G.ToString("X2") + solid.Color.B.ToString("X2");
        }

        static SolidColorBrush Brush(string value)
        {
            return new SolidColorBrush((Color)ColorConverter.ConvertFromString(value));
        }

        static Brush ReadableForeground(Brush background, Brush preferred)
        {
            var bg = background as SolidColorBrush;
            if (bg == null) return preferred ?? Brushes.White;
            var fg = preferred as SolidColorBrush;
            var color = fg == null ? CategoryColorSystem.ReadableForeground(bg.Color) : CategoryColorSystem.ReadableForeground(bg.Color, fg.Color);
            return new SolidColorBrush(color);
        }

        static SolidColorBrush BorderBrush(string background)
        {
            var color = (Color)ColorConverter.ConvertFromString(background);
            return new SolidColorBrush(Color.FromArgb(255, (byte)(color.R * .78), (byte)(color.G * .78), (byte)(color.B * .78)));
        }

        static bool HasVisibleBorder(Brush brush)
        {
            // A null BorderBrush means the control uses its template/default border.
            // Only an explicit transparent brush represents a borderless control.
            if (brush == Brushes.Transparent) return false;
            if (brush == null) return true;
            var solid = brush as SolidColorBrush;
            return solid != null && solid.Color.A > 0;
        }

        static string GroupKey(FrameworkElement control)
        {
            var tag = control.Tag as string;
            if (tag == null || !tag.StartsWith("palette_group:", StringComparison.Ordinal)) return null;
            var parts = tag.Split(':');
            return parts.Length >= 2 ? parts[0] + ":" + parts[1] : tag;
        }

        static void SaveOverride(FrameworkElement control, Brush background, Brush foreground, Brush border)
        {
            var value = new ColorOverride { Background = background, Foreground = foreground, Border = border, ThemeId = CurrentThemeId() };
            var group = GroupKey(control);
            if (group != null) GroupOverrides[ThemeKey(group)] = value;
            else { Overrides.Remove(control); Overrides.Add(control, value); }
            var key = PersistentKey(control);
            if (key != null && paletteSettings != null)
            {
                if (paletteSettings.ButtonColorOverrides == null) paletteSettings.ButtonColorOverrides = new Dictionary<string, string>();
                paletteSettings.ButtonColorOverrides[key] = Hex(background, "#FFFFFF") + "|" + Hex(foreground, "#111827");
                Store.SaveSettings(paletteSettings);
            }
        }

        static void ClearOverride(FrameworkElement control)
        {
            var group = GroupKey(control);
            var key = PersistentKey(control);
            if (key != null && paletteSettings != null && paletteSettings.ButtonColorOverrides != null && paletteSettings.ButtonColorOverrides.Remove(key))
                Store.SaveSettings(paletteSettings);
            if (group != null)
            {
                GroupOverrides.Remove(ThemeKey(group));
                if (Application.Current != null)
                    foreach (Window window in Application.Current.Windows)
                    {
                        var candidate = window as FrameworkElement;
                        if (candidate != null && GroupKey(candidate) == group) RestoreControl(candidate);
                        Visit(window, delegate(DependencyObject child)
                        {
                            var grouped = child as FrameworkElement;
                            if (grouped != null && GroupKey(grouped) == group) RestoreControl(grouped);
                        });
                    }
            }
            else Overrides.Remove(control);
        }

        static void RestoreControl(FrameworkElement control)
        {
            OriginalColors original;
            if (!Originals.TryGetValue(control, out original)) return;
            var button = control as Button;
            if (button != null)
            {
                button.Background = original.Background; button.Foreground = original.Foreground; button.BorderBrush = original.Border;
                RestoreIcons(original); return;
            }
            var segment = control as OnharuSegmentedSwitch;
            if (segment != null) { segment.SetAccent(original.Background, original.Foreground); segment.BorderBrush = original.Border; }
            var slider = control as Slider;
            if (slider != null) slider.Foreground = original.Background;
            var scroll = control as ScrollViewer;
            if (scroll != null) scroll.Resources["OnharuScrollThumb"] = original.Background;
            var border = control as Border;
            if (border != null)
            {
                var detailId = System.Windows.Automation.AutomationProperties.GetAutomationId(border);
                if (detailId != null && detailId.StartsWith("OnharuDetailCard:", StringComparison.Ordinal))
                {
                    border.Background = Brushes.Transparent;
                    var surface = border.Child as Border;
                    if (surface != null) surface.Background = original.Background;
                }
                else border.Background = original.Background;
                border.BorderBrush = original.Border; RestoreIcons(original);
            }
        }

        static void ApplyGroup(FrameworkElement control)
        {
            var group = GroupKey(control);
            if (group == null || Application.Current == null) return;
            foreach (Window window in Application.Current.Windows) ApplyOverrides(window);
        }

        internal static void ApplyOverride(FrameworkElement control)
        {
            if (control == null) return;
            if (Equals(control.Tag, "google_sync")) return;
            var controlId = System.Windows.Automation.AutomationProperties.GetAutomationId(control);
            if (IsWithinDetailCard(control) && (string.IsNullOrWhiteSpace(controlId) || !controlId.StartsWith("OnharuDetailCard:", StringComparison.Ordinal))) return;
            ColorOverride value;
            var group = GroupKey(control);
            if (group != null)
            {
                var runtimeKey = ThemeKey(group);
                if (!GroupOverrides.TryGetValue(runtimeKey, out value) && !TryPersistentOverride(control, out value)) return;
                GroupOverrides[runtimeKey] = value;
                var tag = control.Tag as string;
                if (group == "palette_group:detail_period" && (tag == null || !tag.EndsWith(":selected", StringComparison.Ordinal))) return;
            }
            else if (!Overrides.TryGetValue(control, out value) || value.ThemeId != CurrentThemeId())
            {
                if (!TryPersistentOverride(control, out value)) return;
                Overrides.Remove(control);
                Overrides.Add(control, value);
            }

            var button = control as Button;
            if (button != null)
            {
                Originals.GetValue(button, delegate { return CaptureOriginal(button); });
                button.Background = value.Background; button.Foreground = value.Foreground; button.BorderBrush = value.Border;
                PaintIcons(button, value.Foreground); return;
            }
            var segment = control as OnharuSegmentedSwitch;
            if (segment != null)
            {
                Originals.GetValue(segment, delegate { return new OriginalColors { Background = segment.SelectedBackground, Foreground = segment.SelectedForeground, Border = segment.BorderBrush }; });
                segment.SetAccent(value.Background, value.Foreground); segment.BorderBrush = value.Border;
                return;
            }
            var slider = control as Slider;
            if (slider != null) { slider.Foreground = value.Background; return; }
            var scroll = control as ScrollViewer;
            if (scroll != null)
            {
                Originals.GetValue(scroll, delegate
                {
                    var current = scroll.Resources["OnharuScrollThumb"] as Brush
                        ?? (Application.Current == null ? null : Application.Current.Resources["OnharuScrollThumb"] as Brush);
                    return new OriginalColors { Background = current, Foreground = Brushes.White, Border = current };
                });
                scroll.Resources["OnharuScrollThumb"] = value.Background; return;
            }
            var border = control as Border;
            if (border != null)
            {
                Originals.GetValue(border, delegate { return CaptureOriginal(border); });
                var detailId = System.Windows.Automation.AutomationProperties.GetAutomationId(border);
                if (detailId != null && detailId.StartsWith("OnharuDetailCard:", StringComparison.Ordinal))
                {
                    border.Background = Brushes.Transparent;
                    var surface = border.Child as Border;
                    if (surface != null) surface.Background = value.Background;
                }
                else border.Background = value.Background;
                border.BorderBrush = value.Border; PaintIcons(border, value.Foreground); PaintDetailBorders(border, value.Border); PaintDetailForegrounds(border, value.Foreground);
            }
        }

        static void PaintDetailBorders(DependencyObject root, Brush border)
        {
            if (root == null || border == null) return;
            if (root is Button || FindButton(root) != null || IsIndependentSurface(root)) return;
            var current = root as Border;
            if (current != null && current.BorderThickness != new Thickness(0)) current.BorderBrush = border;
            for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++) PaintDetailBorders(VisualTreeHelper.GetChild(root, i), border);
        }

        static Button FindButton(DependencyObject source)
        {
            for (var current = source; current != null; current = VisualTreeHelper.GetParent(current))
            {
                var button = current as Button;
                if (button != null) return button;
            }
            return null;
        }

        static bool IsIndependentSurface(DependencyObject source)
        {
            var element = source as FrameworkElement;
            return element != null && Equals(element.Tag, "palette_independent_surface");
        }

        static FrameworkElement FindIndependentSurface(DependencyObject source)
        {
            for (var current = source; current != null; current = VisualTreeHelper.GetParent(current))
                if (IsIndependentSurface(current)) return current as FrameworkElement;
            return null;
        }

        static void PaintDetailForegrounds(DependencyObject root, Brush foreground)
        {
            if (root == null || foreground == null) return;
            if (root is Button || FindButton(root) != null || IsIndependentSurface(root)) return;
            var text = root as TextBlock;
            if (text != null) text.Foreground = foreground;
            for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++) PaintDetailForegrounds(VisualTreeHelper.GetChild(root, i), foreground);
        }

        static bool TryPersistentOverride(FrameworkElement control, out ColorOverride value)
        {
            value = null;
            var key = PersistentKey(control);
            string stored;
            if (key == null || paletteSettings == null || paletteSettings.ButtonColorOverrides == null) return false;
            if (!paletteSettings.ButtonColorOverrides.TryGetValue(key, out stored))
            {
                var separator = key.IndexOf('|');
                var legacyKey = separator >= 0 ? key.Substring(separator + 1) : null;
                if (legacyKey == null || !paletteSettings.ButtonColorOverrides.TryGetValue(legacyKey, out stored)) return false;
                paletteSettings.ButtonColorOverrides.Remove(legacyKey);
                paletteSettings.ButtonColorOverrides[key] = stored;
                Store.SaveSettings(paletteSettings);
            }
            var parts = stored.Split('|');
            if (parts.Length != 2) return false;
            try
            {
                value = new ColorOverride { Background = Brush(parts[0]), Foreground = Brush(parts[1]), Border = BorderBrush(parts[0]), ThemeId = CurrentThemeId() };
                return true;
            }
            catch { return false; }
        }

        static string PersistentKey(FrameworkElement control)
        {
            var group = GroupKey(control);
            if (group != null) return ThemeKey(group);
            var automationId = System.Windows.Automation.AutomationProperties.GetAutomationId(control);
            if (!string.IsNullOrWhiteSpace(automationId)) return ThemeKey("automation:" + automationId);
            var path = new List<int>();
            for (DependencyObject current = control; current != null; current = VisualTreeHelper.GetParent(current))
            {
                var parent = VisualTreeHelper.GetParent(current);
                if (parent == null) break;
                var index = -1;
                for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
                    if (ReferenceEquals(VisualTreeHelper.GetChild(parent, i), current)) { index = i; break; }
                if (index < 0) return null;
                path.Add(index);
                if (parent is Window) break;
            }
            if (path.Count == 0) return null;
            path.Reverse();
            return ThemeKey("visual:" + string.Join(".", path));
        }

        static string CurrentThemeId()
        {
            return paletteSettings != null && paletteSettings.ThemeId == "dark" ? "dark" : "classic";
        }

        static string ThemeKey(string key) { return CurrentThemeId() + "|" + key; }

        internal static void ApplyOverrides(DependencyObject root)
        {
            var element = root as FrameworkElement;
            if (element != null) ApplyOverride(element);
            Visit(root, delegate(DependencyObject child)
            {
                var candidate = child as FrameworkElement;
                if (candidate != null) ApplyOverride(candidate);
            });
        }

        static OriginalColors CaptureOriginal(Button button)
        {
            return CaptureOriginal((FrameworkElement)button);
        }

        static OriginalColors CaptureOriginal(FrameworkElement element)
        {
            var control = element as Control; var border = element as Border;
            var original = new OriginalColors { Background = control != null ? control.Background : border == null ? null : border.Background,
                Foreground = control == null ? null : control.Foreground, Border = control != null ? control.BorderBrush : border == null ? null : border.BorderBrush };
            Visit(element, delegate(DependencyObject child)
            {
                var text = child as TextBlock;
                if (text != null) original.Icons.Add(new IconColors { Element = text, Foreground = text.Foreground });
                var shape = child as Shape;
                if (shape != null) original.Icons.Add(new IconColors { Element = shape, Stroke = shape.Stroke, Fill = shape.Fill });
            });
            return original;
        }

        static void PaintIcons(DependencyObject root, Brush foreground)
        {
            Visit(root, delegate(DependencyObject child)
            {
                var text = child as TextBlock;
                if (text != null) text.Foreground = foreground;
                var shape = child as Shape;
                if (shape != null)
                {
                    shape.Stroke = foreground;
                    var fill = shape.Fill as SolidColorBrush;
                    if (fill != null && fill.Color.A != 0) shape.Fill = foreground;
                }
            });
        }

        static void RestoreIcons(OriginalColors original)
        {
            foreach (var icon in original.Icons)
            {
                var text = icon.Element as TextBlock;
                if (text != null) text.Foreground = icon.Foreground;
                var shape = icon.Element as Shape;
                if (shape != null) { shape.Stroke = icon.Stroke; shape.Fill = icon.Fill; }
            }
        }

        static void Visit(DependencyObject root, Action<DependencyObject> action)
        {
            var count = VisualTreeHelper.GetChildrenCount(root);
            for (var i = 0; i < count; i++)
            {
                var child = VisualTreeHelper.GetChild(root, i); action(child); Visit(child, action);
            }
        }
    }
}
