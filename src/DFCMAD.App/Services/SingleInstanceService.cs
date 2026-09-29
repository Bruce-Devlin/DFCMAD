using System.IO.Pipes;
using System.Text;

namespace DFCMAD.App.Services;

public sealed class SingleInstanceService : IDisposable
{
    private const string MutexName = @"Local\DFCMAD.SingleInstance";
    private const string PipeName = "DFCMAD.ShowPanel";
    private readonly Mutex _mutex;
    private CancellationTokenSource? _serverCts;
    private Task? _serverTask;

    public SingleInstanceService()
    {
        _mutex = new Mutex(initiallyOwned: true, MutexName, out var createdNew);
        HasOwnership = createdNew;
    }

    public bool HasOwnership { get; }

    public event EventHandler? ShowRequested;

    public static async Task SignalExistingInstanceAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var client = new NamedPipeClientStream(".", PipeName, PipeDirection.Out, PipeOptions.Asynchronous);
            await client.ConnectAsync(800, cancellationToken).ConfigureAwait(false);
            var bytes = Encoding.UTF8.GetBytes("show");
            await client.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            // The existing process may still be starting. The second instance should still exit cleanly.
        }
    }

    public void StartServer()
    {
        if (!HasOwnership)
        {
            return;
        }

        _serverCts = new CancellationTokenSource();
        _serverTask = Task.Run(() => RunServerAsync(_serverCts.Token));
    }

    private async Task RunServerAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await using var server = new NamedPipeServerStream(PipeName, PipeDirection.In, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
                await server.WaitForConnectionAsync(cancellationToken).ConfigureAwait(false);
                ShowRequested?.Invoke(this, EventArgs.Empty);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch
            {
                await Task.Delay(500, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    public void Dispose()
    {
        _serverCts?.Cancel();
        try
        {
            if (HasOwnership)
            {
                _mutex.ReleaseMutex();
            }
        }
        catch (ApplicationException)
        {
        }

        _serverCts?.Dispose();
        _mutex.Dispose();
    }
}
