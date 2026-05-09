using Runtime = AssestBundleTools.Infrastructure.ThirdParty.Spine.V38;

namespace AssestBundleTools.Infrastructure.Bundles.Previewing.Spine;

internal sealed partial class V38SpineAnimationRuntimeAdapter
{
    private static SpineRuntimeTint CreateTint(Runtime.Slot slot, Runtime.RegionAttachment attachment) =>
        new SpineRuntimeTint(
            slot.Skeleton.R * slot.R * attachment.R,
            slot.Skeleton.G * slot.G * attachment.G,
            slot.Skeleton.B * slot.B * attachment.B,
            slot.Skeleton.A * slot.A * attachment.A).Clamp();

    private static SpineRuntimeTint CreateTint(Runtime.Slot slot, Runtime.MeshAttachment attachment) =>
        new SpineRuntimeTint(
            slot.Skeleton.R * slot.R * attachment.R,
            slot.Skeleton.G * slot.G * attachment.G,
            slot.Skeleton.B * slot.B * attachment.B,
            slot.Skeleton.A * slot.A * attachment.A).Clamp();

    private readonly record struct SpineRuntimeTint(
        double R,
        double G,
        double B,
        double A)
    {
        public SpineRuntimeTint Clamp() =>
            new(
                ClampTint(R),
                ClampTint(G),
                ClampTint(B),
                ClampTint(A));
    }

    private static double ClampTint(double value) =>
        Math.Clamp(value, 0d, 1d);
}
