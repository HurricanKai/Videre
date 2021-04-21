using Silk.NET.Maths;
using Silk.NET.Vulkan;

namespace Videre
{
    public sealed record RenderContext(
        ImageView TargetImageView,
        Vector2D<uint> TargetSize,
        int FrameIndex);
}
