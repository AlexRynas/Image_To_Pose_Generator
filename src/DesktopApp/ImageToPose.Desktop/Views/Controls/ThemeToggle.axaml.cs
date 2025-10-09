using Avalonia.Controls;
using ImageToPose.Desktop.Models;

namespace ImageToPose.Desktop.Views.Controls;

public partial class ThemeToggle : UserControl
{
    public ThemeToggle()
    {
        InitializeComponent();
        
        // Initialize switch state
        ThemeSwitch.IsChecked = App.ThemeService.Current == AppTheme.Dark;
        
        // Wire up event using IsCheckedChanged
        ThemeSwitch.IsCheckedChanged += (_, e) =>
        {
            if (ThemeSwitch.IsChecked == true)
                App.ThemeService.Apply(AppTheme.Dark);
            else
                App.ThemeService.Apply(AppTheme.Light);
        };
    }
}
