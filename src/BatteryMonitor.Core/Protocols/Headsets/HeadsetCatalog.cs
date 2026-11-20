namespace BatteryMonitor.Core.Protocols.Headsets;

/// <summary>
/// Every supported wireless headset family. Not included: Razer BlackShark V2 Pro (PlayStation),
/// whose battery can only be read after switching the dongle to another mode, which breaks the
/// read-only device rule.
/// </summary>
public static class HeadsetCatalog
{
    public static IReadOnlyList<HeadsetSpec> All { get; } =
        [.. SteelSeriesHeadsets.Specs, .. HyperXHeadsets.Specs, .. CorsairHeadsets.Specs, .. SonyInzoneHeadsets.Specs,
         .. JabraHeadsets.Specs, .. LogitechHeadsets.Specs, .. MchoseHeadsets.Specs];

    public static IReadOnlySet<ushort> VendorIds { get; } = All.Select(s => s.VendorId).ToHashSet();

    public static HeadsetSpec? Find(ushort vendorId, ushort productId) =>
        All.FirstOrDefault(s => s.VendorId == vendorId && s.ProductIds.Contains(productId));
}
