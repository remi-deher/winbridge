using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32.SafeHandles;

namespace WinBridge.App.Services.Terminal;

public sealed partial class PtyService : IDisposable
{
    #region P/Invoke Structures et Constants

    private const uint EXTENDED_STARTUPINFO_PRESENT = 0x00080000;
    private const uint PROC_THREAD_ATTRIBUTE_PSEUDOCONSOLE = 0x00020016;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct STARTUPINFOEX
    {
        public STARTUPINFO StartupInfo;
        public IntPtr lpAttributeList;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct STARTUPINFO
    {
        public int cb;
        public string lpReserved;
        public string lpDesktop;
        public string lpTitle;
        public int dwX;
        public int dwY;
        public int dwXSize;
        public int dwYSize;
        public int dwXCountChars;
        public int dwYCountChars;
        public int dwFillAttribute;
        public int dwFlags;
        public short wShowWindow;
        public short cbReserved2;
        public IntPtr lpReserved2;
        public IntPtr hStdInput;
        public IntPtr hStdOutput;
        public IntPtr hStdError;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PROCESS_INFORMATION
    {
        public IntPtr hProcess;
        public IntPtr hThread;
        public int dwProcessId;
        public int dwThreadId;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SECURITY_ATTRIBUTES
    {
        public int nLength;
        public IntPtr lpSecurityDescriptor;
        public int bInheritHandle;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct COORD
    {
        public short X;
        public short Y;
    }

    #endregion

    #region P/Invoke Declarations

    [LibraryImport("kernel32.dll", SetLastError = true)]
    private static partial int CreatePseudoConsole(
        COORD size,
        IntPtr hInput,
        IntPtr hOutput,
        uint dwFlags,
        out IntPtr phPC);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    private static partial int ClosePseudoConsole(IntPtr hPC);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CreatePipe(
        out IntPtr hReadPipe,
        out IntPtr hWritePipe,
        ref SECURITY_ATTRIBUTES lpPipeAttributes,
        uint nSize);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr hObject);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    private static partial int ResizePseudoConsole(IntPtr hPC, COORD size);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool InitializeProcThreadAttributeList(
        IntPtr lpAttributeList,
        int dwAttributeCount,
        int dwFlags,
        ref IntPtr lpSize);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool UpdateProcThreadAttribute(
        IntPtr lpAttributeList,
        uint dwFlags,
        IntPtr attribute,
        IntPtr lpValue,
        IntPtr cbSize,
        IntPtr lpPreviousValue,
        IntPtr lpReturnSize);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    private static partial void DeleteProcThreadAttributeList(IntPtr lpAttributeList);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool CreateProcess(
        string? lpApplicationName,
        string lpCommandLine,
        IntPtr lpProcessAttributes,
        IntPtr lpThreadAttributes,
        bool bInheritHandles,
        uint dwCreationFlags,
        IntPtr lpEnvironment,
        string? lpCurrentDirectory,
        ref STARTUPINFOEX lpStartupInfo,
        out PROCESS_INFORMATION lpProcessInformation);

    #endregion

    #region Events

    public event EventHandler<byte[]>? DataReceived;

    public event EventHandler? ProcessExited;

    #endregion

    #region Fields

    private IntPtr _hPC = IntPtr.Zero;
    private Process? _process;
    
    private FileStream? _consoleInputPipe;
    private FileStream? _consoleOutputPipe;
    private readonly CancellationTokenSource _readCancellationSource = new();
    private bool _disposed;

    #endregion

    #region Public Methods

    public PtyResult StartProcessInPty(string command, string arguments = "", short rows = 24, short cols = 80)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(PtyService));

        SECURITY_ATTRIBUTES sa = new()
        {
            nLength = Marshal.SizeOf<SECURITY_ATTRIBUTES>(),
            bInheritHandle = 1,
            lpSecurityDescriptor = IntPtr.Zero
        };

        if (!CreatePipe(out IntPtr inputReadSide, out IntPtr inputWriteSide, ref sa, 0))
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Échec de création du pipe d'entrée");

        if (!CreatePipe(out IntPtr outputReadSide, out IntPtr outputWriteSide, ref sa, 0))
        {
            CloseHandle(inputReadSide);
            CloseHandle(inputWriteSide);
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Échec de création du pipe de sortie");
        }

        COORD consoleSize = new() { X = cols, Y = rows };
        int hresult = CreatePseudoConsole(consoleSize, inputReadSide, outputWriteSide, 0, out _hPC);

        if (hresult != 0)
        {
            Debug.WriteLine($"[PtyService] CreatePseudoConsole FAILED: hresult=0x{hresult:X8}");
            CloseHandle(inputReadSide); CloseHandle(inputWriteSide);
            CloseHandle(outputReadSide); CloseHandle(outputWriteSide);
            throw new Win32Exception(hresult, "Échec de création du PseudoConsole");
        }

        Debug.WriteLine($"[PtyService] CreatePseudoConsole OK: _hPC=0x{_hPC:X}");
        Debug.WriteLine($"[PtyService] Handles: inRead=0x{inputReadSide:X}, inWrite=0x{inputWriteSide:X}, outRead=0x{outputReadSide:X}, outWrite=0x{outputWriteSide:X}");

        STARTUPINFOEX startupInfo = new();
        startupInfo.StartupInfo.cb = Marshal.SizeOf<STARTUPINFOEX>();

        IntPtr lpSize = IntPtr.Zero;
        InitializeProcThreadAttributeList(IntPtr.Zero, 1, 0, ref lpSize);

        startupInfo.lpAttributeList = Marshal.AllocHGlobal(lpSize);

        if (!InitializeProcThreadAttributeList(startupInfo.lpAttributeList, 1, 0, ref lpSize))
        {
            CleanupPseudoConsole();
            
            CloseHandle(inputWriteSide);
            CloseHandle(outputReadSide);
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Échec d'initialisation de la liste d'attributs");
        }

        IntPtr hPCPtr = Marshal.AllocHGlobal(IntPtr.Size);
        Marshal.WriteIntPtr(hPCPtr, _hPC);

        if (!UpdateProcThreadAttribute(
            startupInfo.lpAttributeList,
            0,
            (IntPtr)PROC_THREAD_ATTRIBUTE_PSEUDOCONSOLE,
            _hPC,  
            (IntPtr)IntPtr.Size,
            IntPtr.Zero,
            IntPtr.Zero))
        {
            Marshal.FreeHGlobal(hPCPtr);
            DeleteProcThreadAttributeList(startupInfo.lpAttributeList);
            Marshal.FreeHGlobal(startupInfo.lpAttributeList);
            CleanupPseudoConsole();
            CloseHandle(inputWriteSide);
            CloseHandle(outputReadSide);
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Échec de mise à jour des attributs du processus");
        }

        Debug.WriteLine($"[PtyService] UpdateProcThreadAttribute OK, _hPC=0x{_hPC:X}");

        string commandLine = string.IsNullOrEmpty(arguments) ? command : $"{command} {arguments}";

        bool success = CreateProcess(
            null!,
            commandLine,
            IntPtr.Zero,
            IntPtr.Zero,
            false,  
            EXTENDED_STARTUPINFO_PRESENT,
            IntPtr.Zero,
            null!,
            ref startupInfo,
            out PROCESS_INFORMATION processInfo);

        Marshal.FreeHGlobal(hPCPtr);
        DeleteProcThreadAttributeList(startupInfo.lpAttributeList);
        Marshal.FreeHGlobal(startupInfo.lpAttributeList);

        if (!success)
        {
            int error = Marshal.GetLastWin32Error();
            Debug.WriteLine($"[PtyService] CreateProcess FAILED: error=0x{error:X8}");
            CleanupPseudoConsole();
            CloseHandle(inputWriteSide);
            CloseHandle(outputReadSide);
            throw new Win32Exception(error, "Échec de création du processus");
        }

        Debug.WriteLine($"[PtyService] CreateProcess OK: PID={processInfo.dwProcessId}");

        CloseHandle(inputReadSide);
        CloseHandle(outputWriteSide);
        Debug.WriteLine("[PtyService] Handles PTY fermés après CreateProcess");

        var safeInputWrite = new SafeFileHandle(inputWriteSide, true);
        var safeOutputRead = new SafeFileHandle(outputReadSide, true);

        CloseHandle(processInfo.hProcess);
        CloseHandle(processInfo.hThread);

        try
        {
            _process = Process.GetProcessById(processInfo.dwProcessId);
            _process.EnableRaisingEvents = true;
            _process.Exited += OnProcessExited;
        }
        catch {  }

        _consoleInputPipe = new FileStream(safeInputWrite, FileAccess.Write, bufferSize: 4096, isAsync: false);
        _consoleOutputPipe = new FileStream(safeOutputRead, FileAccess.Read, bufferSize: 4096, isAsync: false);

        return new PtyResult(_consoleInputPipe, _consoleOutputPipe, _process!);
    }

    public void Resize(short cols, short rows)
    {
        if (_disposed || _hPC == IntPtr.Zero)
            return;

        COORD newSize = new() { X = cols, Y = rows };
        int result = ResizePseudoConsole(_hPC, newSize);

        if (result == 0)
            Debug.WriteLine($"[PtyService] Redimensionné à {cols}x{rows}");
        else
            Debug.WriteLine($"[PtyService] Échec resize: 0x{result:X8}");
    }

    public async Task WriteAsync(byte[] data)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(PtyService));

        if (_consoleInputPipe == null)
            throw new InvalidOperationException("Le processus PTY n'est pas démarré");

        await _consoleInputPipe.WriteAsync(data);
        await _consoleInputPipe.FlushAsync();
    }

    #endregion

    #region Private Methods

    private async Task ReadOutputAsync(CancellationToken cancellationToken)
    {
        if (_consoleOutputPipe == null)
            return;

        byte[] buffer = new byte[4096];

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                int bytesRead = await _consoleOutputPipe.ReadAsync(buffer, 0, buffer.Length, cancellationToken);

                if (bytesRead == 0)
                    break; 

                byte[] data = new byte[bytesRead];
                Array.Copy(buffer, data, bytesRead);

                DataReceived?.Invoke(this, data);
            }
        }
        catch (OperationCanceledException)
        {
            
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[PtyService] Erreur de lecture: {ex.Message}");
        }
    }

    private void OnProcessExited(object? sender, EventArgs e)
    {
        int exitCode = 0;
        try { exitCode = _process?.ExitCode ?? -1; } catch { }
        Debug.WriteLine($"[PtyService] Process exited with code: 0x{exitCode:X8} ({exitCode})");
        ProcessExited?.Invoke(this, EventArgs.Empty);
    }

    private void CleanupPseudoConsole()
    {
        if (_hPC != IntPtr.Zero)
        {
            ClosePseudoConsole(_hPC);
            _hPC = IntPtr.Zero;
        }
    }

    #endregion

    #region IDisposable

    public void Dispose()
    {
        if (_disposed)
            return;

        _readCancellationSource?.Cancel();
        _readCancellationSource?.Dispose();

        _consoleInputPipe?.Dispose();
        _consoleOutputPipe?.Dispose();

        if (_process != null && !_process.HasExited)
        {
            _process.Kill();
        }
        _process?.Dispose();

        CleanupPseudoConsole();

        _disposed = true;
        GC.SuppressFinalize(this);
    }

    #endregion
}

public sealed class PtyResult
{

    public FileStream Input { get; }

    public FileStream Output { get; }

    public Process Process { get; }

    internal PtyResult(FileStream input, FileStream output, Process process)
    {
        Input = input;
        Output = output;
        Process = process;
    }
}
