using System.Windows;
using OnIT.Smtp.ConfigTool.ViewModels;

namespace OnIT.Smtp.ConfigTool;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainViewModel();
    }
}
