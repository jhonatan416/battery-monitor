using BatteryMonitor.Core.Protocols.Headsets;

namespace BatteryMonitor.Tests.Headsets;

/// <summary>
/// Logitech "Centurion" battery read of the PRO X 2 LIGHTSPEED (facts from the public HeadsetControl
/// protocol definition). Frame: 51 LEN FLAGS payload, LEN = payload length + 1. Direct request
/// payload: feature index, function | software id 1, params. Bridge request payload: bridge index,
/// 11 (send fragment), 12-bit size, then the sub-message 00 feature function|1 params. Bridge replies:
/// bridge index, 10 (message event, software id 0), size, 00, feature, function, data.
/// Steps: Root.getFeature(0003 bridge) on the dongle, Root.getFeature(0104 battery) on the headset
/// through the bridge, then battery function 0 -> [percent][?][state: 1 or 2 = charging].
/// </summary>
public class LogitechCenturionTests
{
    private const byte Bridge = 0x02, BatteryFeature = 0x07;

    private static byte[] Frame(params byte[] payload) => [0x51, (byte)(payload.Length + 1), 0x00, .. payload];

    private static byte[] Padded(byte[] frame)
    {
        var r = new byte[64];
        frame.CopyTo(r, 0);
        return r;
    }

    private static readonly byte[] FindBridge = Frame(0x00, 0x01, 0x00, 0x03);
    private static readonly byte[] FindBattery = Frame(Bridge, 0x11, 0x00, 0x05, 0x00, 0x00, 0x01, 0x01, 0x04);
    private static readonly byte[] ReadBattery = Frame(Bridge, 0x11, 0x00, 0x03, 0x00, BatteryFeature, 0x01);

    private static byte[] BridgeFound => Padded(Frame(0x00, 0x01, Bridge, 0x00, 0x00));
    private static byte[] Ack => Padded(Frame(Bridge, 0x11, 0x00));
    private static byte[] BridgeReply(byte feature, params byte[] data) =>
        Padded(Frame([Bridge, 0x10, 0x00, (byte)(data.Length + 3), 0x00, feature, 0x01, .. data]));

    private static readonly byte[] LegacyGet = [0x51, 0x08, 0x00, 0x03, 0x1A, 0x00, 0x03, 0x00, 0x04, 0x0A];

    private static HeadsetStatus? Query(ScriptedLink link) => HeadsetCatalog.Find(0x046D, 0x0AF7)!.Query(link, 0x0AF7);

    [Theory]
    [InlineData((byte)0x00, false)]
    [InlineData((byte)0x01, true)]
    [InlineData((byte)0x02, true)]
    public void Battery_through_the_bridge(byte state, bool charging)
    {
        var link = new ScriptedLink()
            .On(FindBridge, BridgeFound)
            .On(FindBattery, Ack, BridgeReply(0x00, BatteryFeature, 0x00, 0x00))
            .On(ReadBattery, Ack, BridgeReply(BatteryFeature, 85, 0x00, state));

        Assert.Equal(new HeadsetStatus(85, charging), Query(link));
        Assert.DoesNotContain(Convert.ToHexString(LegacyGet), link.Sent);
    }

    [Fact]
    public void Headset_off_event_gives_nothing_and_no_legacy_request()
    {
        var off = Padded(Frame(Bridge, 0x00, 0x00, 0x00)); // bridge connection event, connected = 0
        var link = new ScriptedLink().On(FindBridge, BridgeFound).On(FindBattery, off);

        Assert.Null(Query(link));
        Assert.DoesNotContain(Convert.ToHexString(LegacyGet), link.Sent);
    }

    [Fact]
    public void Headset_on_event_is_skipped()
    {
        var on = Padded(Frame(Bridge, 0x00, 0x00, 0x01));
        var link = new ScriptedLink()
            .On(FindBridge, BridgeFound)
            .On(FindBattery, on, BridgeReply(0x00, BatteryFeature, 0x00, 0x00))
            .On(ReadBattery, BridgeReply(BatteryFeature, 40, 0x00, 0x00));

        Assert.Equal(new HeadsetStatus(40, false), Query(link));
    }

    [Fact]
    public void Rejected_bridge_request_gives_nothing()
    {
        var rejected = Padded(Frame(0xFF, Bridge, 0x11, 0x00));
        var link = new ScriptedLink().On(FindBridge, BridgeFound).On(FindBattery, rejected);

        Assert.Null(Query(link));
    }

    [Fact]
    public void Headset_without_the_battery_feature_falls_back_to_the_older_request()
    {
        var link = new ScriptedLink()
            .On(FindBridge, BridgeFound)
            .On(FindBattery, BridgeReply(0x00, 0x00, 0x00, 0x00)) // index 0 = not supported
            .On(LegacyGet, ScriptedLink.Reply(64, (0, 0x51), (1, 0x0B), (8, 0x04), (10, 66)));

        Assert.Equal(new HeadsetStatus(66, false), Query(link));
    }

    [Fact]
    public void Dongle_that_does_not_answer_centurion_falls_back_to_the_older_request()
    {
        var link = new ScriptedLink().On(LegacyGet, ScriptedLink.Reply(64, (0, 0x51), (1, 0x0B), (8, 0x04), (10, 30), (12, 0x02)));

        Assert.Equal(new HeadsetStatus(30, true), Query(link));
    }

    [Fact]
    public void Reply_for_another_feature_is_skipped()
    {
        var link = new ScriptedLink()
            .On(FindBridge, BridgeFound)
            .On(FindBattery, BridgeReply(0x00, BatteryFeature, 0x00, 0x00))
            .On(ReadBattery, BridgeReply(0x09, 12, 0, 0), BridgeReply(BatteryFeature, 77, 0x00, 0x00));

        Assert.Equal(new HeadsetStatus(77, false), Query(link));
    }

    [Fact]
    public void Level_above_100_gives_nothing()
    {
        var link = new ScriptedLink()
            .On(FindBridge, BridgeFound)
            .On(FindBattery, BridgeReply(0x00, BatteryFeature, 0x00, 0x00))
            .On(ReadBattery, BridgeReply(BatteryFeature, 120, 0x00, 0x00));

        Assert.Null(Query(link));
    }
}
