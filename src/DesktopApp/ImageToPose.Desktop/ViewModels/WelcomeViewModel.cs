using CommunityToolkit.Mvvm.Input;

namespace ImageToPose.Desktop.ViewModels;

public partial class WelcomeViewModel : ViewModelBase
{
    private readonly WizardViewModel _wizard;

    public WelcomeViewModel(WizardViewModel wizard)
    {
        _wizard = wizard;
    }

    [RelayCommand]
    private void GetStarted()
    {
        _wizard.NavigateToStep(WizardStep.ApiKey);
    }
}
