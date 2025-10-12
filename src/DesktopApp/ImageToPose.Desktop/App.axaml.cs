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
using Microsoft.Extensions.Logging;
using Serilog;

namespace ImageToPose.Desktop;

public partial class App : Application
{
    public static IServiceProvider? Services { get; private set; }
    public static IThemeService ThemeService { get; private set; } = default!;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
        
        // Configure services
        var services = new ServiceCollection();
        
        // Logging first
        services.AddLogging(builder =>
        {
            builder.ClearProviders();
            builder.AddSerilog(dispose: true);
        });

        // Core services
        services.AddSingleton<ISettingsService, SettingsService>();
        services.AddSingleton<IPromptLoader, PromptLoader>();
        services.AddSingleton<IPriceEstimator, PriceEstimator>();
        services.AddSingleton<IOpenAIService, OpenAIService>();
        services.AddSingleton<IOpenAIErrorHandler, OpenAIErrorHandler>();
        
        // Desktop services
        services.AddSingleton<IFileService, FileService>();
        
        // Build once to get logger
        var tempProvider = services.BuildServiceProvider();
        var loggerFactory = tempProvider.GetRequiredService<ILoggerFactory>();
        var themeLogger = loggerFactory.CreateLogger<ThemeService>();
        
        // Initialize theme service with logger
        ThemeService = new ThemeService(this, themeLogger);
        
        // Add theme service to DI
        services.AddSingleton<IThemeService>(ThemeService);
        
        // ViewModels
        services.AddTransient<WizardViewModel>();
        
        // Build final service provider
        Services = services.BuildServiceProvider();
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