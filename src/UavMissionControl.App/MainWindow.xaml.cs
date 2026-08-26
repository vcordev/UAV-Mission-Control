using System.Windows;
using UavMissionControl.App.ViewModels;

namespace UavMissionControl.App;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainViewModel();
    }
}
