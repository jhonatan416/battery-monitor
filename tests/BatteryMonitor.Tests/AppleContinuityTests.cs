using BatteryMonitor.Core.Protocols;

namespace BatteryMonitor.Tests;

/// <summary>
/// AirPods / Beats "proximity pairing" advertisement (Apple manufacturer data, company 0x004C),
/// layout as documented by OpenPods, AirStatus and the furiousMAC continuity dissector. Data
/// starts at the type byte 0x07, length 0x19 (27 bytes in all). Byte 3-4 model, byte 5 status
/// (0x20 clear = flipped), byte 6 batteries (low nibble left, high right), byte 7 charging bits
/// (high nibble: 1 left, 2 right, 4 case) and case battery (low nibble). Nibble 0..10, 15 = absent.
/// </summary>
public class AppleContinuityTests
{
    private static byte[] Advert(byte model1, byte model2, byte status, byte batteries, byte charging)
    {
        var data = new byte[27];
        data[0] = 0x07;
        data[1] = 0x19;
        data[2] = 0x01;
        data[3] = model1;
        data[4] = model2;
        data[5] = status;
        data[6] = batteries;
        data[7] = charging;
        return data;
    }

    [Fact]
    public void AirPods_Pro_takes_the_lower_earbud()
    {
        // left 8 (low nibble), right 6 (high nibble), not flipped (0x20 set), case 5, nothing charging.
        var reading = AppleContinuity.Parse(Advert(0x0E, 0x20, 0x2B, 0x68, 0x05));

        Assert.Equal(new AppleAudioReading("AirPods Pro", 65, false), reading);
    }

    [Fact]
    public void Flipped_status_swaps_left_and_right_charging_bits()
    {
        // flipped (0x20 clear): left is the high nibble (2), right the low nibble (9); bit 1 = left charging.
        var reading = AppleContinuity.Parse(Advert(0x0F, 0x20, 0x01, 0x29, 0x25));

        Assert.Equal(new AppleAudioReading("AirPods (2nd generation)", 25, true), reading);
    }

    [Fact]
    public void Earbud_out_of_the_case_reading_ignores_the_case_charging()
    {
        var reading = AppleContinuity.Parse(Advert(0x14, 0x20, 0x22, 0x77, 0x45)); // only the case charges

        Assert.Equal(new AppleAudioReading("AirPods Pro (2nd generation)", 75, false), reading);
    }

    [Fact]
    public void Missing_earbud_uses_the_other_one()
    {
        var reading = AppleContinuity.Parse(Advert(0x13, 0x20, 0x22, 0xF4, 0x0F)); // right 15 = absent

        Assert.Equal(new AppleAudioReading("AirPods (3rd generation)", 45, false), reading);
    }

    [Fact]
    public void Full_is_100_percent()
    {
        Assert.Equal(100, AppleContinuity.Parse(Advert(0x0E, 0x20, 0x22, 0xAA, 0x0A))!.Value.Percent);
    }

    [Theory]
    [InlineData((byte)0x0A, (byte)0x20, "AirPods Max")]
    [InlineData((byte)0x05, (byte)0x20, "BeatsX")]
    [InlineData((byte)0x10, (byte)0x20, "Beats Flex")]
    [InlineData((byte)0x06, (byte)0x20, "Beats Solo3")]
    [InlineData((byte)0x09, (byte)0x20, "Beats Studio3")]
    [InlineData((byte)0x03, (byte)0x20, "Powerbeats3")]
    public void Single_battery_models_use_the_low_nibble(byte model1, byte model2, string name)
    {
        var reading = AppleContinuity.Parse(Advert(model1, model2, 0x22, 0xF3, 0x10));

        Assert.Equal(new AppleAudioReading(name, 35, true), reading);
    }

    [Fact]
    public void Powerbeats_Pro_is_a_two_earbud_model()
    {
        Assert.Equal(new AppleAudioReading("Powerbeats Pro", 55, false),
            AppleContinuity.Parse(Advert(0x0B, 0x20, 0x22, 0x55, 0x0F)));
    }

    [Fact]
    public void Unknown_model_is_shown_as_AirPods()
    {
        Assert.Equal("AirPods", AppleContinuity.Parse(Advert(0x55, 0x20, 0x22, 0x33, 0x00))!.Value.Model);
    }

    [Theory]
    [InlineData(0x07, 0x18, 27)] // wrong length byte
    [InlineData(0x10, 0x19, 27)] // another continuity message (nearby info)
    [InlineData(0x07, 0x19, 26)] // too short
    public void Other_apple_adverts_are_ignored(int type, int length, int size)
    {
        var data = Advert(0x0E, 0x20, 0x22, 0x55, 0x00);
        data[0] = (byte)type;
        data[1] = (byte)length;

        Assert.Null(AppleContinuity.Parse(data[..size]));
    }

    [Fact]
    public void No_earbud_present_gives_nothing()
    {
        Assert.Null(AppleContinuity.Parse(Advert(0x0E, 0x20, 0x22, 0xFF, 0x0F)));
    }

    [Fact]
    public void Levels_above_10_other_than_absent_are_ignored()
    {
        Assert.Null(AppleContinuity.Parse(Advert(0x0E, 0x20, 0x22, 0xCC, 0x00)));
    }

    [Fact]
    public void Advertisements_are_used_only_while_owned_apple_audio_is_connected_without_a_windows_level()
    {
        // Nearby strangers' AirPods also advertise: show a reading only when this PC has AirPods/Beats connected.
        Assert.True(AppleContinuity.NeedsAdvertisement(["Xbox Wireless Controller", "AirPods Pro"], []));
        Assert.False(AppleContinuity.NeedsAdvertisement(["Xbox Wireless Controller"], []));
        Assert.False(AppleContinuity.NeedsAdvertisement([], []));
        // Windows already reports it (Bluetooth battery property): no duplicate.
        Assert.False(AppleContinuity.NeedsAdvertisement(["AirPods Pro"], ["AirPods Pro"]));
        Assert.True(AppleContinuity.NeedsAdvertisement(["AirPods Pro"], ["Xbox Wireless Controller"]));
    }

    [Theory]
    [InlineData("AirPods Pro", true)]
    [InlineData("Juan's AirPods", true)]
    [InlineData("Powerbeats Pro", true)]
    [InlineData("Beats Studio Buds", true)]
    [InlineData("Xbox Wireless Controller", false)]
    public void Paired_names_that_belong_to_apple_audio(string name, bool apple)
    {
        Assert.Equal(apple, AppleContinuity.IsAppleAudioName(name));
    }
}
