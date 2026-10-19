using System.IO;
using System.IO.Pipes;
using System.Text;

namespace AudiobookPlayer.Platform;

/// <summary>
/// Keeps one copy of the app running. Opening a book from File Explorer while the app is already open
/// hands the file to the open window instead of starting a second copy.
/// </summary>
internal sealed class SingleInstance : IDisposable
{
    private readonly Mutex _mutex;
    private readonly string _pipeName;
    private readonly CancellationTokenSource _stop = new();

    public bool IsFirst { get; }

    public SingleInstance()
    {
        var id = AppInfo.Name + "-" + string.Concat(Environment.UserName.Where(char.IsLetterOrDigit));
        _pipeName = id + "-open";
        _mutex = new Mutex(true, @"Local\" + id, out var createdNew);
        IsFirst = createdNew;
    }

    /// <summary>Sends files to the copy that is already running. Returns false if it couldn't be reached.</summary>
    public bool SendToFirst(string[] args)
    {
        try
        {
            using var client = new NamedPipeClientStream(".", _pipeName, PipeDirection.Out);
            client.Connect(3000);
            using var writer = new StreamWriter(client, Encoding.UTF8);
            // An empty message just brings the window to the front.
            writer.Write(string.Join('\n', args));
            return true;
        }
        catch (Exception e) when (e is IOException or TimeoutException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    /// <summary>Listens for files sent by later copies. The callback runs on a background thread.</summary>
    public void Listen(Action<string[]> received)
    {
        Task.Run(async () =>
        {
            while (!_stop.IsCancellationRequested)
            {
                try
                {
                    using var server = new NamedPipeServerStream(_pipeName, PipeDirection.In, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
                    await server.WaitForConnectionAsync(_stop.Token);
                    using var reader = new StreamReader(server, Encoding.UTF8);
                    var text = await reader.ReadToEndAsync(_stop.Token);
                    received(text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
                }
                catch (OperationCanceledException) { return; }
                catch (IOException) { await Task.Delay(200); }
            }
        });
    }

    public void Dispose()
    {
        _stop.Cancel();
        if (IsFirst) _mutex.ReleaseMutex();
        _mutex.Dispose();
    }
}
