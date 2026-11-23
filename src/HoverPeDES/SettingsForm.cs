using System;
using System.Drawing;
using System.Windows.Forms;

namespace HoverPeDES;

internal sealed class SettingsForm : Form
{
    private readonly ComboBox _lang = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 160 };
    private readonly NumericUpDown _delay = Num(100, 3000, 50);
    private readonly NumericUpDown _width = Num(240, 1280, 20);
    private readonly NumericUpDown _start = Num(0, 90, 5);
    private readonly NumericUpDown _volume = Num(0, 100, 5);
    private readonly CheckBox _mute = new() { AutoSize = true };
    private readonly CheckBox _images = new() { AutoSize = true };
    private readonly CheckBox _ext = new() { AutoSize = true };
    private readonly CheckBox _auto = new() { AutoSize = true };

    private static NumericUpDown Num(int min, int max, int step) =>
        new() { Minimum = min, Maximum = max, Increment = step, Width = 90 };

    public SettingsForm()
    {
        var s = Settings.Current;
        Text = L.T("settingsTitle");
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Segoe UI", 9f);
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;

        _lang.Items.AddRange(new object[] { L.T("langAuto"), "English", "Tiếng Việt" });
        _lang.SelectedIndex = s.Language == "en" ? 1 : s.Language == "vi" ? 2 : 0;
        _delay.Value = s.DelayMs;
        _width.Value = s.Width;
        _start.Value = s.StartPercent;
        _volume.Value = s.Volume;
        _mute.Checked = s.Mute;
        _images.Checked = s.PreviewImages;
        _ext.Checked = s.ExtensionEnabled;
        _auto.Checked = s.StartWithWindows;
        _mute.Text = L.T("mute");
        _images.Text = L.T("images");
        _ext.Text = L.T("ext");
        _auto.Text = L.T("autostart");

        var t = new TableLayoutPanel
        {
            ColumnCount = 2,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Padding = new Padding(16, 14, 16, 8),
        };
        Row(t, L.T("language"), _lang);
        Row(t, L.T("delay"), _delay);
        Row(t, L.T("width"), _width);
        Row(t, L.T("start"), _start);
        Row(t, L.T("volume"), _volume);
        Span(t, _mute);
        Span(t, _images);
        Span(t, _ext);
        Span(t, _auto);

        var ok = new Button { Text = L.T("ok"), DialogResult = DialogResult.OK, AutoSize = true, MinimumSize = new Size(90, 28) };
        var cancel = new Button { Text = L.T("cancel"), DialogResult = DialogResult.Cancel, AutoSize = true, MinimumSize = new Size(90, 28) };
        ok.Click += (_, _) => Apply();
        var buttons = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.RightToLeft,
            AutoSize = true,
            Dock = DockStyle.Bottom,
            Padding = new Padding(12, 6, 12, 12),
        };
        buttons.Controls.Add(cancel);
        buttons.Controls.Add(ok);

        var host = new Panel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Dock = DockStyle.Fill };
        host.Controls.Add(t);
        Controls.Add(host);
        Controls.Add(buttons);
        AcceptButton = ok;
        CancelButton = cancel;
    }

    private static void Row(TableLayoutPanel t, string label, Control c)
    {
        int r = t.RowCount++;
        t.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        t.Controls.Add(new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(0, 8, 16, 8) }, 0, r);
        c.Margin = new Padding(0, 5, 0, 5);
        t.Controls.Add(c, 1, r);
    }

    private static void Span(TableLayoutPanel t, Control c)
    {
        int r = t.RowCount++;
        t.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        c.Margin = new Padding(0, 5, 0, 5);
        t.Controls.Add(c, 0, r);
        t.SetColumnSpan(c, 2);
    }

    private void Apply()
    {
        var s = Settings.Current;
        s.Language = _lang.SelectedIndex == 1 ? "en" : _lang.SelectedIndex == 2 ? "vi" : "auto";
        s.DelayMs = (int)_delay.Value;
        s.Width = (int)_width.Value;
        s.StartPercent = (int)_start.Value;
        s.Volume = (int)_volume.Value;
        s.Mute = _mute.Checked;
        s.PreviewImages = _images.Checked;
        s.ExtensionEnabled = _ext.Checked;
        s.StartWithWindows = _auto.Checked;
        s.Save();
    }
}
