#if TOOLS
using System.Collections.Concurrent;
using System.IO.Pipes;
using System.Text;

namespace Tactics.Godot.Adapter.Editor;

internal sealed class NamedPipeAuthoringServer : IDisposable
{
    private readonly string _pipeName;
    private readonly CancellationTokenSource _stop = new();
    private readonly ConcurrentQueue<PendingRequest> _requests = new();
    private readonly object _gate = new();
    private readonly Task _serverTask;
    private NamedPipeServerStream? _activePipe;
    private PendingRequest? _activeRequest;
    private int _disposed;

    public NamedPipeAuthoringServer(string pipeName)
    {
        _pipeName = pipeName;
        _serverTask = Task.Run(RunAsync);
    }

    public string State => _serverTask.IsCompleted ? "stopped" : _activePipe?.IsConnected == true ? "connected" : "waiting";
    public int PendingRequestCount => _requests.Count + (_activeRequest is null ? 0 : 1);

    public bool TryReadRequest(out string request)
    {
        if (_activeRequest is not null || !_requests.TryDequeue(out PendingRequest? pending))
        {
            request = string.Empty;
            return false;
        }
        _activeRequest = pending;
        request = pending.Payload;
        return true;
    }

    public void WriteResponse(string response)
    {
        PendingRequest pending = _activeRequest ?? throw new InvalidOperationException("No active authoring request.");
        _activeRequest = null;
        pending.Response.TrySetResult(response);
    }

    public void AbortConnection()
    {
        PendingRequest? pending = _activeRequest;
        _activeRequest = null;
        pending?.Response.TrySetException(new IOException("Authoring request was aborted."));
        lock (_gate) _activePipe?.Dispose();
    }

    public bool Shutdown(TimeSpan timeout)
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0)
        {
            _stop.Cancel();
            lock (_gate) _activePipe?.Dispose();
            var stopped = new IOException("Authoring bridge is reloading.");
            _activeRequest?.Response.TrySetException(stopped);
            while (_requests.TryDequeue(out PendingRequest? request)) request.Response.TrySetException(stopped);
        }
        try { return _serverTask.Wait(timeout); }
        catch (AggregateException error) when (error.InnerExceptions.All(value => value is OperationCanceledException or ObjectDisposedException or IOException)) { return true; }
    }

    public void Dispose()
    {
        if (Shutdown(TimeSpan.FromSeconds(2))) _stop.Dispose();
    }

    private async Task RunAsync()
    {
        while (!_stop.IsCancellationRequested)
        {
            var pipe = new NamedPipeServerStream(_pipeName, PipeDirection.InOut, 1,
                PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
            lock (_gate) _activePipe = pipe;
            try
            {
                await pipe.WaitForConnectionAsync(_stop.Token).ConfigureAwait(false);
                using var reader = new StreamReader(pipe, Encoding.UTF8, false, 4096, leaveOpen: true);
                using var writer = new StreamWriter(pipe, new UTF8Encoding(false), 4096, leaveOpen: true) { AutoFlush = true };
                string? payload = await reader.ReadLineAsync(_stop.Token).ConfigureAwait(false);
                if (payload is null) continue;
                var pending = new PendingRequest(payload);
                _requests.Enqueue(pending);
                string response = await pending.Response.Task.WaitAsync(_stop.Token).ConfigureAwait(false);
                await writer.WriteLineAsync(response.AsMemory(), _stop.Token).ConfigureAwait(false);
            }
            catch (Exception error) when (error is OperationCanceledException or ObjectDisposedException or IOException)
            {
                if (!_stop.IsCancellationRequested) continue;
                break;
            }
            finally
            {
                lock (_gate)
                {
                    if (ReferenceEquals(_activePipe, pipe)) _activePipe = null;
                }
                pipe.Dispose();
            }
        }
    }

    private sealed class PendingRequest(string payload)
    {
        public string Payload { get; } = payload;
        public TaskCompletionSource<string> Response { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }
}
#endif
