using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.ReactiveUI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NexusMods.App.UI;
using NexusMods.App.UI.Converters;
using NexusMods.App.UI.Windows;
using NexusMods.Sdk;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using Projektanker.Icons.Avalonia;
using Projektanker.Icons.Avalonia.MaterialDesign;
using R3;
using ReactiveUI;
using Splat;

namespace NexusMods.App;



// ReSharper disable once ClassNeverInstantiated.Global
public class Startup
{
    private static bool _hasBeenSetup = false;
    private static IServiceProvider _provider = null!;
    private static ILogger<Startup> _logger = null!;
    private static ulong _windowCount;

#pragma warning disable CS0028 // Disables warning about not being a valid entry point
    
    /// <summary>
    /// Main entry point for the application, the application loop will only exit when the token is cancelled.
    /// </summary>
    public static void Main(IServiceProvider provider, string[] args)
    {
        if (_hasBeenSetup)
            throw new InvalidOperationException("Startup has already been called!");
        
        _hasBeenSetup = true;
        _provider = provider;
        _logger = provider.GetRequiredService<ILogger<Startup>>();
        
        var logger = provider.GetRequiredService<ILogger<Startup>>();
        logger.LogInformation("Version: {Version} Commit: {CommitHash} Build Date: {BuildDate}", ApplicationConstants.Version, ApplicationConstants.CommitHash, ApplicationConstants.BuildDate);

        var builder = BuildAvaloniaApp(provider);
        
        // NOTE(erri120): DI is lazy by default and these services
        // do additional initialization inside their constructors.
        // We need to make sure their constructors are called to
        // finalize our OpenTelemetry configuration.
        provider.GetService<MeterProvider>();
        provider.GetService<TracerProvider>();

        try
        {
            builder.StartWithClassicDesktopLifetime(args);
        }
        catch (Exception e)
        {
            Debugger.Break();
            logger.LogCritical(e, "Exception crashed the application!");
        }
    }
#pragma warning restore CS0028

    private static void AppMain(Application app, string[] args)
    {
        _logger.LogInformation("Starting UI application");
        ShowMainWindow();

        // Start the app, linking to the global shutdown token
        _logger.LogInformation("Starting application loop");
        app.Run(MainThreadData.GlobalShutdownToken);
        _logger.LogInformation("Application loop ended");
    }

    private static void ShowMainWindow()
    {
        // TODO: enable multi-window support
        // https://github.com/Nexus-Mods/NexusMods.App/issues/1267
        if (Interlocked.Read(ref _windowCount) > 0)
        {
            _logger.LogError("We currently only allow 1 MainWindow. See https://github.com/Nexus-Mods/NexusMods.App/issues/1267 for details");
            return;
        }

        var reactiveWindow = _provider.GetRequiredService<MainWindow>();
        reactiveWindow.ViewModel = _provider.GetRequiredService<MainWindowViewModel>();
        reactiveWindow.Show();
    }

    public static AppBuilder BuildAvaloniaApp(IServiceProvider serviceProvider)
    {
        var logger = serviceProvider.GetRequiredService<ILogger<Startup>>();
        ReactiveUiExtensions.DefaultLogger = logger;
        IconProvider.Current.Register<MaterialDesignIconProvider>();

        var app = AppBuilder
            .Configure(serviceProvider.GetRequiredService<App>)
            .UsePlatformDetect()
            .With(new X11PlatformOptions
            {
                // NOTE(erri120): Prevents DBus exceptions for tooltips.
                // For details see https://github.com/Nexus-Mods/NexusMods.App/issues/2799
                UseDBusMenu = false,
            })
            .With(new SkiaOptions
            {
                // NOTE(insomnious): Opacity doesn't work on SVGs without this SkiaOptions set. It's needed for when we want to disable\fade SVG icons.
                // For details see https://github.com/AvaloniaUI/Avalonia/pull/9964
                UseOpacitySaveLayer = true,
            })
            .LogToTrace()
            .UseR3(unhandledExceptionHandler: exception =>
            {
                LogMessages.R3UnhandledException(logger, exception);
            })
            .UseReactiveUI();

        Locator.CurrentMutable.UnregisterCurrent(typeof(IViewLocator));
        Locator.CurrentMutable.Register(serviceProvider.GetRequiredService<InjectedViewLocator>, typeof(IViewLocator));
        
        Locator.CurrentMutable.RegisterConstant<IBindingTypeConverter>(new SizeToStringTypeConverter());
        


        return app;
    }
}
