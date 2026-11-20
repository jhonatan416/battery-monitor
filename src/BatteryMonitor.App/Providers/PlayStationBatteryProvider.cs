using BatteryMonitor.App.Hid;
using BatteryMonitor.Core.Domain;
using BatteryMonitor.Core.Ports;
using BatteryMonitor.Core.Protocols;

namespace BatteryMonitor.App.Providers;

/// <summary>
/// DualShock 4 and DualSense controllers over USB or Bluetooth. Reads the input reports the
/// controller already sends; the only request is a read-only GET of feature report 5, which makes
/// a Bluetooth DualSense switch from its short report to the full one.
/// </summary>
public sealed class PlayStationBatteryProvider : IBatteryProvider
{
    private static readonly TimeSpan ReadTimeout = TimeSpan.FromMilliseconds(150);
    private const int MaxReports = 6;
    private const byte DualSenseCalibrationReport = 0x05;
    private const int DualSenseCalibrationLength = 41;

    public string Name => "PlayStation";

    public IReadOnlyList<DeviceBattery> Poll()
    {
        var result = new List<DeviceBattery>();
        foreach (var coll in HidCollection.Enumerate(PlayStationProtocol.SonyVendorId))
        {
            if (PlayStationProtocol.ModelFor(coll.ProductId) is not { } model) continue;
            if (Read(coll, model) is not { } reading) continue;
            bool bluetooth = HidBattery.IsBluetoothPath(coll.Path);
            result.Add(new DeviceBattery(
                $"ps:{coll.ProductId:X4}:{coll.Path.GetHashCode():X8}",
                model == PlayStationModel.DualSense ? "DualSense" : "DualShock 4",
                DeviceKind.Gamepad,
                bluetooth ? "Bluetooth" : "USB",
                reading.Percent,
                reading.Charging,
                Usb: bluetooth ? null : new UsbProduct(coll.VendorId, coll.ProductId)));
        }
        return result;
    }

    private static PlayStationReading? Read(HidCollection coll, PlayStationModel model)
    {
        using var stream = HidStream.Open(coll);
        if (stream is null) return null;
        bool askedFullMode = false;
        for (int i = 0; i < MaxReports; i++)
        {
            if (stream.Read(ReadTimeout) is not { } report) return null; // controller off / no reports
            if (PlayStationProtocol.Parse(model, report) is { } reading) return reading;
            if (model == PlayStationModel.DualSense && !askedFullMode)
            {
                askedFullMode = true;
                var calibration = new byte[DualSenseCalibrationLength];
                calibration[0] = DualSenseCalibrationReport;
                stream.GetFeature(calibration); // read-only GET; switches Bluetooth to full reports
            }
        }
        return null;
    }
}
