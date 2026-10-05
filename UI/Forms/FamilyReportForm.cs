using Export_Data_From_Family.Models;
using Export_Data_From_Family.UI.Theme;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace Export_Data_From_Family.UI.Forms
{
    public sealed class FamilyReportForm : Form
    {
        private readonly DataGridView grid;
        private readonly List<FamilyHealthResult> families = new List<FamilyHealthResult>();
        private readonly UIApplication uiapp;

        public FamilyReportForm(List<FamilyHealthResult> families, UIApplication uiapp)
        {
            this.uiapp = uiapp;
            if (families != null)
                foreach (FamilyHealthResult family in families)
                    if (family != null) this.families.Add(family);

            Text = "Family Report";
            StartPosition = FormStartPosition.CenterScreen;
            Size = new Size(920, 600);
            MinimumSize = new Size(650, 400);
            AntTheme.StyleForm(this);

            grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                MultiSelect = false,
                RowHeadersVisible = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells
            };

            grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = "Name",
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
                FillWeight = 55
            });
            grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = "Category",
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
                FillWeight = 35
            });
            grid.Columns.Add(AntTheme.ActionColumn("Select"));
            AntTheme.StyleGrid(grid);

            foreach (FamilyHealthResult family in this.families)
            {
                string name = Path.GetFileNameWithoutExtension(family.FileName);
                if (string.IsNullOrWhiteSpace(name))
                    name = Path.GetFileNameWithoutExtension(family.FilePath);
                int index = grid.Rows.Add(name, family.Category ?? "", "Select");
                grid.Rows[index].Tag = family;
            }

            grid.CellContentClick += Grid_CellContentClick;

            Panel card = AntTheme.Card(0);
            card.Dock = DockStyle.Fill;
            card.Padding = new Padding(14);
            Panel toolbar = new Panel { Dock = DockStyle.Top, Height = 70, BackColor = Color.White };
            Label browseLabel = new Label { Text = "BROWSE FAMILIES", Left = 10, Top = 12,
                Width = 250, Height = 25, ForeColor = AntTheme.Text,
                Font = new Font("Segoe UI", 11F, FontStyle.Bold) };
            Label countLabel = new Label { Text = this.families.Count + " shown", Left = 10, Top = 38,
                Width = 200, Height = 20, ForeColor = AntTheme.Muted };
            Label searchLabel = new Label { Text = "SEARCH FAMILY OR CATEGORY", Left = 10, Top = 3,
                Width = 300, Height = 18, ForeColor = AntTheme.Muted,
                Font = new Font("Segoe UI", 8F, FontStyle.Bold) };
            System.Windows.Forms.TextBox search = new System.Windows.Forms.TextBox { Left = 10, Top = 25, Width = 300,
                BorderStyle = BorderStyle.FixedSingle };
            search.TextChanged += (sender, args) =>
            {
                string query = search.Text.Trim();
                grid.CurrentCell = null;
                foreach (DataGridViewRow row in grid.Rows)
                {
                    FamilyHealthResult family = (FamilyHealthResult)row.Tag;
                    row.Visible = query.Length == 0 ||
                        (family.FileName ?? "").IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0 ||
                        (family.Category ?? "").IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
                }
                int visible = 0;
                foreach (DataGridViewRow row in grid.Rows) if (row.Visible) visible++;
                countLabel.Text = visible + " shown";
            };
            toolbar.Resize += (sender, args) =>
            {
                search.Left = Math.Max(320, toolbar.Width - 330);
                searchLabel.Left = search.Left;
            };
            toolbar.Controls.Add(browseLabel);
            toolbar.Controls.Add(countLabel);
            toolbar.Controls.Add(searchLabel);
            toolbar.Controls.Add(search);
            card.Controls.Add(grid);
            card.Controls.Add(toolbar);
            Panel body = new Panel { Dock = DockStyle.Fill, Padding = new Padding(18) };
            body.Controls.Add(card);
            Controls.Add(body);
            Controls.Add(AntTheme.Header("Step 01 / 03", "Family report",
                families.Count + " families  ·  Select a family to view its parameters"));
        }

        private void Grid_CellContentClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex != 2) return;
            using (FamilyDetailForm detail = new FamilyDetailForm((FamilyHealthResult)grid.Rows[e.RowIndex].Tag, uiapp))
                detail.ShowDialog(this);
        }
    }
}
