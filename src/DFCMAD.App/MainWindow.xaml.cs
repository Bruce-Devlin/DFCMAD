using System.ComponentModel;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;
using DFCMAD.App.ViewModels;

namespace DFCMAD.App;

public partial class MainWindow : Window
{
    private readonly IServiceProvider _serviceProvider;
    private readonly MainViewModel _viewModel;
    private bool _allowClose;

    public MainWindow(MainViewModel viewModel, IServiceProvider serviceProvider)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _serviceProvider = serviceProvider;
        DataContext = viewModel;

        viewModel.DiagnosticsRequested += (_, _) => ShowDiagnostics();
        viewModel.OpenLogsRequested += (_, _) => viewModel.OpenLogFolder();
        viewModel.HideRequested += (_, _) => Hide();

        Loaded += (_, _) => PositionNearPrimaryWorkArea();
        SourceInitialized += (_, _) => PositionNearPrimaryWorkArea();
        SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
    }

    public void ShowPanel()
    {
        PositionNearPrimaryWorkArea();
        Show();
        Activate();
    }

    public void TogglePanel()
    {
        if (IsVisible)
        {
            Hide();
        }
        else
        {
            ShowPanel();
        }
    }

    public void AllowClose()
    {
        _allowClose = true;
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!_allowClose)
        {
            e.Cancel = true;
            Hide();
            return;
        }

        SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
        base.OnClosing(e);
    }

    private void OnDisplaySettingsChanged(object? sender, EventArgs e)
    {
        Dispatcher.Invoke(PositionNearPrimaryWorkArea);
    }

    private void PositionNearPrimaryWorkArea()
    {
        var workArea = SystemParameters.WorkArea;
        Left = Math.Max(workArea.Left + 8, workArea.Right - ActualWidth - 16);
        Top = Math.Max(workArea.Top + 8, workArea.Bottom - ActualHeight - 16);
    }

    private void ShowDiagnostics()
    {
        var window = _serviceProvider.GetRequiredService<Views.DiagnosticsWindow>();
        if (IsLoaded && IsVisible)
        {
            window.Owner = this;
        }

        window.Show();
        window.Activate();
    }
}
