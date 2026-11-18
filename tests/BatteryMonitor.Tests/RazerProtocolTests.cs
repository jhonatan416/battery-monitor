using BatteryMonitor.Core.Protocols;

namespace BatteryMonitor.Tests;

public class RazerProtocolTests
{
    private const byte Tid = 0x1F;

    [Fact]
    public void Battery_request_has_report_id_header_command_and_checksum()
    {
        byte[] req = RazerProtocol.BuildRequest(Tid, RazerProtocol.BatteryLevel);

        Assert.Equal(RazerProtocol.ReportLength, req.Length); // 1 report id byte + 90 byte report
        Assert.Equal(0x00, req[0]);                            // report id
        Assert.Equal(Tid, req[2]);                             // transaction id
        Assert.Equal(0x02, req[6]);                            // data size
        Assert.Equal(0x07, req[7]);                            // command class
        Assert.Equal(0x80, req[8]);                            // command id
        Assert.Equal(0x02 ^ 0x07 ^ 0x80, req[89]);             // xor of report bytes 2..87
    }

    [Fact]
    public void Ok_reply_returns_the_arguments()
    {
        byte[] reply = Reply(status: 0x02, Tid, RazerProtocol.BatteryLevel, arg1: 0xC8);

        RazerReply r = RazerProtocol.ParseReply(reply, Tid, RazerProtocol.BatteryLevel);

        Assert.Equal(RazerStatus.Ok, r.Status);
        Assert.Equal(0xC8, r.Arguments[1]);
    }

    [Theory]
    [InlineData(0x01, RazerStatus.Busy)]
    [InlineData(0x03, RazerStatus.Unsupported)]
    [InlineData(0x04, RazerStatus.Offline)]
    [InlineData(0x05, RazerStatus.Unsupported)]
    public void Non_ok_status_codes_are_mapped(byte status, RazerStatus expected)
    {
        byte[] reply = Reply(status, Tid, RazerProtocol.BatteryLevel, arg1: 0);

        Assert.Equal(expected, RazerProtocol.ParseReply(reply, Tid, RazerProtocol.BatteryLevel).Status);
    }

    [Fact]
    public void Reply_for_another_request_is_a_mismatch()
    {
        // Synapse shares the feature-report mailbox; its replies must be ignored.
        byte[] reply = Reply(0x02, 0x3F, RazerProtocol.BatteryLevel, arg1: 0xC8);

        Assert.Equal(RazerStatus.Mismatch, RazerProtocol.ParseReply(reply, Tid, RazerProtocol.BatteryLevel).Status);
    }

    [Fact]
    public void Reply_with_bad_checksum_is_a_mismatch()
    {
        byte[] reply = Reply(0x02, Tid, RazerProtocol.BatteryLevel, arg1: 0xC8);
        reply[89] ^= 0xFF;

        Assert.Equal(RazerStatus.Mismatch, RazerProtocol.ParseReply(reply, Tid, RazerProtocol.BatteryLevel).Status);
    }

    [Fact]
    public void Short_reply_is_a_mismatch()
    {
        Assert.Equal(RazerStatus.Mismatch, RazerProtocol.ParseReply(new byte[10], Tid, RazerProtocol.BatteryLevel).Status);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(128, 50)]
    [InlineData(200, 78)]
    [InlineData(255, 100)]
    public void Raw_level_scales_to_percent(byte raw, int percent)
    {
        Assert.Equal(percent, RazerProtocol.ToPercent(raw));
    }

    private static byte[] Reply(byte status, byte tid, RazerCommand cmd, byte arg1)
    {
        byte[] rep = RazerProtocol.BuildRequest(tid, cmd);
        rep[1] = status;
        rep[10] = arg1;                 // arguments start at report byte 8 (+1 for report id)
        byte crc = 0;
        for (int i = 3; i < 89; i++) crc ^= rep[i];
        rep[89] = crc;
        return rep;
    }
}
