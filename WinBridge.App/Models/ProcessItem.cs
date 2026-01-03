using CommunityToolkit.Mvvm.ComponentModel;
using WinBridge.App.Services.Files;

namespace WinBridge.App.Models;

public partial class ProcessItem : ObservableObject
{
    [ObservableProperty]
    public partial string Pid { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string User { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Command { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Cpu { get; set; } = "0%";

    [ObservableProperty]
    public partial string Mem { get; set; } = "0%";

    [ObservableProperty]
    public partial string Status { get; set; } = "Unknown";

    public double CpuValue => double.TryParse(Cpu, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : 0.0;
    public double MemValue => double.TryParse(Mem, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : 0.0;
    public int PidValue => int.TryParse(Pid, out var v) ? v : 0;

    public void UpdateFrom(SftpService.ProcessInfo info)
    {
        Pid = info.Pid;
        User = info.User;
        Command = info.Command;
        Cpu = info.Cpu;
        Mem = info.Mem;
        Status = info.Status;
    }

    public static ProcessItem FromInfo(SftpService.ProcessInfo info)
    {
        return new ProcessItem
        {
            Pid = info.Pid,
            User = info.User,
            Command = info.Command,
            Cpu = info.Cpu,
            Mem = info.Mem,
            Status = info.Status
        };
    }
}
