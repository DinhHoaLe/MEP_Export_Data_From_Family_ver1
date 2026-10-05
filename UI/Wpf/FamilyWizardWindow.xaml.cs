using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Export_Data_From_Family.Models;
using Export_Data_From_Family.Services;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Input;
using System.Windows.Controls.Primitives;
using UiVisibility = System.Windows.Visibility;

namespace Export_Data_From_Family.UI.Wpf
{
    public partial class FamilyWizardWindow : Window
    {
        private sealed class Choice
        {
            public string Id { get; set; }
            public string Label { get; set; }
            public override string ToString() { return Label; }
        }

        private sealed class TransferDraft
        {
            public bool ChangeFormat;
            public bool CopyData;
            public bool CopyValue;
            public bool CopyFormula;
            public bool MatchTypes;
            public string GroupId;
            public int ScopeIndex;
            public string UnitId;
            public double? Accuracy;
            public string SymbolId;
            public string SourceTypeId;
            public string DestinationTypeId;
            public string Formula;
        }

        private sealed class FamilyRow : INotifyPropertyChanged
        {
            public FamilyHealthResult Family { get; set; }
            public string Name { get; set; }
            public string Category { get; set; }
            public string FileName { get; set; }
            public Action CheckedChanged { get; set; }
            private bool isChecked;
            public bool IsChecked
            {
                get { return isChecked; }
                set
                {
                    if (isChecked == value) return;
                    isChecked = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsChecked)));
                    CheckedChanged?.Invoke();
                }
            }
            public event PropertyChangedEventHandler PropertyChanged;
        }

        private sealed class ParameterRow : INotifyPropertyChanged
        {
            public FamilyHealthResult SourceFamily { get; set; }
            public string FamilyName { get; set; }
            public string FamilyTypeName { get; set; }
            public string FamilyTypeTooltip { get; set; }
            public ParameterResult Parameter { get; set; }
            public string Name { get; set; }
            public string StorageType { get; set; }
            public string Scope { get; set; }
            public string Formula { get; set; }
            public string Availability { get; set; }
            public bool AlreadyExists { get; set; }
            public bool CanTransfer { get; set; }
            public Action CheckedChanged { get; set; }
            private bool isChecked;
            public bool IsChecked
            {
                get { return isChecked; }
                set
                {
                    if (isChecked == value) return;
                    isChecked = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsChecked)));
                    CheckedChanged?.Invoke();
                }
            }
            public event PropertyChangedEventHandler PropertyChanged;
        }

        private readonly UIApplication uiapp;
        private readonly List<FamilyRow> families;
        private readonly List<ParameterRow> batchRows = new List<ParameterRow>();
        private readonly HashSet<ParameterRow> transferredRows = new HashSet<ParameterRow>();
        private readonly Dictionary<ParameterRow, TransferDraft> transferDrafts =
            new Dictionary<ParameterRow, TransferDraft>();
        private List<FamilyHealthResult> selectedFamilies = new List<FamilyHealthResult>();
        private readonly List<ParameterRow> parameters = new List<ParameterRow>();
        private ParameterRow activeBatchRow;
        private ICollectionView familyView;
        private ICollectionView parameterView;
        private FamilyHealthResult selectedFamily;
        private ParameterResult selectedParameter;
        private Document destination;
        private FormatOptions destinationFormat;
        private bool canChangeUnits;
        private bool ready;
        private bool updatingHeaderChecks;
        private bool changingSelection;
        private bool windowClosed;
        private CancellationTokenSource loadingCancellation;
        private int step;
        private string familyCategoryFilter;
        private string parameterFilter;

        public Exception LoadError { get; private set; }
        public bool LoadCompleted { get; private set; }

        public FamilyWizardWindow(string reportPath, UIApplication uiapp)
            : this(new List<FamilyHealthResult>(), uiapp, false)
        {
            Loaded += async (sender, args) => await LoadReportAsync(reportPath);
        }

        public FamilyWizardWindow(UIApplication uiapp, bool preferJson)
            : this(new List<FamilyHealthResult>(), uiapp, false)
        {
            ready = false;
            JsonModeRadio.IsChecked = preferJson;
            FolderModeRadio.IsChecked = !preferJson;
            ready = true;
            SourceMode_Changed(null, null);
            ShowStep(-2);
        }

        public FamilyWizardWindow(List<FamilyHealthResult> report, UIApplication uiapp)
            : this(report, uiapp, true)
        {
        }

        private FamilyWizardWindow(List<FamilyHealthResult> report, UIApplication uiapp, bool loaded)
        {
            this.uiapp = uiapp;
            families = new List<FamilyRow>();
            InitializeComponent();
            Closed += (sender, args) => windowClosed = true;
            FamilyGrid.ItemsSource = families;
            familyView = CollectionViewSource.GetDefaultView(FamilyGrid.ItemsSource);
            if (loaded)
            {
                SetFamilies(report);
                ShowStep(1);
            }
            else ShowStep(0);
        }

        private void SetFamilies(List<FamilyHealthResult> report)
        {
            families.Clear();
            families.AddRange((report ?? new List<FamilyHealthResult>())
                .Where(x => x != null)
                .Select(x => new FamilyRow
                {
                    Family = x,
                    Name = FamilyName(x),
                    Category = x.Category ?? "",
                    FileName = Path.GetFileName(x.FileName ?? x.FilePath ?? "")
                }));
            foreach (FamilyRow row in families) row.CheckedChanged = UpdateFamilySelection;
            FamilyGrid.ItemsSource = null;
            FamilyGrid.ItemsSource = families;
            familyView = CollectionViewSource.GetDefaultView(FamilyGrid.ItemsSource);
            ready = true;
            LoadCompleted = true;
            FamilySearch_Changed(null, null);
        }

        private async Task LoadReportAsync(string reportPath)
        {
            try
            {
                Task<List<FamilyHealthResult>> read = Task.Run(() =>
                {
                    List<FamilyHealthResult> result = JsonConvert.DeserializeObject<List<FamilyHealthResult>>(
                        File.ReadAllText(reportPath));
                    if (result == null)
                        throw new InvalidDataException("Report JSON must contain a list of families.");
                    return result;
                });
                await Task.WhenAll(read, Task.Delay(400));
                if (windowClosed) return;
                await Dispatcher.InvokeAsync(() =>
                {
                    if (windowClosed) return;
                    SetFamilies(read.Result);
                    ShowStep(1);
                });
            }
            catch (Exception error)
            {
                if (windowClosed) return;
                await Dispatcher.InvokeAsync(() =>
                {
                    if (windowClosed) return;
                    LoadCompleted = false;
                    ShowStep(-1);
                    SourcePathHint.Text = "Could not read the report: " + error.Message;
                });
            }
        }

        private static string FamilyName(FamilyHealthResult family)
        {
            string name = Path.GetFileNameWithoutExtension(family.FileName);
            return string.IsNullOrWhiteSpace(name)
                ? Path.GetFileNameWithoutExtension(family.FilePath) : name;
        }

        private void ShowStep(int next)
        {
            step = next;
            if (next == 3 && WindowState == WindowState.Normal)
            {
                double availableHeight = SystemParameters.WorkArea.Height - 20;
                if (availableHeight > Height) Height = Math.Min(1080, availableHeight);
            }
            StepWelcome.Visibility = next == -2 ? UiVisibility.Visible : UiVisibility.Collapsed;
            StepSource.Visibility = next == -1 ? UiVisibility.Visible : UiVisibility.Collapsed;
            StepLoading.Visibility = next == 0 ? UiVisibility.Visible : UiVisibility.Collapsed;
            StepOne.Visibility = next == 1 ? UiVisibility.Visible : UiVisibility.Collapsed;
            StepTwo.Visibility = next == 2 ? UiVisibility.Visible : UiVisibility.Collapsed;
            StepThree.Visibility = next == 3 ? UiVisibility.Visible : UiVisibility.Collapsed;
            CancelButton.Visibility = next == -2 || next == 0 ? UiVisibility.Visible : UiVisibility.Collapsed;
            CancelButton.Content = next == 0 ? "Cancel" : "Exit";
            BackButton.Visibility = next == -1 || next >= 1 ? UiVisibility.Visible : UiVisibility.Collapsed;
            NextButton.Visibility = next == -2 || next == -1 || next == 1 ? UiVisibility.Visible : UiVisibility.Collapsed;
            NextButton.Content = next == -2 ? "Get started  →" : next == -1 ? "Load families  →" : "Continue  →";
            NextButton.IsEnabled = next == -2 || next == -1 && SourcePathIsValid() ||
                next == 1 && families.Any(x => x.IsChecked);
            ExitButton.Visibility = next == 3 ? UiVisibility.Visible : UiVisibility.Collapsed;
            TransferButton.Visibility = next >= 2 ? UiVisibility.Visible : UiVisibility.Collapsed;
            StepText.Text = "FAMILY DATA STUDIO";
            Brush accent = (Brush)FindResource("AccentDark");
            Brush inactive = (Brush)FindResource("ProgressInactive");
            ProgressWelcome.Background = next == -2 ? accent : inactive;
            ProgressSource.Background = next == -1 || next == 0 ? accent : inactive;
            ProgressOne.Background = next == 1 ? accent : inactive;
            ProgressTwo.Background = next == 2 ? accent : inactive;
            ProgressThree.Background = next == 3 ? accent : inactive;
            ProgressWelcomeText.Foreground = next == -2 ? Brushes.White : accent;
            ProgressSourceText.Foreground = next == -1 || next == 0 ? Brushes.White : accent;
            ProgressOneText.Foreground = next == 1 ? Brushes.White : accent;
            ProgressTwoText.Foreground = next == 2 ? Brushes.White : accent;
            ProgressThreeText.Foreground = next == 3 ? Brushes.White : accent;
            TitleText.Text = next == -2 ? "Welcome" : next == -1 ? "Choose data source" :
                next == 0 ? "Loading families" : next == 1 ? "Choose families" : next == 2
                ? selectedFamilies.Count == 1 ? FamilyName(selectedFamilies[0])
                    : selectedFamilies.Count + " families selected"
                : "Review and transfer";
            TitleText.Foreground = next == 2 ? (Brush)FindResource("AccentDark") :
                (Brush)FindResource("Ink");
            SubtitleText.Text = next == -2 ? "Explore, compare and work with Revit family data."
                : next == -1 ? "Select a family folder or an existing JSON report."
                : next == 0 ? "Preparing the family report for selection."
                : next == 1
                ? "Choose one or more families from the exported JSON report."
                : next == 2
                    ? selectedFamilies.Count == 1
                        ? "Category: " + (selectedFamilies[0].Category ?? "") + "  ·  Choose a parameter"
                        : "Parameters from " + selectedFamilies.Count + " selected families"
                : "Select any parameter above to review or transfer it to the open family.";
            StatusText.Text = next == -2 ? "Start with your family data."
                : next == -1 ? "Enter or browse to a source path."
                : next == 0 ? "Reading family data..." :
                next == 1 ? "Select a family to continue" : next == 2
                ? "Rows already present in the open family are locked."
                : StatusText.Text;
            if (next == 1) UpdateFamilySelection();
            if (next == 2) UpdateParameterSelection();
            if (next == 2) TransferButton.Content = "Transfer  →";
            if (next == 3) TransferButton.Content = "Transfer  →";
            if (next == 3) RefreshTransferPreview();
        }

        private void FamilySearch_Changed(object sender, TextChangedEventArgs e)
        {
            if (familyView == null) return;
            string query = FamilySearch.Text.Trim();
            familyView.Filter = item =>
            {
                FamilyRow row = item as FamilyRow;
                return row != null &&
                    (familyCategoryFilter == null || row.Category == familyCategoryFilter) &&
                    (query.Length == 0 ||
                    row.Name.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    row.Category.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    row.FileName.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0);
            };
            UpdateFamilySelection();
        }

        private void SetHeaderCheck(CheckBox check, bool enabled, bool selected)
        {
            if (check == null) return;
            updatingHeaderChecks = true;
            check.IsEnabled = enabled;
            check.IsChecked = selected;
            updatingHeaderChecks = false;
        }

        private void FamilySelectAll_Changed(object sender, RoutedEventArgs e)
        {
            if (!ready || updatingHeaderChecks || familyView == null) return;
            bool select = FamilySelectAllCheck.IsChecked == true;
            changingSelection = true;
            foreach (FamilyRow row in familyView.Cast<FamilyRow>().ToList()) row.IsChecked = select;
            changingSelection = false;
            UpdateFamilySelection();
        }

        private void ParameterSelectAll_Changed(object sender, RoutedEventArgs e)
        {
            if (!ready || updatingHeaderChecks || parameterView == null) return;
            bool select = ParameterSelectAllCheck.IsChecked == true;
            changingSelection = true;
            foreach (ParameterRow row in parameterView.Cast<ParameterRow>().Where(x => x.CanTransfer).ToList())
                row.IsChecked = select;
            changingSelection = false;
            UpdateParameterSelection();
        }

        private static void OpenMenu(Button button, IEnumerable<MenuItem> items)
        {
            ContextMenu menu = new ContextMenu
            {
                PlacementTarget = button,
                Placement = PlacementMode.Bottom
            };
            foreach (MenuItem item in items) menu.Items.Add(item);
            button.ContextMenu = menu;
            menu.IsOpen = true;
        }

        private static MenuItem MenuChoice(string title, Action action)
        {
            MenuItem item = new MenuItem { Header = title };
            item.Click += (sender, args) => action();
            return item;
        }

        private static void SortView(ICollectionView view, string property,
            ListSortDirection direction)
        {
            view.SortDescriptions.Clear();
            view.SortDescriptions.Add(new SortDescription(property, direction));
            view.Refresh();
        }

        private void FamilyFilter_Click(object sender, RoutedEventArgs e)
        {
            List<MenuItem> items = new List<MenuItem>
            {
                MenuChoice("All categories", () => { familyCategoryFilter = null; FamilySearch_Changed(null, null); })
            };
            foreach (string category in families.Select(x => x.Category).Distinct()
                .OrderBy(x => x, StringComparer.CurrentCultureIgnoreCase))
            {
                string selectedCategory = category;
                items.Add(MenuChoice(string.IsNullOrWhiteSpace(category) ? "Uncategorized" : category,
                    () => { familyCategoryFilter = selectedCategory; FamilySearch_Changed(null, null); }));
            }
            OpenMenu(FamilyFilterButton, items);
        }

        private void FamilySort_Click(object sender, RoutedEventArgs e)
        {
            OpenMenu(FamilySortButton, new[]
            {
                MenuChoice("Name A–Z", () => SortView(familyView, "Name", ListSortDirection.Ascending)),
                MenuChoice("Name Z–A", () => SortView(familyView, "Name", ListSortDirection.Descending)),
                MenuChoice("Category A–Z", () => SortView(familyView, "Category", ListSortDirection.Ascending))
            });
        }

        private void FamilyAll_Click(object sender, RoutedEventArgs e)
        {
            List<FamilyRow> visible = familyView.Cast<FamilyRow>().ToList();
            bool select = visible.Any(x => !x.IsChecked);
            changingSelection = true;
            foreach (FamilyRow row in visible) row.IsChecked = select;
            changingSelection = false;
            UpdateFamilySelection();
        }

        private void ParameterSearch_Changed(object sender, TextChangedEventArgs e)
        {
            if (parameterView == null) return;
            string query = ParameterSearch.Text.Trim();
            parameterView.Filter = item =>
            {
                ParameterRow row = item as ParameterRow;
                return row != null &&
                    (parameterFilter == null ||
                        parameterFilter == "available" && row.CanTransfer ||
                        parameterFilter == "existing" && row.Availability == "Already exists" ||
                        parameterFilter == "formula" && row.Formula != "—") &&
                    (query.Length == 0 ||
                        row.Name.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0 ||
                        row.FamilyName.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0);
            };
            ParameterSummaryText.Text = parameterView.Cast<object>().Count() + " OF " + parameters.Count +
                " PARAMETERS   ·   " + selectedFamilies.Count + " FAMILIES";
            UpdateParameterSelection();
        }

        private void ParameterFilter_Click(object sender, RoutedEventArgs e)
        {
            OpenMenu(ParameterFilterButton, new[]
            {
                MenuChoice("All parameters", () => SetParameterFilter(null)),
                MenuChoice("Available to transfer", () => SetParameterFilter("available")),
                MenuChoice("Already in current family", () => SetParameterFilter("existing")),
                MenuChoice("With formula", () => SetParameterFilter("formula"))
            });
        }

        private void SetParameterFilter(string filter)
        {
            parameterFilter = filter;
            ParameterSearch_Changed(null, null);
        }

        private void ParameterSort_Click(object sender, RoutedEventArgs e)
        {
            OpenMenu(ParameterSortButton, new[]
            {
                MenuChoice("Parameter A–Z", () => SortView(parameterView, "Name", ListSortDirection.Ascending)),
                MenuChoice("Parameter Z–A", () => SortView(parameterView, "Name", ListSortDirection.Descending)),
                MenuChoice("Family A–Z", () => SortView(parameterView, "FamilyName", ListSortDirection.Ascending))
            });
        }

        private void ParameterAll_Click(object sender, RoutedEventArgs e)
        {
            List<ParameterRow> visible = parameterView.Cast<ParameterRow>()
                .Where(x => x.CanTransfer).ToList();
            bool select = visible.Any(x => !x.IsChecked);
            changingSelection = true;
            foreach (ParameterRow row in visible) row.IsChecked = select;
            changingSelection = false;
            UpdateParameterSelection();
        }

        private void FamilyGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!ready) return;
            if (FamilyGrid.SelectedItems.Count > 0) FamilyGrid.UnselectAll();
        }

        private void UpdateFamilySelection()
        {
            if (!ready || changingSelection || familyView == null) return;
            List<FamilyRow> visible = familyView.Cast<FamilyRow>().ToList();
            SetHeaderCheck(FamilySelectAllCheck, visible.Count > 0,
                visible.Count > 0 && visible.All(x => x.IsChecked));
            int selectedCount = families.Count(x => x.IsChecked);
            NextButton.IsEnabled = selectedCount > 0;
            FamilyCountText.Text = familyView.Cast<object>().Count() + " of " + families.Count +
                " families  ·  " + selectedCount + " selected";
            if (step != 1) return;
            StatusText.Text = selectedCount == 0 ? "Choose families to continue" :
                selectedCount + (selectedCount == 1 ? " family selected" : " families selected");
            StatusText.Foreground = (Brush)FindResource("Muted");
        }

        private async void Next_Click(object sender, RoutedEventArgs e)
        {
            if (step == -2) ShowStep(-1);
            else if (step == -1) await LoadSelectedSourceAsync();
            else if (step == 1) ConfirmFamily();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            if (step == 0 && loadingCancellation != null)
                loadingCancellation.Cancel();
            Close();
        }

        private bool SourcePathIsValid()
        {
            if (FolderPathBox == null || JsonPathBox == null || JsonModeRadio == null) return false;
            string path = SelectedSourcePath();
            return JsonModeRadio.IsChecked == true
                ? File.Exists(path) && path.EndsWith(".json", StringComparison.OrdinalIgnoreCase)
                : Directory.Exists(path);
        }

        private string SelectedSourcePath()
        {
            return (JsonModeRadio.IsChecked == true ? JsonPathBox.Text : FolderPathBox.Text)
                .Trim().Trim('"');
        }

        private void SourceMode_Changed(object sender, RoutedEventArgs e)
        {
            if (FolderPathBox == null || JsonPathBox == null || JsonModeRadio == null) return;
            bool json = JsonModeRadio.IsChecked == true;
            FolderPathBox.IsEnabled = !json;
            BrowseFolderButton.IsEnabled = !json;
            JsonPathBox.IsEnabled = json;
            BrowseJsonButton.IsEnabled = json;
            SourcePathHint.Text = json
                ? "Select an existing .json report or paste its full path."
                : "Choose a folder containing .rfa files. A JSON report will be saved inside it.";
            if (step == -1) NextButton.IsEnabled = SourcePathIsValid();
        }

        private void SourcePath_Changed(object sender, TextChangedEventArgs e)
        {
            if (NextButton != null && step == -1)
                NextButton.IsEnabled = SourcePathIsValid();
        }

        private void BrowseSource_Click(object sender, RoutedEventArgs e)
        {
            bool json = sender == BrowseJsonButton;
            if (json) JsonModeRadio.IsChecked = true;
            else FolderModeRadio.IsChecked = true;
            System.Windows.Controls.TextBox pathBox = json ? JsonPathBox : FolderPathBox;
            SourcePathPickerWindow picker = new SourcePathPickerWindow(json, pathBox.Text) { Owner = this };
            if (picker.ShowDialog() == true) pathBox.Text = picker.SelectedPath;
        }

        private async Task LoadSelectedSourceAsync()
        {
            if (!SourcePathIsValid())
            {
                SourcePathHint.Text = "Choose a valid source path before continuing.";
                return;
            }
            string path = SelectedSourcePath();
            bool json = JsonModeRadio.IsChecked == true;
            try
            {
                if (!json && Directory.GetFiles(path, "*.rfa", SearchOption.TopDirectoryOnly).Length == 0)
                {
                    SourcePathHint.Text = "This folder has no .rfa files. Choose another folder.";
                    return;
                }
            }
            catch (Exception error)
            {
                SourcePathHint.Text = "Could not read this folder: " + error.Message;
                return;
            }
            loadingCancellation?.Dispose();
            loadingCancellation = new CancellationTokenSource();
            ShowStep(0);
            LoadingTitle.Text = json ? "Opening family report" : "Analyzing Revit families";
            LoadingDetail.Text = json ? "Reading names, Types and parameters from the JSON report."
                : "Preparing family documents for analysis.";
            LoadingProgress.IsIndeterminate = json;
            LoadingProgress.Value = 0;
            try
            {
                if (json) await LoadReportAsync(path);
                else
                {
                    string report = await FamilyReportExporter.ExportAsync(uiapp, path,
                        (done, total) =>
                        {
                            if (windowClosed) return;
                            LoadingDetail.Text = done == total
                                ? "Writing the JSON report..."
                                : "Analyzing family " + (done + 1) + " of " + total + ".";
                            LoadingProgress.Maximum = Math.Max(1, total);
                            LoadingProgress.Value = done;
                            StatusText.Text = done + " / " + total + " families analyzed";
                        }, loadingCancellation.Token);
                    if (!windowClosed) await LoadReportAsync(report);
                }
            }
            catch (OperationCanceledException)
            {
                if (!windowClosed) await Dispatcher.InvokeAsync(() => ShowStep(-1));
            }
            catch (Exception error)
            {
                if (windowClosed) return;
                await Dispatcher.InvokeAsync(() =>
                {
                    if (windowClosed) return;
                    ShowStep(-1);
                    SourcePathHint.Text = "Could not load this source: " + error.Message;
                });
            }
        }

        private void ConfirmFamily()
        {
            selectedFamilies = families.Where(x => x.IsChecked).Select(x => x.Family).ToList();
            if (selectedFamilies.Count == 0) return;
            RefreshParameters();
            ShowStep(2);
        }

        private void RefreshParameters()
        {
            HashSet<ParameterResult> checkedBefore = new HashSet<ParameterResult>(
                parameters.Where(x => x.IsChecked).Select(x => x.Parameter));
            destination = uiapp?.ActiveUIDocument == null ? null : uiapp.ActiveUIDocument.Document;
            bool familyOpen = destination != null && destination.IsFamilyDocument;
            HashSet<string> names = familyOpen
                ? new HashSet<string>(destination.FamilyManager.GetParameters()
                    .Select(x => x.Definition.Name), StringComparer.OrdinalIgnoreCase)
                : new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            parameters.Clear();
            foreach (FamilyHealthResult family in selectedFamilies)
            {
                List<string> familyTypeNames = (family.Parameters ?? new List<ParameterResult>())
                    .Where(x => x != null && x.TypeValues != null)
                    .SelectMany(x => x.TypeValues)
                    .Where(x => x != null && !string.IsNullOrWhiteSpace(x.TypeName))
                    .Select(x => x.TypeName)
                    .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                foreach (ParameterResult parameter in family.Parameters ?? new List<ParameterResult>())
                {
                    if (parameter == null) continue;
                    bool exists = names.Contains(parameter.Name ?? "");
                    ParameterRow row = new ParameterRow
                    {
                        SourceFamily = family,
                        FamilyName = FamilyName(family),
                        FamilyTypeName = familyTypeNames.Count == 0 ? "No family type in report" :
                            familyTypeNames.Count == 1 ? familyTypeNames[0] :
                            familyTypeNames[0] + " (+" + (familyTypeNames.Count - 1) + " more)",
                        FamilyTypeTooltip = familyTypeNames.Count == 0 ? "No family type in report" :
                            string.Join("\n", familyTypeNames),
                        Parameter = parameter,
                        Name = parameter.Name ?? "",
                        StorageType = parameter.StorageType ?? "",
                        Scope = parameter.IsInstance ? "Instance" : "Type",
                        Formula = string.IsNullOrWhiteSpace(parameter.Formula) ? "—" : parameter.Formula,
                        Availability = !familyOpen ? "Open family first" : exists ? "Already exists" : "Available",
                        AlreadyExists = exists,
                        CanTransfer = familyOpen && !exists,
                        IsChecked = familyOpen && !exists && checkedBefore.Contains(parameter)
                    };
                    row.CheckedChanged = UpdateParameterSelection;
                    parameters.Add(row);
                }
            }
            ParameterGrid.ItemsSource = null;
            ParameterGrid.ItemsSource = parameters;
            parameterView = CollectionViewSource.GetDefaultView(ParameterGrid.ItemsSource);
            ParameterSearch_Changed(null, null);
            DestinationText.Text = "Current family: " +
                (familyOpen ? destination.Title : "none open");
            UpdateParameterSelection();
        }

        private void ParameterGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ready && ParameterGrid.SelectedItems.Count > 0) ParameterGrid.UnselectAll();
        }

        private void UpdateParameterSelection()
        {
            if (!ready || changingSelection || parameterView == null) return;
            List<ParameterRow> visible = parameterView.Cast<ParameterRow>()
                .Where(x => x.CanTransfer).ToList();
            SetHeaderCheck(ParameterSelectAllCheck, visible.Count > 0,
                visible.Count > 0 && visible.All(x => x.IsChecked));
            if (step != 2) return;
            List<ParameterRow> checkedRows = parameters.Where(x => x.IsChecked).ToList();
            string duplicate = checkedRows.GroupBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault(x => x.Count() > 1)?.Key;
            TransferButton.IsEnabled = checkedRows.Count > 0 && duplicate == null;
            StatusText.Text = duplicate != null
                ? "Select one source for parameter ‘" + duplicate + "’."
                : checkedRows.Count == 0
                    ? "Select parameters using the checkboxes."
                    : checkedRows.Count + " parameters selected for transfer.";
            StatusText.Foreground = duplicate == null ? (Brush)FindResource("Muted") :
                (Brush)FindResource("AccentDark");
        }

        private int PendingBatchCount
        {
            get { return batchRows.Count(x => !transferredRows.Contains(x)); }
        }

        private void ShowBatchParameter(ParameterRow row)
        {
            if (row == null || !batchRows.Contains(row)) return;
            if (step == 3 && activeBatchRow == row) return;
            if (step == 3 && activeBatchRow != null && activeBatchRow != row)
                SaveCurrentDraft();
            activeBatchRow = row;
            selectedFamily = row.SourceFamily;
            selectedParameter = row.Parameter;
            PrepareTransfer();
            RestoreDraft(row);
            if (step != 3) ShowStep(3);
            OptionsScroll.IsEnabled = !transferredRows.Contains(row);
            RefreshTransferPreview();
            RefreshBatchPreview();
        }

        private ParameterRow NextPendingParameter(int startIndex)
        {
            if (batchRows.Count == 0) return null;
            for (int offset = 0; offset < batchRows.Count; offset++)
            {
                ParameterRow row = batchRows[(startIndex + offset) % batchRows.Count];
                if (!transferredRows.Contains(row)) return row;
            }
            return null;
        }

        private void BatchSelect_Click(object sender, RoutedEventArgs e)
        {
            ShowBatchParameter((sender as Button)?.Tag as ParameterRow);
        }

        private void BatchRemove_Click(object sender, RoutedEventArgs e)
        {
            ParameterRow row = (sender as Button)?.Tag as ParameterRow;
            if (row == null || transferredRows.Contains(row)) return;
            int index = batchRows.IndexOf(row);
            if (index < 0) return;
            bool wasActive = row == activeBatchRow;
            batchRows.RemoveAt(index);
            transferDrafts.Remove(row);
            row.IsChecked = false;
            foreach (ParameterRow current in parameters.Where(x => x.Parameter == row.Parameter &&
                x.SourceFamily == row.SourceFamily)) current.IsChecked = false;
            if (batchRows.Count == 0)
            {
                activeBatchRow = null;
                transferredRows.Clear();
                RefreshParameters();
                ShowStep(2);
                StatusText.Text = "No parameters remain in the transfer list.";
                return;
            }
            if (wasActive)
                ShowBatchParameter(NextPendingParameter(index) ??
                    batchRows[Math.Min(index, batchRows.Count - 1)]);
            else RefreshBatchPreview();
        }

        private void RefreshBatchPreview()
        {
            int index = batchRows.IndexOf(activeBatchRow);
            bool activeDone = activeBatchRow != null && transferredRows.Contains(activeBatchRow);
            BatchCurrentText.Text = (activeDone ? "TRANSFERRED" : "PARAMETER " + (index + 1) +
                " / " + batchRows.Count) + "   ·   " + (selectedParameter?.Name ?? "");
            BatchProgressText.Text = transferredRows.Count + " of " + batchRows.Count + " transferred";
            BatchProgressBar.Minimum = 0;
            BatchProgressBar.Maximum = Math.Max(1, batchRows.Count);
            BatchProgressBar.Value = transferredRows.Count;
            BatchItemsPanel.Children.Clear();
            for (int itemIndex = 0; itemIndex < batchRows.Count; itemIndex++)
            {
                ParameterRow row = batchRows[itemIndex];
                bool done = transferredRows.Contains(row);
                bool current = row == activeBatchRow;
                Border chip = new Border
                {
                    CornerRadius = new CornerRadius(8),
                    BorderThickness = new Thickness(1),
                    BorderBrush = (Brush)FindResource(done ? "Accent" : "Line"),
                    Background = current && !done ? (Brush)FindResource("AccentDark") :
                        done ? (Brush)FindResource("ProgressInactive") : (Brush)FindResource("Surface"),
                    Margin = new Thickness(0, 0, 8, 0),
                    ToolTip = row.FamilyName + "  ·  " + row.Name +
                        (done ? "  ·  Transferred" : "  ·  Pending")
                };
                StackPanel contents = new StackPanel { Orientation = Orientation.Horizontal };
                Button select = new Button
                {
                    Style = (Style)FindResource("BatchChipButton"),
                    Tag = row,
                    ToolTip = "View " + row.Name,
                    Content = new TextBlock
                    {
                        Text = (done ? "✓ " : (itemIndex + 1) + ". ") + row.Name,
                        Foreground = done ? (Brush)FindResource("AccentDark") :
                            current ? Brushes.White : (Brush)FindResource("Ink"),
                        FontWeight = done || current ? FontWeights.SemiBold : FontWeights.Normal,
                        MaxWidth = 190,
                        TextTrimming = TextTrimming.CharacterEllipsis
                    }
                };
                select.Click += BatchSelect_Click;
                contents.Children.Add(select);
                if (!done)
                {
                    Button remove = new Button
                    {
                        Style = (Style)FindResource("BatchChipButton"),
                        Tag = row,
                        Padding = new Thickness(3, 5, 9, 5),
                        ToolTip = "Remove " + row.Name,
                        Content = new TextBlock
                        {
                            Text = "×", FontSize = 16, FontWeight = FontWeights.Bold,
                            Foreground = current ? Brushes.White : (Brush)FindResource("AccentDark")
                        }
                    };
                    remove.Click += BatchRemove_Click;
                    contents.Children.Add(remove);
                }
                chip.Child = contents;
                BatchItemsPanel.Children.Add(chip);
            }
            TransferButton.Content = activeDone ? "Transferred" :
                PendingBatchCount > 1 ? "Transfer & next  →" : "Transfer parameter";
            if (step == 3 && activeDone && PendingBatchCount == 0)
                StatusText.Text = "All remaining parameters were transferred or removed.";
        }

        private void SaveCurrentDraft()
        {
            if (!ready || activeBatchRow == null || !batchRows.Contains(activeBatchRow) ||
                transferredRows.Contains(activeBatchRow)) return;
            transferDrafts[activeBatchRow] = new TransferDraft
            {
                ChangeFormat = ChangeFormatCheck.IsChecked == true,
                CopyData = CopyDataCheck.IsChecked == true,
                CopyValue = CopyValueRadio.IsChecked == true,
                CopyFormula = CopyFormulaRadio.IsChecked == true,
                MatchTypes = MatchTypesCheck.IsChecked == true,
                GroupId = (GroupBox.SelectedItem as Choice)?.Id,
                ScopeIndex = ScopeBox.SelectedIndex,
                UnitId = (UnitBox.SelectedItem as Choice)?.Id,
                Accuracy = PrecisionBox.SelectedItem is double
                    ? (double?)PrecisionBox.SelectedItem : null,
                SymbolId = (SymbolBox.SelectedItem as Choice)?.Id,
                SourceTypeId = (SourceTypeBox.SelectedItem as Choice)?.Id,
                DestinationTypeId = (DestinationTypeBox.SelectedItem as Choice)?.Id,
                Formula = FormulaBox.Text
            };
        }

        private static Choice FindChoice(System.Windows.Controls.ComboBox box, string id)
        {
            return box.Items.Cast<Choice>().FirstOrDefault(x => x.Id == id);
        }

        private void RestoreDraft(ParameterRow row)
        {
            TransferDraft draft;
            if (!transferDrafts.TryGetValue(row, out draft)) return;
            ready = false;
            ChangeFormatCheck.IsChecked = draft.ChangeFormat;
            CopyDataCheck.IsChecked = draft.CopyData;
            GroupBox.SelectedItem = FindChoice(GroupBox, draft.GroupId);
            ScopeBox.SelectedIndex = draft.ScopeIndex;
            if (canChangeUnits && draft.UnitId != null)
            {
                UnitBox.SelectedItem = FindChoice(UnitBox, draft.UnitId);
                PopulatePrecisionAndSymbols();
                if (draft.Accuracy.HasValue) PrecisionBox.SelectedItem = draft.Accuracy.Value;
                SymbolBox.SelectedItem = FindChoice(SymbolBox, draft.SymbolId);
            }
            SourceTypeBox.SelectedItem = FindChoice(SourceTypeBox, draft.SourceTypeId);
            DestinationTypeBox.SelectedItem = FindChoice(DestinationTypeBox, draft.DestinationTypeId);
            FormulaBox.Text = draft.Formula ?? "";
            CopyValueRadio.IsChecked = draft.CopyValue;
            CopyFormulaRadio.IsChecked = draft.CopyFormula;
            MatchTypesCheck.IsChecked = draft.MatchTypes;
            ready = true;
            RefreshTransferPreview();
        }

        private void PrepareTransfer()
        {
            ready = false;
            ChangeFormatCheck.IsChecked = false;
            CopyDataCheck.IsChecked = false;
            CopyValueRadio.IsChecked = false;
            CopyFormulaRadio.IsChecked = false;
            MatchTypesCheck.IsChecked = false;
            FormulaResult.Text = "";
            FormulaBox.Text = selectedParameter.Formula ?? "";
            SourceTypeBox.ItemsSource = null;
            DestinationTypeBox.ItemsSource = null;

            string groupId = string.IsNullOrWhiteSpace(selectedParameter.GroupTypeId)
                ? GroupTypeId.Data.TypeId : selectedParameter.GroupTypeId;
            List<Choice> groups = new List<Choice>();
            foreach (ForgeTypeId id in ParameterUtils.GetAllBuiltInGroups())
            {
                try { groups.Add(new Choice { Id = id.TypeId, Label = LabelUtils.GetLabelForGroup(id) }); }
                catch { }
            }
            GroupBox.ItemsSource = groups.OrderBy(x => x.Label).ToList();
            GroupBox.SelectedItem = groups.FirstOrDefault(x => x.Id == groupId);
            ScopeBox.SelectedIndex = selectedParameter.IsInstance ? 1 : 0;

            ForgeTypeId spec = string.IsNullOrWhiteSpace(selectedParameter.DataTypeId)
                ? null : new ForgeTypeId(selectedParameter.DataTypeId);
            canChangeUnits = spec != null && SpecUtils.IsValidDataType(spec) &&
                UnitUtils.IsMeasurableSpec(spec) && Units.IsModifiableSpec(spec);
            destinationFormat = canChangeUnits && destination != null && destination.IsFamilyDocument
                ? destination.GetUnits().GetFormatOptions(spec) : null;
            UnitBox.IsEnabled = canChangeUnits;
            PrecisionBox.IsEnabled = canChangeUnits;
            SymbolBox.IsEnabled = canChangeUnits;
            FormatNote.Text = canChangeUnits
                ? "Unit changes affect other parameters with the same data type in this family."
                : "This data type has no editable unit display. Group and scope can still be changed.";
            if (canChangeUnits)
            {
                List<Choice> units = new List<Choice>();
                foreach (ForgeTypeId id in UnitUtils.GetValidUnits(spec))
                {
                    try { units.Add(new Choice { Id = id.TypeId, Label = LabelUtils.GetLabelForUnit(id) }); }
                    catch { }
                }
                UnitBox.ItemsSource = units.OrderBy(x => x.Label).ToList();
                UnitBox.SelectedItem = units.FirstOrDefault(x =>
                    x.Id == destinationFormat?.GetUnitTypeId().TypeId) ?? units.FirstOrDefault();
                PopulatePrecisionAndSymbols();
            }
            else
            {
                UnitBox.ItemsSource = null;
                PrecisionBox.ItemsSource = null;
                SymbolBox.ItemsSource = null;
            }

            List<Choice> sourceTypes = (selectedParameter.TypeValues ?? new List<FamilyTypeValueResult>())
                .Where(x => x != null).GroupBy(x => x.TypeName).Select(x =>
                    new Choice { Id = x.Key, Label = x.Key ?? "Default" }).ToList();
            if (sourceTypes.Count == 0)
                sourceTypes.Add(new Choice { Id = null, Label = "Default (no source Type)" });
            SourceTypeBox.ItemsSource = sourceTypes;
            SourceTypeBox.SelectedIndex = sourceTypes.Count == 1 ? 0 : -1;

            List<Choice> destinationTypes = new List<Choice>();
            if (destination != null && destination.IsFamilyDocument)
            {
                destinationTypes.AddRange(destination.FamilyManager.Types.Cast<FamilyType>()
                    .Select(x => new Choice { Id = x.Name, Label = x.Name }));
            }
            if (destinationTypes.Count == 0)
                destinationTypes.Add(new Choice { Id = null, Label = "Default (created on transfer)" });
            DestinationTypeBox.ItemsSource = destinationTypes;
            string currentType = destination?.IsFamilyDocument == true
                ? destination.FamilyManager.CurrentType?.Name : null;
            DestinationTypeBox.SelectedItem = destinationTypes.FirstOrDefault(x => x.Id == currentType)
                ?? destinationTypes[0];
            ready = true;
            RefreshTransferPreview();
        }

        private void PopulatePrecisionAndSymbols()
        {
            Choice unit = UnitBox.SelectedItem as Choice;
            if (unit == null) return;
            ForgeTypeId unitId = new ForgeTypeId(unit.Id);
            FormatOptions format = new FormatOptions(unitId);
            double previous = PrecisionBox.SelectedItem is double
                ? (double)PrecisionBox.SelectedItem : destinationFormat?.Accuracy ?? 0.01;
            double[] candidates = { 100, 10, 1, 0.5, 0.25, 0.125, 0.1, 0.0625, 0.05,
                0.01, 0.001, 0.0001, 0.00001 };
            List<double> values = candidates.Concat(new[] { previous }).Distinct()
                .Where(format.IsValidAccuracy).OrderByDescending(x => x).ToList();
            PrecisionBox.ItemsSource = values;
            PrecisionBox.SelectedItem = values.FirstOrDefault(x => Math.Abs(x - previous) < 0.0000001);
            if (PrecisionBox.SelectedItem == null && values.Count > 0) PrecisionBox.SelectedIndex = 0;

            string priorSymbol = (SymbolBox.SelectedItem as Choice)?.Id ??
                destinationFormat?.GetSymbolTypeId().TypeId;
            List<Choice> symbols = new List<Choice>();
            foreach (ForgeTypeId id in FormatOptions.GetValidSymbols(unitId))
            {
                try { symbols.Add(new Choice { Id = id.TypeId,
                    Label = string.IsNullOrWhiteSpace(id.TypeId)
                        ? "No symbol" : LabelUtils.GetLabelForSymbol(id) }); }
                catch { }
            }
            SymbolBox.ItemsSource = symbols;
            SymbolBox.SelectedItem = symbols.FirstOrDefault(x => x.Id == priorSymbol) ??
                symbols.FirstOrDefault();
        }

        private TransferOptions GetOptions()
        {
            bool change = ChangeFormatCheck.IsChecked == true;
            Choice source = SourceTypeBox.SelectedItem as Choice;
            Choice target = DestinationTypeBox.SelectedItem as Choice;
            return new TransferOptions
            {
                GroupTypeId = change ? (GroupBox.SelectedItem as Choice)?.Id
                    : string.IsNullOrWhiteSpace(selectedParameter.GroupTypeId)
                        ? GroupTypeId.Data.TypeId : selectedParameter.GroupTypeId,
                IsInstance = change ? ScopeBox.SelectedIndex == 1 : selectedParameter.IsInstance,
                ChangeFormat = change,
                UnitTypeId = change && canChangeUnits ? (UnitBox.SelectedItem as Choice)?.Id : null,
                Accuracy = change && canChangeUnits && PrecisionBox.SelectedItem is double
                    ? (double?)PrecisionBox.SelectedItem : null,
                SymbolTypeId = change && canChangeUnits ? (SymbolBox.SelectedItem as Choice)?.Id : null,
                CopyValue = CopyDataCheck.IsChecked == true && CopyValueRadio.IsChecked == true,
                CopyFormula = CopyDataCheck.IsChecked == true && CopyFormulaRadio.IsChecked == true,
                SourceTypeName = source?.Id,
                MatchTypes = MatchTypesCheck.IsChecked == true,
                DestinationTypeName = MatchTypesCheck.IsChecked == true ? null : target?.Id,
                FormulaText = FormulaBox.Text.Trim(),
                SourceParameterNames = (selectedFamily.Parameters ?? new List<ParameterResult>())
                    .Where(x => x != null && !string.IsNullOrWhiteSpace(x.Name))
                    .Select(x => x.Name).ToList()
            };
        }

        private void RefreshTransferPreview()
        {
            if (!ready || selectedParameter == null || step != 3) return;
            bool change = ChangeFormatCheck.IsChecked == true;
            bool data = CopyDataCheck.IsChecked == true;
            FormatFields.Visibility = change ? UiVisibility.Visible : UiVisibility.Collapsed;
            FormatNote.Visibility = change ? UiVisibility.Visible : UiVisibility.Collapsed;
            DataFields.Visibility = data ? UiVisibility.Visible : UiVisibility.Collapsed;
            Choice source = SourceTypeBox.SelectedItem as Choice;
            FamilyTypeValueResult value = selectedParameter.TypeValues?.FirstOrDefault(x =>
                x != null && x.TypeName == source?.Id);
            bool sourceChosen = SourceTypeBox.SelectedIndex >= 0;
            bool hasValue = value?.HasValue == true;
            CopyValueRadio.IsEnabled = data && sourceChosen && hasValue;
            CopyFormulaRadio.IsEnabled = data && sourceChosen;
            if (!CopyValueRadio.IsEnabled) CopyValueRadio.IsChecked = false;
            if (!CopyFormulaRadio.IsEnabled) CopyFormulaRadio.IsChecked = false;
            bool copyValue = data && CopyValueRadio.IsChecked == true;
            bool copyFormula = data && CopyFormulaRadio.IsChecked == true;
            FormatNote.Text = (canChangeUnits
                ? "Display units affect all parameters with this data type in the destination family."
                : "This data type has no editable unit display. Group and scope can still be changed.") +
                (copyFormula ? " The formula will be tested with these settings before transfer."
                    : !string.IsNullOrWhiteSpace(selectedParameter.Formula)
                        ? " The source formula is not copied unless Copy formula is selected." : "");
            MatchTypesCheck.IsEnabled = copyValue && destination != null &&
                destination.IsFamilyDocument && destination.FamilyManager.Types.Cast<FamilyType>().Any();
            if (!MatchTypesCheck.IsEnabled) MatchTypesCheck.IsChecked = false;
            bool matchTypes = MatchTypesCheck.IsChecked == true;
            DestinationTypeBox.IsEnabled = !matchTypes;
            ValueFields.Visibility = copyValue ? UiVisibility.Visible : UiVisibility.Collapsed;
            FormulaFields.Visibility = copyFormula ? UiVisibility.Visible : UiVisibility.Collapsed;
            SourceValueText.Text = !sourceChosen ? "Choose a source Type" :
                hasValue ? value.DisplayValue ?? "(no display value)" : "Default / unset";
            Choice target = DestinationTypeBox.SelectedItem as Choice;
            ValueNote.Text = matchTypes
                ? "Values go to destination Types with matching names."
                : target?.Id == null
                    ? "A Default Type will be created to receive the value."
                    : "Value goes to the selected destination Type.";
            TransferOptions options = GetOptions();
            string type = TypeLabel(selectedParameter.DataTypeId);
            string sourceValue = sourceChosen ? SourceValueText.Text : "Choose a source Type";
            SourceFamilyPreview.Text = selectedFamily.FileName ?? "—";
            SourceParameterPreview.Text = selectedParameter.Name ?? "—";
            SourceDatatypePreview.Text = type;
            SourceGroupPreview.Text = GroupLabel(selectedParameter.GroupTypeId);
            SourceScopePreview.Text = selectedParameter.IsInstance ? "Instance" : "Type";
            SourceFormatPreview.Text = FormatLabel(selectedParameter.Format);
            SourceValuePreview.Text = sourceValue;
            SourceFormulaPreview.Text = string.IsNullOrWhiteSpace(selectedParameter.Formula)
                ? "None" : selectedParameter.Formula;
            string format = change && canChangeUnits
                ? (UnitBox.SelectedItem as Choice)?.Label + " · " +
                  Convert.ToString(PrecisionBox.SelectedItem, CultureInfo.InvariantCulture)
                : FormatLabel(destinationFormat);
            string destinationValue = copyValue ? matchTypes
                ? "Values by matching Type" : sourceValue : "Default / unset";
            DestinationFamilyPreview.Text = destination?.Title ?? "—";
            DestinationParameterPreview.Text = selectedParameter.Name ?? "—";
            DestinationDatatypePreview.Text = type;
            DestinationGroupPreview.Text = change
                ? (GroupBox.SelectedItem as Choice)?.Label ?? "—" : GroupLabel(options.GroupTypeId);
            DestinationScopePreview.Text = options.IsInstance ? "Instance" : "Type";
            DestinationFormatPreview.Text = format ?? "—";
            DestinationValuePreview.Text = destinationValue;
            DestinationFormulaPreview.Text = copyFormula ? options.FormulaText : "None";
            if (copyFormula)
            {
                IEnumerable<string> names = destination != null && destination.IsFamilyDocument
                    ? destination.FamilyManager.GetParameters().Select(x => x.Definition.Name)
                        .Concat(new[] { selectedParameter.Name }) : new[] { selectedParameter.Name };
                var missing = FormulaDependencyChecker.MissingParameters(options.FormulaText,
                    options.SourceParameterNames, names);
                FormulaHint.Text = missing.Count == 0
                    ? "Referenced source parameters are present in the destination."
                    : "Missing in destination: " + string.Join(", ", missing);
                FormulaHint.Foreground = missing.Count == 0 ? (Brush)FindResource("Muted") :
                    (Brush)FindResource("AccentDark");
            }
            bool modeNeeded = data && sourceChosen && !copyValue && !copyFormula;
            string issue = modeNeeded ? "Choose Copy value or Copy formula." :
                data && !sourceChosen ? "Choose a source Type first." :
                ParameterTransfer.Check(uiapp, selectedParameter, options);
            TransferButton.IsEnabled = issue == null;
            StatusText.Text = issue ?? (destination != null && destination.IsFamilyDocument &&
                !destination.FamilyManager.Types.Cast<FamilyType>().Any() && (copyValue || copyFormula)
                    ? "Ready. A Default Type will be created in the destination family."
                    : "Ready to transfer.");
            StatusText.Foreground = (Brush)FindResource("AccentDark");
            if (activeBatchRow != null && transferredRows.Contains(activeBatchRow))
            {
                TransferButton.IsEnabled = false;
                StatusText.Text = PendingBatchCount == 0
                    ? "All selected parameters have been transferred."
                    : "This parameter has been transferred. Select a pending parameter above.";
            }
        }

        private static string GroupLabel(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return "Data";
            try { return LabelUtils.GetLabelForGroup(new ForgeTypeId(id)); }
            catch { return id; }
        }

        private string TypeLabel(string id)
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
            if (format == null || string.IsNullOrWhiteSpace(format.UnitTypeId))
                return "Default / not exported";
            return FormatLabel(format.UnitTypeId, format.Accuracy);
        }

        private static string FormatLabel(FormatOptions format)
        {
            return format == null ? "Family default" :
                FormatLabel(format.GetUnitTypeId().TypeId, format.Accuracy);
        }

        private static string FormatLabel(string id, double accuracy)
        {
            string unit = id;
            try { unit = LabelUtils.GetLabelForUnit(new ForgeTypeId(id)); }
            catch { }
            return unit + " · " + accuracy.ToString("G", CultureInfo.InvariantCulture);
        }

        private void TransferOptions_Changed(object sender, RoutedEventArgs e)
        {
            if (ready) RefreshTransferPreview();
        }

        private void ResetOptions_Click(object sender, RoutedEventArgs e)
        {
            if (activeBatchRow == null || transferredRows.Contains(activeBatchRow)) return;
            transferDrafts.Remove(activeBatchRow);
            PrepareTransfer();
            RefreshTransferPreview();
        }

        private void Unit_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (!ready) return;
            ready = false;
            PopulatePrecisionAndSymbols();
            ready = true;
            RefreshTransferPreview();
        }

        private void SourceType_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (ready) RefreshTransferPreview();
        }

        private void Formula_Changed(object sender, TextChangedEventArgs e)
        {
            if (FormulaResult != null) FormulaResult.Text = "";
            if (ready) RefreshTransferPreview();
        }

        private void CheckFormula_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string issue = ParameterTransfer.CheckFormula(uiapp, selectedParameter, GetOptions());
                FormulaResult.Text = issue == null
                    ? "Formula can be applied. The test was rolled back."
                    : issue;
                FormulaResult.Foreground = (Brush)FindResource("AccentDark");
            }
            catch (Exception error) { FormulaResult.Text = error.Message; }
        }

        private void Transfer_Click(object sender, RoutedEventArgs e)
        {
            if (step == 2)
            {
                List<ParameterRow> checkedRows = parameters.Where(x => x.IsChecked && x.CanTransfer).ToList();
                if (checkedRows.Count == 0 || checkedRows.GroupBy(x => x.Name,
                    StringComparer.OrdinalIgnoreCase).Any(x => x.Count() > 1)) return;
                batchRows.Clear();
                batchRows.AddRange(checkedRows);
                transferredRows.Clear();
                transferDrafts.Clear();
                activeBatchRow = null;
                TransferSuccessBanner.Visibility = UiVisibility.Collapsed;
                ShowBatchParameter(batchRows[0]);
                return;
            }
            if (step != 3 || activeBatchRow == null || transferredRows.Contains(activeBatchRow)) return;
            try
            {
                SaveCurrentDraft();
                TransferOptions options = GetOptions();
                if (options.CopyFormula)
                {
                    string formulaIssue = ParameterTransfer.CheckFormula(uiapp, selectedParameter, options);
                    if (formulaIssue != null)
                    {
                        FormulaResult.Text = formulaIssue;
                        StatusText.Text = formulaIssue;
                        return;
                    }
                }
                ParameterTransfer.Transfer(uiapp, selectedParameter, options);
                string createdName = activeBatchRow.Name;
                int currentIndex = batchRows.IndexOf(activeBatchRow);
                transferredRows.Add(activeBatchRow);
                activeBatchRow.IsChecked = false;
                RefreshParameters();
                ParameterRow next = NextPendingParameter(currentIndex + 1);
                if (next != null) ShowBatchParameter(next);
                else
                {
                    OptionsScroll.IsEnabled = false;
                    RefreshBatchPreview();
                    RefreshTransferPreview();
                    StatusText.Text = "All " + transferredRows.Count +
                        " parameters were transferred. You can review them or exit.";
                    StatusText.Foreground = (Brush)FindResource("AccentDark");
                }
                TransferSuccessText.Text = "Parameter “" + createdName + "” was created in the open family.";
                TransferSuccessBanner.Visibility = UiVisibility.Visible;
            }
            catch (Exception error) { TaskDialog.Show("Transfer parameter", error.Message); }
        }

        private void Back_Click(object sender, RoutedEventArgs e)
        {
            if (step == 3)
            {
                if (!ConfirmLeaveBatch()) return;
                batchRows.Clear();
                transferredRows.Clear();
                transferDrafts.Clear();
                activeBatchRow = null;
                RefreshParameters();
            }
            ShowStep(step == 1 ? -1 : step == -1 ? -2 : step - 1);
        }

        private bool ConfirmLeaveBatch()
        {
            if (step != 3 || PendingBatchCount == 0) return true;
            ConfirmLeaveWindow dialog = new ConfirmLeaveWindow(PendingBatchCount, transferredRows.Count)
            {
                Owner = this
            };
            return dialog.ShowDialog() == true;
        }

        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            if (!ConfirmLeaveBatch()) e.Cancel = true;
            else if (step == 0 && loadingCancellation != null)
                loadingCancellation.Cancel();
        }

        private void Minimize_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void Maximize_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState == WindowState.Maximized
                ? WindowState.Normal : WindowState.Maximized;
        }

        private void CloseWindow_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void Window_StateChanged(object sender, EventArgs e)
        {
            if (MaximizeButton == null) return;
            bool maximized = WindowState == WindowState.Maximized;
            MaximizeButton.ToolTip = maximized ? "Restore" : "Maximize";
            System.Windows.Shapes.Path icon = MaximizeButton.Content as System.Windows.Shapes.Path;
            if (icon != null)
                icon.Data = Geometry.Parse(maximized
                    ? "M3,1 L11,1 11,9 9,9 M1,3 L9,3 9,11 1,11 Z"
                    : "M1,1 L11,1 11,10 1,10 Z");
        }
    }
}
