using System.Drawing;
using System.Windows;
using DFCMAD.App.ViewModels;
using DFCMAD.Core.Models;
using DFCMAD.Core.Services;
using Microsoft.Extensions.Logging;
using Forms = System.Windows.Forms;
using Application = System.Windows.Application;

namespace DFCMAD.App.Tray;

public sealed class TrayIconService : IDisposable
{
    private readonly MainWindow _mainWindow;
    private readonly MainViewModel _mainViewModel;
    private readonly IAudioEnforcementService _enforcementService;
    private readonly ISettingsService _settingsService;
    private readonly IStartupRegistrationService _startupRegistrationService;
    private readonly ILogger<TrayIconService> _logger;
    private Forms.NotifyIcon? _notifyIcon;
    private Forms.ToolStripMenuItem? _pauseItem;
    private Forms.ToolStripMenuItem? _resumeItem;
    private Forms.ToolStripMenuItem? _startupItem;

    public TrayIconService(
        MainWindow mainWindow,
        MainViewModel mainViewModel,
        IAudioEnforcementService enforcementService,
        ISettingsService settingsService,
        IStartupRegistrationService startupRegistrationService,
        ILogger<TrayIconService> logger)
    {
        _mainWindow = mainWindow;
        _mainViewModel = mainViewModel;
        _enforcementService = enforcementService;
        _settingsService = settingsService;
        _startupRegistrationService = startupRegistrationService;
        _logger = logger;
        _mainViewModel.FirstSetupCompleted += (_, _) => ShowBalloon("DFCMAD is enforcing your selected audio devices.");
        _enforcementService.StateChanged += (_, state) => Application.Current.Dispatcher.Invoke(() => UpdateStatus(state));
    }

    public void Initialize()
    {
        try
        {
            _notifyIcon = new Forms.NotifyIcon
            {
                Icon = SystemIcons.Application,
                Text = "DFCMAD",
                Visible = true,
                ContextMenuStrip = BuildContextMenu()
            };
            _notifyIcon.MouseUp += OnMouseUp;
            _logger.LogInformation("Tray icon initialized.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Tray icon failed to initialize.");
        }
    }

    public void ShowBalloon(string message)
    {
        try
        {
            _notifyIcon?.ShowBalloonTip(3000, "DFCMAD", message, Forms.ToolTipIcon.Info);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Tray notification failed.");
        }
    }

    public void Dispose()
    {
        if (_notifyIcon is null)
        {
            return;
        }

        _notifyIcon.Visible = false;
        _notifyIcon.MouseUp -= OnMouseUp;
        _notifyIcon.Dispose();
        _notifyIcon = null;
    }

    private Forms.ContextMenuStrip BuildContextMenu()
    {
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("Open DFCMAD", null, (_, _) => _mainWindow.ShowPanel());
        menu.Items.Add("Enforce now", null, async (_, _) => await _enforcementService.EnforceNowAsync(EnforcementReason.Manual, CancellationToken.None).ConfigureAwait(true));
        _pauseItem = new Forms.ToolStripMenuItem("Pause enforcement", null, async (_, _) => await _enforcementService.PauseAsync(TimeSpan.FromMinutes(5), CancellationToken.None).ConfigureAwait(true));
        _resumeItem = new Forms.ToolStripMenuItem("Resume enforcement", null, async (_, _) => await _enforcementService.ResumeAsync(CancellationToken.None).ConfigureAwait(true));
        _startupItem = new Forms.ToolStripMenuItem("Run at startup", null, (_, _) => ToggleStartup())
        {
            CheckOnClick = false,
            Checked = _settingsService.Current.StartWithWindowsEnabled
        };
        menu.Items.Add(_pauseItem);
        menu.Items.Add(_resumeItem);
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add(_startupItem);
        menu.Items.Add("Diagnostics", null, (_, _) => _mainViewModel.OpenDiagnosticsCommand.Execute(null));
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) =>
        {
            _mainWindow.AllowClose();
            Application.Current.Shutdown();
        });
        UpdateStatus(_enforcementService.State);
        return menu;
    }

    private void OnMouseUp(object? sender, Forms.MouseEventArgs e)
    {
        if (e.Button == Forms.MouseButtons.Left)
        {
            _mainWindow.TogglePanel();
        }
    }

    private void ToggleStartup()
    {
        try
        {
            var enable = !_settingsService.Current.StartWithWindowsEnabled;
            if (enable)
            {
                _startupRegistrationService.Enable();
            }
            else
            {
                _startupRegistrationService.Disable();
            }

            _settingsService.Current.StartWithWindowsEnabled = enable;
            _settingsService.SaveAsync(CancellationToken.None).GetAwaiter().GetResult();
            if (_startupItem is not null)
            {
                _startupItem.Checked = enable;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to toggle startup registration from tray.");
        }
    }

    private void UpdateStatus(EnforcementState state)
    {
        if (_notifyIcon is not null)
        {
            _notifyIcon.Text = state.Status switch
            {
                EnforcementStatus.Paused => "DFCMAD - Paused",
                EnforcementStatus.WaitingForDevice => "DFCMAD - Waiting for device",
                EnforcementStatus.Error => "DFCMAD - Error",
                _ => "DFCMAD - Enforcing"
            };
        }

        if (_pauseItem is not null)
        {
            _pauseItem.Enabled = state.Status != EnforcementStatus.Paused;
        }

        if (_resumeItem is not null)
        {
            _resumeItem.Enabled = state.Status == EnforcementStatus.Paused;
        }

        if (_startupItem is not null)
        {
            _startupItem.Checked = _settingsService.Current.StartWithWindowsEnabled;
        }
    }
}
