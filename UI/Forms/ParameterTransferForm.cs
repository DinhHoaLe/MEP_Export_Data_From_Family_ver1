using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Export_Data_From_Family.Models;
using Export_Data_From_Family.Services;
using Export_Data_From_Family.UI.Theme;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;
using Color = System.Drawing.Color;
using Panel = System.Windows.Forms.Panel;
using Control = System.Windows.Forms.Control;

namespace Export_Data_From_Family.UI.Forms
{
    public sealed class ParameterTransferForm : System.Windows.Forms.Form
    {
        private sealed class Choice
        {
            public string Id { get; set; }
            public string Label { get; set; }
            public override string ToString() { return Label; }
        }

        private sealed class PrecisionChoice
        {
            public double Value { get; set; }
            public override string ToString() { return Value.ToString("G", CultureInfo.InvariantCulture); }
        }

        public ParameterTransferForm(FamilyHealthResult sourceFamily, ParameterResult parameter, UIApplication uiapp)
        {
            Text = "Transfer parameter";
            StartPosition = FormStartPosition.CenterParent;
            Size = new Size(1040, 800);
            MinimumSize = new Size(900, 680);
            AntTheme.StyleForm(this);

            Document destination = uiapp.ActiveUIDocument == null ? null : uiapp.ActiveUIDocument.Document;
            string destinationName = destination == null ? "No family open" : destination.Title;
            string dataTypeLabel = TypeLabel(parameter.DataTypeId, destination);
            string sourceGroup = GroupLabel(parameter.GroupTypeId);
            string initialGroupId = string.IsNullOrWhiteSpace(parameter.GroupTypeId)
                ? GroupTypeId.Data.TypeId : parameter.GroupTypeId;
            ForgeTypeId spec = string.IsNullOrWhiteSpace(parameter.DataTypeId)
                ? null : new ForgeTypeId(parameter.DataTypeId);
            bool canChangeUnits = spec != null && SpecUtils.IsValidDataType(spec) &&
                UnitUtils.IsMeasurableSpec(spec) && Units.IsModifiableSpec(spec);
            FormatOptions destinationFormat = null;
            if (canChangeUnits && destination != null && destination.IsFamilyDocument)
                destinationFormat = destination.GetUnits().GetFormatOptions(spec);

            Dictionary<string, Label> before;
            Dictionary<string, Label> after;
            Panel beforeCard = CompareCard("01  SOURCE PARAMETER", "FROM REPORT", out before);
            Panel afterCard = CompareCard("02  DESTINATION PREVIEW", "AFTER TRANSFER", out after);
            before["Family"].Text = sourceFamily.FileName ?? "(unknown)";
            before["Parameter"].Text = parameter.Name ?? "(unnamed)";
            before["Data type"].Text = dataTypeLabel;
            before["Group"].Text = sourceGroup;
            before["Scope"].Text = parameter.IsInstance ? "Instance" : "Type";
            before["Format"].Text = FormatLabel(parameter.Format);
            before["Value"].Text = "Select a source Type below";
            before["Formula"].Text = string.IsNullOrWhiteSpace(parameter.Formula)
                ? "None" : parameter.Formula;
            after["Family"].Text = destinationName;
            after["Parameter"].Text = parameter.Name ?? "(unnamed)";
            after["Data type"].Text = dataTypeLabel;

            Panel compare = new Panel { Height = 276, Margin = new Padding(0, 0, 0, 14) };
            Label arrow = new Label
            {
                Text = "→", TextAlign = ContentAlignment.MiddleCenter,
                ForeColor = AntTheme.Accent, BackColor = AntTheme.AccentPale,
                Font = new Font("Segoe UI", 17F, FontStyle.Bold),
                Width = 38, Height = 38, Top = 116
            };
            compare.Controls.Add(beforeCard);
            compare.Controls.Add(arrow);
            compare.Controls.Add(afterCard);

            Panel formatCard = AntTheme.Card(72);
            formatCard.Margin = new Padding(0, 0, 0, 14);
            CheckBox changeFormat = SectionCheck("Change format", "Edit destination group, scope and unit display", 18, 13);
            formatCard.Controls.Add(changeFormat);
            formatCard.Controls.Add((Label)changeFormat.Tag);
            Label groupLabel = Field("PARAMETER GROUP", 20, 75);
            Label scopeLabel = Field("SCOPE", 350, 75);
            Label unitLabel = Field("DISPLAY UNIT", 20, 151);
            Label precisionLabel = Field("PRECISION", 350, 151);
            Label symbolLabel = Field("SYMBOL", 560, 151);
            System.Windows.Forms.ComboBox groupBox = DropDown(20, 96, 300);
            System.Windows.Forms.ComboBox scopeBox = DropDown(350, 96, 190);
            System.Windows.Forms.ComboBox unitBox = DropDown(20, 172, 300);
            System.Windows.Forms.ComboBox precisionBox = DropDown(350, 172, 190);
            System.Windows.Forms.ComboBox symbolBox = DropDown(560, 172, 230);
            Label formatNote = new Label
            {
                Left = 20, Top = 214, Width = 780, Height = 20,
                ForeColor = AntTheme.Muted,
                Text = "Unit changes affect every parameter with the same data type in this family."
            };
            foreach (ForgeTypeId id in ParameterUtils.GetAllBuiltInGroups())
            {
                try { groupBox.Items.Add(new Choice { Id = id.TypeId, Label = LabelUtils.GetLabelForGroup(id) }); }
                catch { }
            }
            SortChoices(groupBox);
            SelectChoice(groupBox, initialGroupId);
            scopeBox.Items.Add("Type");
            scopeBox.Items.Add("Instance");
            scopeBox.SelectedIndex = parameter.IsInstance ? 1 : 0;

            if (canChangeUnits)
            {
                foreach (ForgeTypeId id in UnitUtils.GetValidUnits(spec))
                {
                    try { unitBox.Items.Add(new Choice { Id = id.TypeId, Label = LabelUtils.GetLabelForUnit(id) }); }
                    catch { }
                }
                SortChoices(unitBox);
                if (destinationFormat != null)
                    SelectChoice(unitBox, destinationFormat.GetUnitTypeId().TypeId);
                if (unitBox.SelectedIndex < 0 && unitBox.Items.Count > 0) unitBox.SelectedIndex = 0;
            }
            else
            {
                unitBox.Enabled = false;
                precisionBox.Enabled = false;
                symbolBox.Enabled = false;
                formatNote.Text = "Unit display is not editable for this data type; group and scope can still be changed.";
            }

            formatCard.Controls.Add(groupLabel);
            formatCard.Controls.Add(scopeLabel);
            formatCard.Controls.Add(unitLabel);
            formatCard.Controls.Add(precisionLabel);
            formatCard.Controls.Add(symbolLabel);
            formatCard.Controls.Add(groupBox);
            formatCard.Controls.Add(scopeBox);
            formatCard.Controls.Add(unitBox);
            formatCard.Controls.Add(precisionBox);
            formatCard.Controls.Add(symbolBox);
            formatCard.Controls.Add(formatNote);

            Panel valueCard = AntTheme.Card(142);
            valueCard.Margin = new Padding(0, 0, 0, 14);
            CheckBox copyData = SectionCheck("Copy value and formula",
                "Choose a source Type, then select value, formula, or both", 18, 13);
            valueCard.Controls.Add(copyData);
            valueCard.Controls.Add((Label)copyData.Tag);
            Label sourceTypeLabel = Field("SOURCE TYPE", 20, 76);
            Label sourceValueLabel = Field("VALUE IN SOURCE", 350, 76);
            Label destinationTypeLabel = Field("DESTINATION TYPE", 20, 204);
            System.Windows.Forms.ComboBox sourceTypeBox = DropDown(20, 97, 300);
            System.Windows.Forms.ComboBox destinationTypeBox = DropDown(20, 225, 300);
            CheckBox copyValue = new CheckBox { Text = "Copy value", Left = 20, Top = 154,
                Width = 280, Height = 25, ForeColor = AntTheme.Text,
                Font = new Font("Segoe UI", 10F, FontStyle.Bold) };
            CheckBox copyFormula = new CheckBox { Text = "Copy formula", Left = 350, Top = 154,
                Width = 290, Height = 25, ForeColor = AntTheme.Text,
                Font = new Font("Segoe UI", 10F, FontStyle.Bold) };
            Label sourceValue = new Label
            {
                Left = 350, Top = 96, Width = 450, Height = 32,
                Font = new Font("Segoe UI", 13F, FontStyle.Bold),
                ForeColor = AntTheme.Accent, Text = "Choose a source Type"
            };
            Label valueNote = new Label
            {
                Left = 350, Top = 225, Width = 470, Height = 44,
                ForeColor = AntTheme.Muted
            };
            if (parameter.TypeValues == null || !parameter.TypeValues.Any(x => x != null))
            {
                sourceTypeBox.Items.Add(new Choice { Id = null, Label = "Default (no source Type)" });
                sourceTypeBox.SelectedIndex = 0;
            }
            else
            {
                foreach (FamilyTypeValueResult value in parameter.TypeValues.Where(x => x != null)
                    .GroupBy(x => x.TypeName).Select(x => x.First()))
                    sourceTypeBox.Items.Add(new Choice { Id = value.TypeName, Label = value.TypeName });
            }

            if (destination != null && destination.IsFamilyDocument)
            {
                foreach (FamilyType type in destination.FamilyManager.Types)
                    destinationTypeBox.Items.Add(new Choice { Id = type.Name, Label = type.Name });
                if (destinationTypeBox.Items.Count > 1)
                    destinationTypeBox.Items.Add(new Choice
                    { Id = "__matching__", Label = "All destination Types with matching names" });
                if (destination.FamilyManager.CurrentType != null)
                    SelectChoice(destinationTypeBox, destination.FamilyManager.CurrentType.Name);
                if (destinationTypeBox.SelectedIndex < 0 && destinationTypeBox.Items.Count > 0)
                    destinationTypeBox.SelectedIndex = 0;
            }
            if (destinationTypeBox.Items.Count == 0)
            {
                destinationTypeBox.Items.Add(new Choice { Id = null, Label = "Default (created on transfer)" });
                destinationTypeBox.SelectedIndex = 0;
            }
            valueCard.Controls.Add(sourceTypeLabel);
            valueCard.Controls.Add(sourceValueLabel);
            valueCard.Controls.Add(destinationTypeLabel);
            valueCard.Controls.Add(sourceTypeBox);
            valueCard.Controls.Add(destinationTypeBox);
            valueCard.Controls.Add(sourceValue);
            valueCard.Controls.Add(valueNote);
            valueCard.Controls.Add(copyValue);
            valueCard.Controls.Add(copyFormula);
            Label formulaLabel = Field("FORMULA TO APPLY", 20, 286);
            System.Windows.Forms.TextBox formulaBox = new System.Windows.Forms.TextBox
            {
                Left = 20, Top = 308, Width = 650, Height = 58,
                Multiline = true, ScrollBars = ScrollBars.Vertical,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Segoe UI", 10F),
                ForeColor = AntTheme.Text,
                Text = parameter.Formula ?? ""
            };
            Label formulaHint = new Label
            {
                Left = 20, Top = 372, Width = 790, Height = 38,
                ForeColor = AntTheme.Muted,
                Text = "Parameter references are checked against the open destination family."
            };
            Button checkFormula = AntTheme.Button("Check formula");
            checkFormula.Width = 130;
            checkFormula.Left = 680;
            checkFormula.Top = 308;
            Label formulaCheckResult = new Label { Left = 20, Top = 412, Width = 790,
                Height = 36, ForeColor = AntTheme.Muted };
            valueCard.Controls.Add(formulaLabel);
            valueCard.Controls.Add(formulaBox);
            valueCard.Controls.Add(formulaHint);
            valueCard.Controls.Add(checkFormula);
            valueCard.Controls.Add(formulaCheckResult);

            Label status = new Label
            {
                Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = AntTheme.Accent, Padding = new Padding(18, 0, 0, 0)
            };
            Panel statusPanel = new Panel { Dock = DockStyle.Bottom, Height = 52, BackColor = AntTheme.AccentPale };
            statusPanel.Controls.Add(status);
            Button transfer = AntTheme.Button("Transfer", true);
            transfer.Dock = DockStyle.Right;
            Button back = AntTheme.Button("Back");
            back.Dock = DockStyle.Right;
            back.Click += (sender, args) => Close();
            Panel footer = new Panel
            {
                Dock = DockStyle.Bottom, Height = 68, BackColor = AntTheme.Background,
                Padding = new Padding(16, 12, 18, 12)
            };
            footer.Controls.Add(back);
            footer.Controls.Add(transfer);

            Action refreshPrecisionAndSymbols = () =>
            {
                Choice unit = unitBox.SelectedItem as Choice;
                if (unit == null) return;
                ForgeTypeId unitId = new ForgeTypeId(unit.Id);
                FormatOptions format = new FormatOptions(unitId);
                double previous = (precisionBox.SelectedItem as PrecisionChoice)?.Value ??
                    (destinationFormat == null ? 0.01 : destinationFormat.Accuracy);
                precisionBox.Items.Clear();
                double[] candidates = { 100, 10, 1, 0.5, 0.25, 0.125, 0.1, 0.0625, 0.05, 0.01, 0.001, 0.0001, 0.00001 };
                foreach (double accuracy in candidates.Concat(new[] { previous }).Distinct().OrderByDescending(x => x))
                    if (format.IsValidAccuracy(accuracy))
                        precisionBox.Items.Add(new PrecisionChoice { Value = accuracy });
                for (int i = 0; i < precisionBox.Items.Count; i++)
                    if (Math.Abs(((PrecisionChoice)precisionBox.Items[i]).Value - previous) < 0.0000001)
                        precisionBox.SelectedIndex = i;
                if (precisionBox.SelectedIndex < 0 && precisionBox.Items.Count > 0)
                    precisionBox.SelectedIndex = 0;

                string previousSymbol = (symbolBox.SelectedItem as Choice)?.Id ??
                    destinationFormat?.GetSymbolTypeId().TypeId;
                symbolBox.Items.Clear();
                foreach (ForgeTypeId symbol in FormatOptions.GetValidSymbols(unitId))
                {
                    try
                    {
                        string label = string.IsNullOrWhiteSpace(symbol.TypeId)
                            ? "No symbol" : LabelUtils.GetLabelForSymbol(symbol);
                        symbolBox.Items.Add(new Choice { Id = symbol.TypeId, Label = label });
                    }
                    catch { }
                }
                SelectChoice(symbolBox, previousSymbol);
                if (symbolBox.SelectedIndex < 0 && symbolBox.Items.Count > 0) symbolBox.SelectedIndex = 0;
            };
            if (canChangeUnits && unitBox.SelectedItem != null) refreshPrecisionAndSymbols();

            Func<TransferOptions> options = () =>
            {
                Choice source = sourceTypeBox.SelectedItem as Choice;
                Choice target = destinationTypeBox.SelectedItem as Choice;
                return new TransferOptions
                {
                    GroupTypeId = changeFormat.Checked
                        ? (groupBox.SelectedItem as Choice)?.Id : initialGroupId,
                    IsInstance = changeFormat.Checked ? scopeBox.SelectedIndex == 1 : parameter.IsInstance,
                    ChangeFormat = changeFormat.Checked,
                    UnitTypeId = changeFormat.Checked && canChangeUnits
                        ? (unitBox.SelectedItem as Choice)?.Id : null,
                    Accuracy = changeFormat.Checked && canChangeUnits
                        ? (precisionBox.SelectedItem as PrecisionChoice)?.Value : null,
                    SymbolTypeId = changeFormat.Checked && canChangeUnits
                        ? (symbolBox.SelectedItem as Choice)?.Id : null,
                    CopyValue = copyData.Checked && copyValue.Checked,
                    SourceTypeName = source?.Id,
                    MatchTypes = target?.Id == "__matching__",
                    DestinationTypeName = target?.Id == "__matching__" ? null : target?.Id,
                    CopyFormula = copyData.Checked && copyFormula.Checked,
                    FormulaText = formulaBox.Text.Trim(),
                    SourceParameterNames = sourceFamily.Parameters == null
                        ? new List<string>() : sourceFamily.Parameters
                            .Where(x => x != null && !string.IsNullOrWhiteSpace(x.Name))
                            .Select(x => x.Name).ToList()
                };
            };
            Action refresh = () =>
            {
                formatCard.Height = changeFormat.Checked ? 246 : 72;
                foreach (Control control in formatCard.Controls)
                    if (control != changeFormat && control != changeFormat.Tag)
                        control.Visible = changeFormat.Checked;
                bool sourceChosen = sourceTypeBox.SelectedIndex >= 0;
                bool hasSourceValue = parameter.TypeValues != null &&
                    parameter.TypeValues.Any(x => x != null && x.HasValue &&
                        x.TypeName == (sourceTypeBox.SelectedItem as Choice)?.Id);
                copyValue.Enabled = copyData.Checked && sourceChosen && hasSourceValue;
                copyFormula.Enabled = copyData.Checked && sourceChosen;
                if (!copyValue.Enabled) copyValue.Checked = false;
                if (!copyFormula.Enabled) copyFormula.Checked = false;
                valueCard.Height = !copyData.Checked ? 72 : copyFormula.Checked ? 464 :
                    copyValue.Checked ? 284 : 195;
                foreach (Control control in valueCard.Controls)
                    if (control != copyData && control != copyData.Tag)
                        control.Visible = copyData.Checked;
                destinationTypeLabel.Visible = copyData.Checked && copyValue.Checked;
                destinationTypeBox.Visible = destinationTypeLabel.Visible;
                valueNote.Visible = destinationTypeLabel.Visible;
                formulaLabel.Visible = copyData.Checked && copyFormula.Checked;
                formulaBox.Visible = formulaLabel.Visible;
                formulaHint.Visible = formulaLabel.Visible;
                checkFormula.Visible = formulaLabel.Visible;
                formulaCheckResult.Visible = formulaLabel.Visible;

                Choice selectedSource = sourceTypeBox.SelectedItem as Choice;
                FamilyTypeValueResult selectedValue = parameter.TypeValues?.FirstOrDefault(x =>
                    x != null && x.TypeName == selectedSource?.Id);
                if (selectedSource?.Id == null)
                    sourceValue.Text = parameter.TypeValues == null ||
                        !parameter.TypeValues.Any(x => x != null && x.HasValue)
                        ? "Default / unset" : "Choose a source Type";
                else
                    sourceValue.Text = selectedValue?.DisplayValue ?? "(no display value)";
                valueNote.Text = (destinationTypeBox.SelectedItem as Choice)?.Id == "__matching__"
                    ? "Values are assigned to destination Types with the same name."
                    : (destinationTypeBox.SelectedItem as Choice)?.Id == null
                        ? "A Default Type will be created to receive the value."
                        : "Selected source value will be written to this destination Type.";

                before["Value"].Text = selectedSource?.Id == null
                    ? (parameter.TypeValues != null && parameter.TypeValues.Any(x => x != null && x.HasValue)
                        ? "Select a source Type" : "Default / unset")
                    : selectedSource.Label + "  ·  " + sourceValue.Text;
                TransferOptions current = options();
                after["Group"].Text = changeFormat.Checked
                    ? (groupBox.SelectedItem as Choice)?.Label ?? "(choose group)"
                    : GroupLabel(initialGroupId);
                after["Scope"].Text = current.IsInstance ? "Instance" : "Type";
                after["Format"].Text = changeFormat.Checked && canChangeUnits
                    ? ((unitBox.SelectedItem as Choice)?.Label ?? "(choose unit)") +
                      " · " + ((precisionBox.SelectedItem as PrecisionChoice)?.ToString() ?? "") +
                      SymbolSuffix((symbolBox.SelectedItem as Choice)?.Id)
                    : FormatLabel(destinationFormat);
                Choice selectedDestination = destinationTypeBox.SelectedItem as Choice;
                after["Value"].Text = !copyValue.Checked || selectedSource?.Id == null
                    ? "Default / unset"
                    : selectedDestination?.Id == "__matching__"
                        ? "Values by matching Type name" : sourceValue.Text;
                if (copyValue.Checked && copyFormula.Checked)
                    after["Value"].Text = "Formula determines final value";
                after["Formula"].Text = copyFormula.Checked
                    ? (string.IsNullOrWhiteSpace(formulaBox.Text) ? "(enter formula)" : formulaBox.Text.Trim())
                    : "None";

                if (copyFormula.Checked)
                {
                    var destinationNames = destination != null && destination.IsFamilyDocument
                        ? destination.FamilyManager.GetParameters().Select(x => x.Definition.Name)
                            .Concat(new[] { parameter.Name })
                        : new[] { parameter.Name };
                    var missing = FormulaDependencyChecker.MissingParameters(formulaBox.Text,
                        sourceFamily.Parameters == null ? new string[0] :
                            sourceFamily.Parameters.Where(x => x != null).Select(x => x.Name),
                        destinationNames);
                    formulaHint.Text = missing.Count == 0
                        ? "All referenced source parameters are present in the destination."
                        : "Missing in destination: " + string.Join(", ", missing);
                    formulaHint.ForeColor = missing.Count == 0 ? AntTheme.Muted : AntTheme.Accent;
                }

                bool hasSource = selectedSource?.Id != null;
                destinationTypeBox.Enabled = copyData.Checked && copyValue.Checked && hasSource;
                string issue = ParameterTransfer.Check(uiapp, parameter, current);
                transfer.Enabled = issue == null;
                status.Text = issue ?? ((copyValue.Checked || copyFormula.Checked) &&
                    destination != null && destination.IsFamilyDocument &&
                    !destination.FamilyManager.Types.Cast<FamilyType>().Any()
                    ? "Ready: a Default Type will be created in the destination family."
                    : copyValue.Checked && !hasSource
                    ? "Ready: no source Type value; the new parameter will keep its default value."
                    : "Ready to transfer. Review the destination preview above.");
                status.ForeColor = issue == null ? Color.FromArgb(45, 125, 88) : AntTheme.Accent;
                formatCard.Parent?.PerformLayout();
                valueCard.Parent?.PerformLayout();
            };

            changeFormat.CheckedChanged += (sender, args) => refresh();
            copyData.CheckedChanged += (sender, args) => refresh();
            copyValue.CheckedChanged += (sender, args) => refresh();
            copyFormula.CheckedChanged += (sender, args) => refresh();
            formulaBox.TextChanged += (sender, args) =>
            {
                formulaCheckResult.Text = "";
                refresh();
            };
            groupBox.SelectedIndexChanged += (sender, args) => refresh();
            scopeBox.SelectedIndexChanged += (sender, args) => refresh();
            unitBox.SelectedIndexChanged += (sender, args) => { refreshPrecisionAndSymbols(); refresh(); };
            precisionBox.SelectedIndexChanged += (sender, args) => refresh();
            symbolBox.SelectedIndexChanged += (sender, args) => refresh();
            sourceTypeBox.SelectedIndexChanged += (sender, args) => refresh();
            destinationTypeBox.SelectedIndexChanged += (sender, args) => refresh();
            checkFormula.Click += (sender, args) =>
            {
                string issue = ParameterTransfer.CheckFormula(uiapp, parameter, options());
                formulaCheckResult.Text = issue == null
                    ? "✓ Formula can be applied to this family. No changes were saved."
                    : "✕ " + issue;
                formulaCheckResult.ForeColor = issue == null
                    ? Color.FromArgb(45, 125, 88) : AntTheme.Accent;
            };

            transfer.Click += (sender, args) =>
            {
                try
                {
                    ParameterTransfer.Transfer(uiapp, parameter, options());
                    TaskDialog.Show("Transfer parameter", "Parameter added to " + destinationName + ".");
                    Close();
                }
                catch (Exception error) { TaskDialog.Show("Transfer parameter", error.Message); }
            };

            Panel compareHost = new Panel
            {
                Dock = DockStyle.Top, Height = 298,
                Padding = new Padding(20, 18, 20, 0), BackColor = Color.White
            };
            compare.Left = 20;
            compare.Top = 18;
            compareHost.Controls.Add(compare);
            FlowLayoutPanel body = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown,
                WrapContents = false, AutoScroll = true,
                Padding = new Padding(20, 18, 20, 18), BackColor = Color.White
            };
            body.Controls.Add(formatCard);
            body.Controls.Add(valueCard);
            Action layoutBody = () =>
            {
                int compareWidth = Math.Max(820, compareHost.ClientSize.Width - 40);
                compare.Width = compareWidth;
                int optionsWidth = Math.Max(820, body.ClientSize.Width - 46);
                formatCard.Width = optionsWidth;
                valueCard.Width = optionsWidth;
                formatNote.Width = optionsWidth - 40;
                valueNote.Width = optionsWidth - 370;
                formulaBox.Width = optionsWidth - 190;
                checkFormula.Left = optionsWidth - 150;
                formulaHint.Width = optionsWidth - 40;
                int cardWidth = (compareWidth - 54) / 2;
                beforeCard.SetBounds(0, 0, cardWidth, 274);
                arrow.Left = cardWidth + 8;
                afterCard.SetBounds(cardWidth + 54, 0, cardWidth, 274);
            };
            body.Resize += (sender, args) => layoutBody();
            compareHost.Resize += (sender, args) => layoutBody();

            Panel contentShell = AntTheme.Card(0);
            contentShell.Dock = DockStyle.Fill;
            contentShell.Padding = new Padding(8);
            contentShell.Controls.Add(body);
            contentShell.Controls.Add(compareHost);
            Panel shellHost = new Panel { Dock = DockStyle.Fill,
                Padding = new Padding(28, 0, 28, 16) };
            shellHost.Controls.Add(contentShell);
            Controls.Add(shellHost);
            Controls.Add(statusPanel);
            Controls.Add(footer);
            Controls.Add(AntTheme.Header("STEP 03 / 03", "Review and transfer",
                "Compare the source with the destination preview, then choose what to change"));
            layoutBody();
            refresh();
        }

        private static Panel CompareCard(string title, string tag, out Dictionary<string, Label> values)
        {
            Panel card = AntTheme.Card(274);
            Label titleLabel = new Label
            {
                Text = title, Left = 18, Top = 13, Width = 300, Height = 25,
                ForeColor = AntTheme.Accent,
                Font = new Font("Segoe UI", 10F, FontStyle.Bold)
            };
            Label tagLabel = new Label
            {
                Text = tag, Left = 18, Top = 39, Width = 300, Height = 18,
                ForeColor = AntTheme.Muted,
                Font = new Font("Segoe UI", 8F, FontStyle.Bold)
            };
            TableLayoutPanel table = new TableLayoutPanel
            {
                Left = 18, Top = 65, Width = 400, Height = 196,
                ColumnCount = 2, RowCount = 8
            };
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 94));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            values = new Dictionary<string, Label>();
            string[] rows = { "Family", "Parameter", "Data type", "Group", "Scope", "Format", "Value", "Formula" };
            for (int row = 0; row < rows.Length; row++)
            {
                string key = rows[row];
                table.RowStyles.Add(new RowStyle(SizeType.Percent, 100F / rows.Length));
                table.Controls.Add(new Label
                {
                    Text = key.ToUpperInvariant(), Dock = DockStyle.Fill,
                    ForeColor = AntTheme.Muted,
                    Font = new Font("Segoe UI", 7.5F, FontStyle.Bold),
                    TextAlign = ContentAlignment.MiddleLeft
                }, 0, row);
                Label value = new Label
                {
                    Dock = DockStyle.Fill, ForeColor = AntTheme.Text,
                    AutoEllipsis = true, TextAlign = ContentAlignment.MiddleLeft
                };
                table.Controls.Add(value, 1, row);
                values[key] = value;
            }
            card.Controls.Add(titleLabel);
            card.Controls.Add(tagLabel);
            card.Controls.Add(table);
            card.Resize += (sender, args) => table.Width = Math.Max(280, card.Width - 36);
            return card;
        }

        private static CheckBox SectionCheck(string title, string subtitle, int left, int top)
        {
            CheckBox box = new CheckBox
            {
                Text = title, Left = left, Top = top, Width = 320, Height = 25,
                ForeColor = AntTheme.Text,
                Font = new Font("Segoe UI", 11F, FontStyle.Bold)
            };
            Label hint = new Label
            {
                Text = subtitle, Left = left + 23, Top = top + 28,
                Width = 650, Height = 20, ForeColor = AntTheme.Muted
            };
            box.Tag = hint;
            return box;
        }

        private static Label Field(string text, int left, int top)
        {
            return new Label
            {
                Text = text, Left = left, Top = top, Width = 220, Height = 18,
                ForeColor = AntTheme.Muted,
                Font = new Font("Segoe UI", 8F, FontStyle.Bold)
            };
        }

        private static System.Windows.Forms.ComboBox DropDown(int left, int top, int width)
        {
            return new System.Windows.Forms.ComboBox
            {
                Left = left, Top = top, Width = width, Height = 30,
                DropDownStyle = ComboBoxStyle.DropDownList,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.White, ForeColor = AntTheme.Text
            };
        }

        private static void SortChoices(System.Windows.Forms.ComboBox box)
        {
            List<Choice> sorted = box.Items.Cast<Choice>()
                .OrderBy(x => x.Label, StringComparer.CurrentCultureIgnoreCase).ToList();
            box.Items.Clear();
            foreach (Choice choice in sorted) box.Items.Add(choice);
        }

        private static void SelectChoice(System.Windows.Forms.ComboBox box, string id)
        {
            for (int index = 0; index < box.Items.Count; index++)
                if ((box.Items[index] as Choice)?.Id == id)
                {
                    box.SelectedIndex = index;
                    return;
                }
        }

        private static string GroupLabel(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return "Data (source unspecified)";
            try { return LabelUtils.GetLabelForGroup(new ForgeTypeId(id)); }
            catch { return id; }
        }

        private static string TypeLabel(string id, Document destination)
        {
            if (string.IsNullOrWhiteSpace(id)) return "(missing)";
            try
            {
                ForgeTypeId type = new ForgeTypeId(id);
                if (SpecUtils.IsSpec(type)) return LabelUtils.GetLabelForSpec(type);
                if (Category.IsBuiltInCategory(type) && destination != null)
                {
                    Category category = Category.GetCategory(destination, Category.GetBuiltInCategory(type));
                    if (category != null) return "Family Type: " + category.Name;
                }
            }
            catch { }
            return id;
        }

        private static string FormatLabel(ParameterFormatResult format)
        {
            if (format == null || string.IsNullOrWhiteSpace(format.UnitTypeId)) return "Default / not exported";
            return FormatLabel(format.UnitTypeId, format.Accuracy) + SymbolSuffix(format.SymbolTypeId);
        }

        private static string FormatLabel(FormatOptions format)
        {
            if (format == null) return "Family default";
            return FormatLabel(format.GetUnitTypeId().TypeId, format.Accuracy) +
                SymbolSuffix(format.GetSymbolTypeId().TypeId);
        }

        private static string FormatLabel(string unitId, double accuracy)
        {
            string unit = unitId;
            try { unit = LabelUtils.GetLabelForUnit(new ForgeTypeId(unitId)); }
            catch { }
            return unit + " · " + accuracy.ToString("G", CultureInfo.InvariantCulture);
        }

        private static string SymbolSuffix(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return "";
            try { return " · " + LabelUtils.GetLabelForSymbol(new ForgeTypeId(id)); }
            catch { return ""; }
        }
    }
}
