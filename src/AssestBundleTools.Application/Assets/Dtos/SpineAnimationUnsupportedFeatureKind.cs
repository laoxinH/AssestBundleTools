namespace AssestBundleTools.Application.Assets.Dtos;

/// <summary>
/// 表示实验性 Spine 动画预览当前无法完整解释的 skeleton 特性。
/// </summary>
public enum SpineAnimationUnsupportedFeatureKind
{
    BinarySkeleton = 0,
    NonDefaultSkin = 1,
    MeshAttachment = 2,
    WeightedVertices = 3,
    LinkedMeshAttachment = 4,
    BoundingBoxAttachment = 5,
    PathAttachment = 6,
    ClippingAttachment = 7,
    PointAttachment = 8,
    UnknownAttachment = 9,
    SlotColorTimeline = 10,
    SlotTwoColorTimeline = 11,
    BoneShearTimeline = 12,
    DeformTimeline = 13,
    DrawOrderTimeline = 14,
    EventTimeline = 15,
    IkConstraint = 16,
    TransformConstraint = 17,
    PathConstraint = 18,
    PhysicsConstraint = 19,
    BezierCurve = 20
}
