using System.Windows;
using System.Windows.Input;

namespace Export_Data_From_Family.UI.Wpf
{
    public partial class ConfirmLeaveWindow : Window
    {
        public ConfirmLeaveWindow(int pendingCount, int transferredCount)
        {
            InitializeComponent();
            WarningText.Text = pendingCount == 1
                ? "1 parameter has not been transferred yet."
                : pendingCount + " parameters have not been transferred yet.";
            DetailText.Text = transferredCount > 0
                ? transferredCount + " transferred parameter" +
                  (transferredCount == 1 ? " remains" : "s remain") +
                  " in the current family. Leave this transfer list?"
                : "Leave this transfer list? You can return to the parameter selection screen.";
        }

        private void Header_Drag(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed) DragMove();
        }

        private void Leave_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }

        private void KeepWorking_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
