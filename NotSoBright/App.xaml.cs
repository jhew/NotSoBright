using System.Windows;
using NotSoBright.Models;
using NotSoBright.Services;

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

	protected override void OnExit(ExitEventArgs e)
	{
		_fullscreenDetectionService?.Dispose();
		_trayService?.Dispose();
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
}

