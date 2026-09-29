using System.Windows;
using System.Windows.Input;
using DFCMAD.App.ViewModels;

namespace DFCMAD.App.Views;

public partial class DiagnosticsWindow : Window
{
    private readonly DiagnosticsViewModel _viewModel;

    public DiagnosticsWindow(DiagnosticsViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
        Loaded += async (_, _) => await _viewModel.RefreshAsync(CancellationToken.None).ConfigureAwait(true);
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }
}
