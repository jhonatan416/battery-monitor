using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace BatteryMonitor.App.Hid;

/// <summary>
/// A HID collection opened for input reports (what the device sends) and output reports
/// (requests we send). Reads time out instead of blocking the poll.
/// </summary>
public sealed class HidStream : IDisposable
{
    private const uint FILE_FLAG_OVERLAPPED = 0x40000000;
    private readonly FileStream _stream;

    public int InputLength { get; }
    public int OutputLength { get; }

    private HidStream(FileStream stream, int inputLength, int outputLength)
    {
        _stream = stream;
        InputLength = inputLength;
        OutputLength = outputLength;
    }

    /// <summary>Opens the collection, or returns null when it cannot be opened for reading.</summary>
    public static HidStream? Open(HidCollection coll)
    {
        var lengths = HidBatteryReader.ReportLengths(coll);
        if (lengths is not var (input, output) || input == 0) return null;
        SafeFileHandle h = CreateFileW(coll.Path, Native.GENERIC_READ | Native.GENERIC_WRITE, 3, IntPtr.Zero, 3,
            FILE_FLAG_OVERLAPPED, IntPtr.Zero);
        if (h.IsInvalid)
        {
            h.Dispose();
            return null; // e.g. mouse/keyboard collections that Windows keeps exclusive
        }
        return new HidStream(new FileStream(h, FileAccess.ReadWrite, 0, isAsync: true), input, output);
    }

    /// <summary>Next input report, or null if none arrives within the timeout.</summary>
    public byte[]? Read(TimeSpan timeout)
    {
        var buffer = new byte[InputLength];
        using var cts = new CancellationTokenSource(timeout);
        try
        {
            int n = _stream.ReadAsync(buffer, cts.Token).AsTask().GetAwaiter().GetResult();
            return n > 0 ? buffer : null;
        }
        catch (Exception ex) when (ex is OperationCanceledException or IOException)
        {
            return null;
        }
    }

    /// <summary>Sends one output report (padded to the device's output length). False if it failed.</summary>
    public bool Write(byte[] report)
    {
        if (OutputLength == 0 || report.Length > OutputLength) return false;
        var buffer = new byte[OutputLength];
        report.CopyTo(buffer, 0);
        try
        {
            _stream.Write(buffer, 0, buffer.Length);
            return true;
        }
        catch (IOException) { return false; }
    }

    /// <summary>Feature report through the same handle (read-only GET).</summary>
    public bool GetFeature(byte[] buffer) => Native.HidD_GetFeature(_stream.SafeFileHandle, buffer, buffer.Length);

    public void Dispose() => _stream.Dispose();

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern SafeFileHandle CreateFileW(string name, uint access, uint share, IntPtr sa, uint disposition, uint flags, IntPtr template);
}
