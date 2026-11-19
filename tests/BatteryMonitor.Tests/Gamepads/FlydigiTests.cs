using BatteryMonitor.Core.Protocols.Gamepads;
using BatteryMonitor.Tests.Headsets;

namespace BatteryMonitor.Tests.Gamepads;

/// <summary>
/// Flydigi V2 controllers (facts from SDL's HIDAPI Flydigi driver): the "get info" request
/// 5A A5 01 02 00 (report id 03, or unnumbered on Vader and Apex 6) is answered with an input
/// report starting 5A A5 01; byte 11 holds the power state (high nibble) and level (low nibble).
/// </summary>
public class FlydigiTests
{
    private static readonly byte[] Numbered = [0x03, 0x5A, 0xA5, 0x01, 0x02, 0x00];
    private static readonly byte[] Unnumbered = [0x00, 0x5A, 0xA5, 0x01, 0x02, 0x00];

    private static byte[] Info(byte battery, byte? reportId = null)
    {
        var r = ScriptedLink.Reply(32, (0, 0x5A), (1, 0xA5), (2, 0x01), (11, battery));
        return reportId is byte id ? [id, .. r] : r;
    }

    [Fact]
    public void Apex_sends_a_numbered_request_and_counts_in_fifths()
    {
        var link = new ScriptedLink().On(Numbered, Info(0x04, reportId: 0x03));

        Assert.Equal(new GamepadStatus(80, false), Flydigi.Query(link, Flydigi.Apex));
        Assert.Equal([Convert.ToHexString(Numbered)], link.Sent);
    }

    [Theory]
    [InlineData((ushort)0x2401, (byte)0x13, 60, true)]   // Vader: charging, level 3 -> 60 %
    [InlineData((ushort)0x2502, (byte)0x07, 70, false)]  // Apex 6: level in tenths
    [InlineData((ushort)0x2502, (byte)0x25, 100, false)] // charged
    public void Vader_and_Apex_6_use_unnumbered_requests(ushort productId, byte battery, int percent, bool charging)
    {
        var link = new ScriptedLink().On(Unnumbered, Info(battery));

        Assert.Equal(new GamepadStatus(percent, charging), Flydigi.Query(link, productId));
    }

    [Fact]
    public void Unrelated_input_reports_are_skipped()
    {
        var other = ScriptedLink.Reply(32, (0, 0x5A), (1, 0xA5), (2, 0xEF)); // another command
        var link = new ScriptedLink().On(Numbered, other, Info(0x02, reportId: 0x03));

        Assert.Equal(new GamepadStatus(40, false), Flydigi.Query(link, Flydigi.Apex));
    }

    [Fact]
    public void Short_reply_unknown_state_or_no_reply_gives_nothing()
    {
        Assert.Null(Flydigi.Query(new ScriptedLink().On(Numbered, Info(0x02, 0x03)[..20]), Flydigi.Apex));
        Assert.Null(Flydigi.Query(new ScriptedLink().On(Numbered, Info(0x34, 0x03)), Flydigi.Apex));
        Assert.Null(Flydigi.Query(new ScriptedLink(), Flydigi.Apex));
    }

    [Fact]
    public void Level_never_exceeds_100()
    {
        var link = new ScriptedLink().On(Numbered, Info(0x0F, 0x03)); // level 15 x 20

        Assert.Equal(new GamepadStatus(100, false), Flydigi.Query(link, Flydigi.Apex));
    }

    [Theory]
    [InlineData((ushort)0x2501, "Flydigi Apex")]
    [InlineData((ushort)0x2502, "Flydigi Apex 6")]
    [InlineData((ushort)0x2401, "Flydigi Vader")]
    public void Names(ushort productId, string name)
    {
        Assert.Equal(name, Flydigi.NameFor(productId));
    }
}
