using System;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WinBridge.Core.Grpc;

namespace WinBridge.App.Services.Terminal;

public sealed partial class TerminalSessionManager(BridgeService bridgeService) : IDisposable
{
    #region Fields

    private readonly BridgeService _bridgeService = bridgeService ?? throw new ArgumentNullException(nameof(bridgeService));
    public PtyService? _ptyService;  
    private string? _sessionId;
    private bool _disposed;

    #endregion

    #region Events

    public event EventHandler<TerminalDataEventArgs>? DataReceived;

    public event EventHandler? SessionClosed;

    #endregion

    #region Properties

    public string? SessionId => _sessionId;

    public bool IsActive => _ptyService != null && _sessionId != null;

    #endregion

    #region Properties

    public PtyResult? PtyResult { get; private set; }

    #endregion
    #region Constructor

    #endregion

    #region Public Methods

    public string StartLocalSession(string command, string arguments = "", short rows = 24, short cols = 80)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(TerminalSessionManager));

        if (IsActive)
            throw new InvalidOperationException("Une session est dÃ©jÃ  active. Fermez-la avant d'en dÃ©marrer une nouvelle.");

        _sessionId = Guid.NewGuid().ToString();
        _ptyService = new PtyService();
        _ptyService.DataReceived += OnPtyDataReceived;
        _ptyService.ProcessExited += OnPtyProcessExited;

        try
        {
            PtyResult = _ptyService.StartProcessInPty(command, arguments, rows, cols);
            Debug.WriteLine($"[TerminalSession] Session {_sessionId} dÃ©marrÃ©e: {command} {arguments}");
            return _sessionId;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[TerminalSession] Erreur de dÃ©marrage: {ex.Message}");
            _ptyService.Dispose();
            _ptyService = null;
            _sessionId = null;
            PtyResult = null;
            throw;
        }
    }

    public void Resize(int cols, int rows)
    {
        _ptyService?.Resize((short)cols, (short)rows);
    }

    public async Task SendInputAsync(byte[] data)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(TerminalSessionManager));

        if (_ptyService == null)
            throw new InvalidOperationException("Aucune session active");

        await _ptyService.WriteAsync(data);

        await NotifyModulesAsync(DataType.Input, data);
    }

    public async Task SendInputAsync(string text)
    {
        byte[] data = Encoding.UTF8.GetBytes(text);
        await SendInputAsync(data);
    }

    public async Task ResizeAsync(short rows, short cols)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(TerminalSessionManager));

        if (_ptyService == null)
            throw new InvalidOperationException("Aucune session active");

        _ptyService.Resize(rows, cols);

        byte[] resizeData = [.. BitConverter.GetBytes(rows)
, .. BitConverter.GetBytes(cols)];

        await NotifyModulesAsync(DataType.Resize, resizeData);
    }

    public void CloseSession()
    {
        if (_ptyService != null)
        {
            _ptyService.DataReceived -= OnPtyDataReceived;
            _ptyService.ProcessExited -= OnPtyProcessExited;
            _ptyService.Dispose();
            _ptyService = null;
        }

        _sessionId = null;
        SessionClosed?.Invoke(this, EventArgs.Empty);
    }

    #endregion

    #region Private Methods

    private async void OnPtyDataReceived(object? sender, byte[] data)
    {
        if (_sessionId == null)
            return;

        DataReceived?.Invoke(this, new TerminalDataEventArgs(_sessionId, data, DataType.Output));

        await NotifyModulesAsync(DataType.Output, data);
    }

    private void OnPtyProcessExited(object? sender, EventArgs e)
    {
        Debug.WriteLine($"[TerminalSession] Session {_sessionId} terminÃ©e");
        CloseSession();
    }

    private async Task NotifyModulesAsync(DataType dataType, byte[] payload)
    {
        if (_sessionId == null)
            return;

        Debug.WriteLine($"[TerminalSession] {dataType}: {payload.Length} octets pour session {_sessionId}");

        await Task.CompletedTask;
    }

    #endregion

    #region IDisposable

    public void Dispose()
    {
        if (_disposed)
            return;

        CloseSession();
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    #endregion
}

public sealed class TerminalDataEventArgs(string sessionId, byte[] data, DataType dataType) : EventArgs
{

    public string SessionId { get; } = sessionId;

    public byte[] Data { get; } = data;

    public DataType DataType { get; } = dataType;
}

