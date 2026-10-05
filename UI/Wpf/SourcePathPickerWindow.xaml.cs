using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Export_Data_From_Family.UI.Wpf
{
    public partial class SourcePathPickerWindow : Window
    {
        private readonly bool jsonMode;
        public string SelectedPath { get; private set; }

        public SourcePathPickerWindow(bool jsonMode, string initialPath)
        {
            this.jsonMode = jsonMode;
            InitializeComponent();
            PickerTitle.Text = jsonMode ? "Choose an existing JSON report" : "Choose a family folder";
            PickerHint.Text = jsonMode
                ? "Select a report file or paste its full path."
                : "Select the folder containing Revit family files (.rfa).";
            FilesPanel.Visibility = jsonMode ? Visibility.Visible : Visibility.Collapsed;
            foreach (DriveInfo drive in DriveInfo.GetDrives())
            {
                try
                {
                    if (drive.IsReady)
                        FolderTree.Items.Add(CreateNode(drive.RootDirectory.FullName));
                }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
            PathBox.Text = initialPath ?? "";
        }

        private TreeViewItem CreateNode(string path)
        {
            TreeViewItem node = new TreeViewItem
            {
                Header = Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar)) == ""
                    ? path : Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar)),
                Tag = path,
                Padding = new Thickness(5, 3, 5, 3)
            };
            node.Items.Add("Loading");
            node.Expanded += Node_Expanded;
            return node;
        }

        private void Node_Expanded(object sender, RoutedEventArgs e)
        {
            TreeViewItem node = sender as TreeViewItem;
            if (node == null || node.Items.Count != 1 || !(node.Items[0] is string)) return;
            node.Items.Clear();
            try
            {
                foreach (string directory in Directory.GetDirectories((string)node.Tag)
                    .OrderBy(x => x, StringComparer.CurrentCultureIgnoreCase))
                    node.Items.Add(CreateNode(directory));
            }
            catch (UnauthorizedAccessException) { }
            catch (IOException) { }
        }

        private void FolderTree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            TreeViewItem node = e.NewValue as TreeViewItem;
            if (node == null) return;
            string path = (string)node.Tag;
            if (!jsonMode) PathBox.Text = path;
            else
            {
                FileList.Items.Clear();
                try
                {
                    foreach (string file in Directory.GetFiles(path, "*.json")
                        .OrderBy(x => x, StringComparer.CurrentCultureIgnoreCase))
                        FileList.Items.Add(new ListBoxItem { Content = Path.GetFileName(file), Tag = file });
                }
                catch (UnauthorizedAccessException) { }
                catch (IOException) { }
            }
        }

        private void FileList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ListBoxItem item = FileList.SelectedItem as ListBoxItem;
            if (item != null) PathBox.Text = (string)item.Tag;
        }

        private void PathBox_Changed(object sender, TextChangedEventArgs e)
        {
            if (ValidationText != null) ValidationText.Text = "";
        }

        private void Accept_Click(object sender, RoutedEventArgs e)
        {
            string path = PathBox.Text.Trim().Trim('"');
            if (jsonMode ? !File.Exists(path) || !path.EndsWith(".json", StringComparison.OrdinalIgnoreCase)
                : !Directory.Exists(path))
            {
                ValidationText.Text = jsonMode ? "Choose an existing JSON file." : "Choose an existing folder.";
                return;
            }
            SelectedPath = path;
            DialogResult = true;
        }

        private void Cancel_Click(object sender, RoutedEventArgs e) { DialogResult = false; }

        private void Header_Drag(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed) DragMove();
        }
    }
}
