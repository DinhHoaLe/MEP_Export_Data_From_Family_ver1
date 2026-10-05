using Export_Data_From_Family.Models;
using Export_Data_From_Family.UI.Theme;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Color = System.Drawing.Color;
using Panel = System.Windows.Forms.Panel;
using Rectangle = System.Drawing.Rectangle;

namespace Export_Data_From_Family.UI.Forms
{
    public sealed class FamilyDetailForm : System.Windows.Forms.Form
    {
        private readonly System.Collections.Generic.List<ParameterResult> parameters = new System.Collections.Generic.List<ParameterResult>();
        private readonly UIApplication uiapp;

        public FamilyDetailForm(FamilyHealthResult family, UIApplication uiapp)
        {
            this.uiapp = uiapp;
            Text = "Family Parameters";
            StartPosition = FormStartPosition.CenterParent;
            Size = new Size(1160, 720);
            MinimumSize = new Size(920, 520);
            AntTheme.StyleForm(this);
            Document destination = uiapp.ActiveUIDocument == null ? null : uiapp.ActiveUIDocument.Document;
            bool familyOpen = destination != null && destination.IsFamilyDocument;
            Func<HashSet<string>> existingNames = () => familyOpen
                ? new HashSet<string>(destination.FamilyManager.GetParameters()
                    .Select(p => p.Definition.Name), StringComparer.OrdinalIgnoreCase)
                : new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            string name = Path.GetFileNameWithoutExtension(family.FileName);
            if (string.IsNullOrWhiteSpace(name))
                name = Path.GetFileNameWithoutExtension(family.FilePath);

            DataGridView grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                RowHeadersVisible = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.None,
                MultiSelect = false
            };

            grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "PARAMETER", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, FillWeight = 34 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "DATA TYPE", Width = 115 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "SCOPE", Width = 90 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "SHARED", Width = 80 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "FORMULA", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, FillWeight = 32 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "IN CURRENT FAMILY", Width = 160 });
            DataGridViewButtonColumn action = AntTheme.ActionColumn("Transfer  →");
            action.Width = 125;
            action.UseColumnTextForButtonValue = false;
            grid.Columns.Add(action);
            AntTheme.StyleGrid(grid);
            grid.RowTemplate.Height = 46;
            grid.ColumnHeadersHeight = 48;
            grid.DefaultCellStyle.Font = new Font("Segoe UI", 9F);
            grid.Columns[4].DefaultCellStyle.ForeColor = AntTheme.Muted;
            grid.Columns[6].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            grid.CellPainting += (sender, args) =>
            {
                if (args.RowIndex < 0 || (args.ColumnIndex != 5 && args.ColumnIndex != 6)) return;
                args.PaintBackground(args.CellBounds, true);
                string caption = Convert.ToString(args.Value) ?? "";
                bool locked = caption == "Locked" || caption == "Already exists" ||
                    caption == "Open family first";
                bool actionCell = args.ColumnIndex == 6;
                Rectangle badge = new Rectangle(args.CellBounds.Left + 11,
                    args.CellBounds.Top + 9, args.CellBounds.Width - 22, args.CellBounds.Height - 18);
                using (GraphicsPath path = new GraphicsPath())
                {
                    int radius = badge.Height;
                    path.AddArc(badge.Left, badge.Top, radius, radius, 90, 180);
                    path.AddArc(badge.Right - radius, badge.Top, radius, radius, 270, 180);
                    path.CloseFigure();
                    using (SolidBrush fill = new SolidBrush(actionCell
                        ? locked ? Color.FromArgb(243, 244, 246) : AntTheme.Accent
                        : locked ? AntTheme.AccentPale : Color.FromArgb(232, 247, 239)))
                        args.Graphics.FillPath(fill, path);
                }
                TextRenderer.DrawText(args.Graphics, caption,
                    new Font("Segoe UI", 8.5F, FontStyle.Bold), badge,
                    actionCell && !locked ? Color.White :
                        locked ? (actionCell ? AntTheme.Muted : AntTheme.Accent)
                        : Color.FromArgb(38, 130, 92),
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
                    TextFormatFlags.EndEllipsis);
                args.Handled = true;
            };

            Action updateAvailability = () =>
            {
                HashSet<string> names = existingNames();
                foreach (DataGridViewRow row in grid.Rows)
                {
                    ParameterResult item = row.Tag as ParameterResult;
                    bool exists = item != null && names.Contains(item.Name ?? "");
                    row.Cells[5].Value = !familyOpen ? "Open family first" : exists ? "Already exists" : "Available";
                    row.Cells[5].Style.ForeColor = exists ? AntTheme.Accent : familyOpen
                        ? Color.FromArgb(38, 130, 92) : AntTheme.Muted;
                    row.Cells[5].Style.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
                    row.Cells[6].Value = exists || !familyOpen ? "Locked" : "Transfer  →";
                    row.Cells[6].Style.ForeColor = exists || !familyOpen ? AntTheme.Muted : AntTheme.Accent;
                    row.Cells[6].Style.BackColor = exists || !familyOpen ? Color.FromArgb(245, 246, 248) : AntTheme.AccentPale;
                }
            };

            if (family.Parameters != null)
            {
                foreach (ParameterResult parameter in family.Parameters)
                {
                    if (parameter == null) continue;
                    parameters.Add(parameter);
                    int index = grid.Rows.Add(parameter.Name ?? "", parameter.StorageType ?? "",
                        parameter.IsInstance ? "Instance" : "Type", parameter.IsShared ? "Yes" : "No",
                        parameter.Formula ?? "—", "", "");
                    grid.Rows[index].Tag = parameter;
                }
            }
            updateAvailability();

            grid.CellContentClick += (sender, args) =>
            {
                if (args.RowIndex < 0 || args.ColumnIndex != 6) return;
                updateAvailability();
                if ((string)grid.Rows[args.RowIndex].Cells[6].Value == "Locked") return;
                using (ParameterTransferForm transfer = new ParameterTransferForm(family,
                    (ParameterResult)grid.Rows[args.RowIndex].Tag, this.uiapp))
                    transfer.ShowDialog(this);
                updateAvailability();
            };

            Button back = AntTheme.Button("Back");
            back.Dock = DockStyle.Right;
            back.Click += (sender, args) => Close();
            Panel footer = new Panel { Dock = DockStyle.Bottom, Height = 58,
                Padding = new Padding(12), BackColor = AntTheme.Background };
            footer.Controls.Add(back);

            Panel card = AntTheme.Card(0);
            card.Dock = DockStyle.Fill;
            card.Padding = new Padding(14);
            Panel toolbar = new Panel { Dock = DockStyle.Top, Height = 92, BackColor = Color.White };
            int sharedCount = parameters.FindAll(x => x.IsShared).Count;
            int formulaCount = parameters.FindAll(x => !string.IsNullOrWhiteSpace(x.Formula)).Count;
            Label summary = new Label
            {
                Text = parameters.Count + " PARAMETERS    ·    " + sharedCount +
                       " SHARED    ·    " + formulaCount + " WITH FORMULA",
                Left = 10, Top = 15, Width = 560, Height = 24,
                ForeColor = AntTheme.Accent,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold)
            };
            Label count = new Label { Text = parameters.Count + " shown  ·  Current family: " +
                (familyOpen ? destination.Title : "none open"), Left = 10, Top = 45,
                Width = 600, Height = 22, ForeColor = AntTheme.Muted };
            Label searchLabel = new Label { Text = "SEARCH PARAMETERS", Top = 14,
                Width = 300, Height = 18, ForeColor = AntTheme.Muted,
                Font = new Font("Segoe UI", 8F, FontStyle.Bold) };
            System.Windows.Forms.TextBox search = new System.Windows.Forms.TextBox
            { Top = 38, Width = 300, BorderStyle = BorderStyle.FixedSingle };
            search.TextChanged += (sender, args) =>
            {
                string query = search.Text.Trim();
                grid.CurrentCell = null;
                int visible = 0;
                foreach (DataGridViewRow row in grid.Rows)
                {
                    ParameterResult item = (ParameterResult)row.Tag;
                    row.Visible = query.Length == 0 ||
                        (item.Name ?? "").IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
                    if (row.Visible) visible++;
                }
                count.Text = visible + " shown";
            };
            toolbar.Resize += (sender, args) =>
            {
                search.Left = Math.Max(540, toolbar.Width - 330);
                searchLabel.Left = search.Left;
            };
            toolbar.Controls.Add(summary);
            toolbar.Controls.Add(count);
            toolbar.Controls.Add(searchLabel);
            toolbar.Controls.Add(search);
            card.Controls.Add(grid);
            card.Controls.Add(toolbar);
            Panel body = new Panel { Dock = DockStyle.Fill, Padding = new Padding(18) };
            body.Controls.Add(card);
            Controls.Add(body);
            Controls.Add(footer);
            Controls.Add(AntTheme.Header("Step 02 / 03", name,
                "Category: " + (family.Category ?? "") + "  ·  " + parameters.Count + " parameters"));
        }
    }
}
