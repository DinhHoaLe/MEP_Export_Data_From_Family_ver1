using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Export_Data_From_Family.UI.Theme
{
    internal static class AntTheme
    {
        internal static readonly Color Accent = Color.FromArgb(207, 84, 91);
        internal static readonly Color AccentPale = Color.FromArgb(255, 241, 240);
        internal static readonly Color Background = Color.FromArgb(249, 247, 248);
        internal static readonly Color Border = Color.FromArgb(226, 229, 234);
        internal static readonly Color Text = Color.FromArgb(24, 39, 68);
        internal static readonly Color Muted = Color.FromArgb(108, 115, 126);

        internal static void StyleForm(Form form)
        {
            form.BackColor = Background;
            form.Font = new Font("Segoe UI", 9F);
            form.AutoScaleMode = AutoScaleMode.Dpi;
        }

        internal static Panel Card(int height)
        {
            return new SoftCard
            {
                Height = height, BackColor = Color.White,
                Padding = new Padding(18)
            };
        }

        internal static Panel Header(string step, string title, string subtitle)
        {
            Panel panel = new Panel { Dock = DockStyle.Top, Height = 120, BackColor = Background };
            Label stepLabel = new Label
            {
                Text = step.ToUpperInvariant(), Left = 28, Top = 22, Width = 520, Height = 18,
                ForeColor = Accent, Font = new Font("Segoe UI", 8F, FontStyle.Bold)
            };
            Label titleLabel = new Label
            {
                Text = title, Left = 28, Top = 47, Width = 840, Height = 34,
                ForeColor = Text, Font = new Font("Segoe UI", 18F, FontStyle.Bold),
                AutoEllipsis = true
            };
            Label subtitleLabel = new Label
            {
                Text = subtitle, Left = 29, Top = 88, Width = 840, Height = 22,
                ForeColor = Muted, AutoEllipsis = true
            };
            panel.Controls.Add(stepLabel);
            panel.Controls.Add(titleLabel);
            panel.Controls.Add(subtitleLabel);
            return panel;
        }

        internal static Button Button(string text, bool primary = false)
        {
            Button button = new Button
            {
                Text = text,
                Width = 112,
                Height = 38,
                FlatStyle = FlatStyle.Flat,
                BackColor = primary ? Accent : Color.White,
                ForeColor = primary ? Color.White : Text,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            button.FlatAppearance.BorderColor = primary ? Accent : Border;
            button.FlatAppearance.BorderSize = 0;
            button.Resize += (sender, args) => Round(button, 7);
            button.MouseEnter += (sender, args) => button.BackColor = primary
                ? Color.FromArgb(185, 65, 73) : AccentPale;
            button.MouseLeave += (sender, args) => button.BackColor = primary
                ? Accent : Color.White;
            return button;
        }

        private static void Round(Control control, int radius)
        {
            if (control.Width < 2 || control.Height < 2) return;
            using (GraphicsPath path = new GraphicsPath())
            {
                int diameter = radius * 2;
                path.AddArc(0, 0, diameter, diameter, 180, 90);
                path.AddArc(control.Width - diameter, 0, diameter, diameter, 270, 90);
                path.AddArc(control.Width - diameter, control.Height - diameter, diameter, diameter, 0, 90);
                path.AddArc(0, control.Height - diameter, diameter, diameter, 90, 90);
                path.CloseFigure();
                control.Region = new Region(path);
            }
        }

        private sealed class SoftCard : Panel
        {
            public SoftCard()
            {
                DoubleBuffered = true;
                Resize += (sender, args) => Round(this, 9);
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                base.OnPaint(e);
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using (Pen pen = new Pen(Border))
                    e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
            }
        }

        internal static void StyleGrid(DataGridView grid)
        {
            grid.BackgroundColor = Color.White;
            grid.BorderStyle = BorderStyle.None;
            grid.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
            grid.GridColor = Border;
            grid.EnableHeadersVisualStyles = false;
            grid.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
            grid.ColumnHeadersHeight = 42;
            grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            grid.ColumnHeadersDefaultCellStyle.BackColor = AccentPale;
            grid.ColumnHeadersDefaultCellStyle.ForeColor = Accent;
            grid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            grid.ColumnHeadersDefaultCellStyle.Padding = new Padding(8, 0, 0, 0);
            grid.DefaultCellStyle.BackColor = Color.White;
            grid.DefaultCellStyle.ForeColor = Text;
            grid.DefaultCellStyle.SelectionBackColor = AccentPale;
            grid.DefaultCellStyle.SelectionForeColor = Text;
            grid.DefaultCellStyle.Padding = new Padding(8, 3, 8, 3);
            grid.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(252, 252, 253);
            grid.RowTemplate.Height = 38;
        }

        internal static DataGridViewButtonColumn ActionColumn(string caption)
        {
            DataGridViewButtonColumn column = new DataGridViewButtonColumn
            {
                HeaderText = "",
                Text = caption,
                UseColumnTextForButtonValue = true,
                Width = 105,
                FlatStyle = FlatStyle.Flat
            };
            column.DefaultCellStyle.ForeColor = Accent;
            column.DefaultCellStyle.BackColor = AccentPale;
            column.DefaultCellStyle.SelectionBackColor = AccentPale;
            return column;
        }

        internal static Label FieldLabel(string text)
        {
            return new Label
            {
                Text = text, AutoSize = true, ForeColor = Muted,
                Font = new Font("Segoe UI", 8F, FontStyle.Bold),
                Margin = new Padding(0, 8, 0, 5)
            };
        }
    }
}
