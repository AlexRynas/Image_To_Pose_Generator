using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using ImageToPose.Desktop.ViewModels;
using ImageToPose.Desktop.Views;
using ImageToPose.Desktop.Services;
using ImageToPose.Core.Services;
using Microsoft.Extensions.DependencyInjection;

namespace ImageToPose.Desktop;

public partial class App : Application
{
    public static IServiceProvider? Services { get; private set; }
    public static IThemeService ThemeService { get; private set; } = default!;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
        
        // Initialize theme service before DI
        ThemeService = new ThemeService(this);
        
        // Configure services
        var services = new ServiceCollection();
        ConfigureServices(services);
        Services = services.BuildServiceProvider();
    }

    private void ConfigureServices(IServiceCollection services)
    {
        // Core services
        services.AddSingleton<ISettingsService, SettingsService>();
        services.AddSingleton<IPromptLoader, PromptLoader>();
        services.AddSingleton<IPriceEstimator, JsonPricingEstimator>();
        services.AddSingleton<IOpenAIService, OpenAIService>();
        
        // Desktop services
        services.AddSingleton<IFileService, FileService>();
        services.AddSingleton<IThemeService>(ThemeService);
        
        // ViewModels
        services.AddTransient<WizardViewModel>();
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            DisableAvaloniaDataAnnotationValidation();
            
            var wizardViewModel = Services!.GetRequiredService<WizardViewModel>();
            desktop.MainWindow = new MainWindow
            {
                DataContext = wizardViewModel
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void DisableAvaloniaDataAnnotationValidation()
    {
        // Get an array of plugins to remove
        var dataValidationPluginsToRemove =
            BindingPlugins.DataValidators.OfType<DataAnnotationsValidationPlugin>().ToArray();

        // remove each entry found
        foreach (var plugin in dataValidationPluginsToRemove)
        {
            BindingPlugins.DataValidators.Remove(plugin);
        }
    }
}