using System.IO;
using System.Windows;
using NotSoBright.Models;
using NotSoBright.Services;
using Serilog;

namespace NotSoBright;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : System.Windows.Application
{
	private TrayService? _trayService;
	private MainWindow? _mainWindow;
	private ConfigService? _configService;
	private AppConfig? _config;
	private FullscreenDetectionService? _fullscreenDetectionService;
	private bool _overlayHiddenForFullscreen;

	protected override void OnStartup(StartupEventArgs e)
	{
		base.OnStartup(e);

		DispatcherUnhandledException += App_DispatcherUnhandledException;
		AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;

		Log.Logger = new LoggerConfiguration()
			.WriteTo.File(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "NotSoBright", "logs", "log-.txt"), rollingInterval: RollingInterval.Day)
			.CreateLogger();

		Log.Information("Application starting");

		try
		{
			_configService = new ConfigService();
			_config = _configService.Load();
			_mainWindow = new MainWindow(_config, _configService);
			_mainWindow.Show();
			_trayService = new TrayService(_mainWindow);
			_fullscreenDetectionService = new FullscreenDetectionService(
				() => _mainWindow.IsVisible,
				() => OnExclusiveFullscreenDetected(),
				() => OnExclusiveFullscreenExited());
		}
		catch (Exception ex)
		{
			Log.Fatal(ex, "Failed to start application");
			System.Windows.MessageBox.Show("Failed to start NotSoBright. Check logs for details.", "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
			Shutdown();
		}
	}

	protected override void OnExit(ExitEventArgs e)
	{
		Log.Information("Application exiting");
		_fullscreenDetectionService?.Dispose();
		_trayService?.Dispose();
		Log.CloseAndFlush();
		base.OnExit(e);
	}

	private void OnExclusiveFullscreenDetected()
	{
		if (_trayService is null || _mainWindow is null)
		{
			return;
		}

		_trayService.ShowFullscreenBlockedOnce();

		if (_mainWindow.IsVisible)
		{
			_overlayHiddenForFullscreen = true;
			_mainWindow.Hide();
		}
	}

	private void OnExclusiveFullscreenExited()
	{
		if (!_overlayHiddenForFullscreen || _mainWindow is null)
		{
			return;
		}

		_overlayHiddenForFullscreen = false;
		_mainWindow.ShowOverlay();
	}

	private void App_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
	{
		Log.Error(e.Exception, "Unhandled dispatcher exception");
		System.Windows.MessageBox.Show($"An unexpected error occurred: {e.Exception.Message}", "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
		e.Handled = true;
	}

	private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
	{
		var ex = e.ExceptionObject as Exception;
		Log.Fatal(ex, "Unhandled exception");
		System.Windows.MessageBox.Show("A critical error occurred. The application will close.", "Fatal Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
	}
}

