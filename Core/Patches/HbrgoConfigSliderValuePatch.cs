using System;
using System.Globalization;
using System.Reflection;
using BaseLib.Config;
using BaseLib.Config.UI;
using Godot;
using HarmonyLib;
using HBRGOSakiMod.Core.Config;
using MegaCrit.Sts2.addons.mega_text;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;

namespace HBRGOSakiMod.Core.Patches;

[HarmonyPatch(typeof(NConfigSlider), nameof(NConfigSlider._Ready))]
internal static class HbrgoConfigSliderValuePatch
{
    private static void Postfix(
        ModConfig ____config,
        PropertyInfo ____property,
        NSlider ____slider,
        MegaLabel ____sliderLabel)
    {
        if (____config is not HBRGOSakiConfig ||
            ____property?.DeclaringType != typeof(HBRGOSakiConfig) ||
            ____property.PropertyType != typeof(int) ||
            ____property.GetCustomAttribute<ConfigSliderAttribute>() is not { } range ||
            ____sliderLabel.HasNode("HbrgoNumericEditor")) return;

        // Keep BaseLib's formatter and auto-sized label. Only editing overlays it;
        // dragging and configuration reloads continue to use the original signals.
        _ = new NumericEditor(____config, ____slider, ____sliderLabel, range);
    }

    internal static bool TryParseValue(string text, double minimum, double maximum,
        double step, out double value)
    {
        text = text?.Trim();
        if (!string.IsNullOrEmpty(text) && (text[^1] == '%' || text[^1] == '％'))
            text = text[..^1].TrimEnd();
        if ((!double.TryParse(text, NumberStyles.Float | NumberStyles.AllowThousands,
                 CultureInfo.InvariantCulture, out value) &&
             !double.TryParse(text, NumberStyles.Float | NumberStyles.AllowThousands,
                 CultureInfo.CurrentCulture, out value)) || !double.IsFinite(value))
            return false;

        value = Math.Clamp(value, minimum, maximum);
        if (step > 0)
            value = minimum + Math.Round((value - minimum) / step,
                MidpointRounding.AwayFromZero) * step;
        value = Math.Clamp(value, minimum, maximum);
        return true;
    }

    private sealed class NumericEditor
    {
        private readonly ModConfig _config;
        private readonly NSlider _slider;
        private readonly MegaLabel _label;
        private readonly ConfigSliderAttribute _range;
        private readonly Button _clickTarget;
        private readonly LineEdit _edit;
        private readonly Color _labelColor;
        private bool _editing;

        internal NumericEditor(ModConfig config, NSlider slider,
            MegaLabel label, ConfigSliderAttribute range)
        {
            _config = config;
            _slider = slider;
            _label = label;
            _range = range;
            _labelColor = label.SelfModulate;
            string tooltip = new LocString("settings_ui",
                "HBRGOSAKIMOD-NUMERIC_VALUE.hover.desc").GetFormattedText();

            Control host = new()
            {
                Name = "HbrgoNumericEditor",
                MouseFilter = Control.MouseFilterEnum.Ignore
            };
            label.AddChild(host);
            host.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
            _clickTarget = new Button
            {
                Flat = true,
                FocusMode = Control.FocusModeEnum.None,
                MouseDefaultCursorShape = Control.CursorShape.Ibeam,
                TooltipText = tooltip
            };
            foreach (string style in new[] { "normal", "hover", "pressed", "focus" })
                _clickTarget.AddThemeStyleboxOverride(style, new StyleBoxEmpty());
            host.AddChild(_clickTarget);
            _clickTarget.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);

            _edit = new LineEdit
            {
                Name = "ValueInput",
                Visible = false,
                Alignment = HorizontalAlignment.Right,
                ExpandToTextLength = false,
                SelectAllOnFocus = true,
                MaxLength = 16,
                CaretBlink = true,
                TooltipText = tooltip,
                MouseFilter = Control.MouseFilterEnum.Stop
            };
            _edit.AddThemeFontOverride("font", label.GetThemeFont("font"));
            _edit.AddThemeFontSizeOverride("font_size", label.GetThemeFontSize("font_size"));
            _edit.AddThemeColorOverride("font_color", label.GetThemeColor("font_color"));
            host.AddChild(_edit);
            _edit.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
            _edit.OffsetLeft = -8;
            _edit.OffsetRight = 8;

            _clickTarget.Pressed += Begin;
            _edit.TextSubmitted += _ => Finish(commit: true);
            _edit.FocusExited += () => Finish(commit: true);
            _edit.GuiInput += input =>
            {
                if (input is InputEventKey { Pressed: true, Keycode: Key.Escape })
                {
                    _edit.AcceptEvent();
                    Finish(commit: false);
                }
            };
            host.TreeExiting += () => Finish(commit: true);
        }

        private void Begin()
        {
            if (_editing) return;
            _editing = true;
            _edit.Text = _label.Text;
            // SelfModulate hides only the label's drawing, not its editor children.
            _label.SelfModulate = new Color(_labelColor, 0f);
            _clickTarget.Hide();
            _edit.Show();
            _edit.GrabFocus();
            _edit.Edit();
            _edit.SelectAll();
        }

        private void Finish(bool commit)
        {
            if (!_editing) return;
            _editing = false;
            if (commit && TryParseValue(_edit.Text, _range.Min, _range.Max,
                    _range.Step, out double value))
            {
                // BaseLib's slider is zero-based even for non-zero minimums.
                // Its native callback performs the shared property write/notification.
                _slider.Value = value - _range.Min;
                // Leaving a config page can save before its controls lose focus.
                // Persist this explicit commit too, so the final edit is not lost.
                _config.Save();
            }
            _edit.Hide();
            _label.SelfModulate = _labelColor;
            _clickTarget.Show();
        }
    }
}
