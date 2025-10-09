using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ImageToPose.Desktop.ViewModels;

public partial class ReviewViewModel : ViewModelBase
{
    private readonly WizardViewModel _wizard;

    [ObservableProperty]
    private string _extendedPoseText = string.Empty;

    public ReviewViewModel(WizardViewModel wizard)
    {
        _wizard = wizard;
    }

    [RelayCommand]
    private void Continue()
    {
        // Pass the edited text to the generate view model
        _wizard.GenerateViewModel.ExtendedPoseDescription = ExtendedPoseText;
        
        // Navigate to generate step
        _wizard.NavigateToStep(WizardStep.Generate);
    }

    public bool CanContinue => !string.IsNullOrWhiteSpace(ExtendedPoseText);
}
