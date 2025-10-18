using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ImageToPose.Desktop.ViewModels;

public partial class ReviewViewModel : ViewModelBase
{
    private readonly WizardViewModel _wizard;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanContinue))]
    private string _extendedPoseText = string.Empty;

    [ObservableProperty]
    private string _imagePath = string.Empty;

    public ReviewViewModel(WizardViewModel wizard)
    {
        _wizard = wizard;
    }

    [RelayCommand]
    private void Continue()
    {
        // Pass the edited text and image path to the generate view model
        _wizard.GenerateViewModel.ExtendedPoseDescription = ExtendedPoseText;
        _wizard.GenerateViewModel.ImagePath = ImagePath;
        
        // Navigate to generate step
        _wizard.NavigateToStep(WizardStep.Generate);
    }

    public bool CanContinue => !string.IsNullOrWhiteSpace(ExtendedPoseText);
}
