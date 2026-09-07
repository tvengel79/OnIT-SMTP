using System.Windows;
using System.Windows.Controls;

namespace OnIT.Smtp.ConfigTool.Views;

public partial class AboutView : UserControl
{
    // Re-armed whenever the selection isn't the full "OnIT Belgium" text, so the easter egg
    // can be found again by anyone who deselects (or partially selects) and then selects it
    // all -- just not spammed on every intermediate step of a single drag-to-select gesture.
    private bool _easterEggArmed = true;

    public AboutView()
    {
        InitializeComponent();
    }

    private void OnItBelgiumTextBox_SelectionChanged(object sender, RoutedEventArgs e)
    {
        var textBox = (TextBox)sender;
        var isFullySelected = textBox.SelectionLength > 0 && textBox.SelectionLength == textBox.Text.Length;

        if (!isFullySelected)
        {
            _easterEggArmed = true;
            return;
        }

        if (!_easterEggArmed) return;
        _easterEggArmed = false;

        MessageBox.Show(
            "This software is developed and will always be owned by Tim Van Engeland.\n\n" +
            "OnIT Belgium holds usage rights for as long as Tim is employed there.",
            "You found something...",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }
}
