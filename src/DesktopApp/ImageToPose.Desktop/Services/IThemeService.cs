using ImageToPose.Desktop.Models;

namespace ImageToPose.Desktop.Services;

public interface IThemeService
{
    AppTheme Current { get; }
    void Apply(AppTheme theme, bool persist = true);
    void Toggle();
}
