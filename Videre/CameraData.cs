using System.Numerics;
using System.Runtime.InteropServices;

namespace Videre
{
    [StructLayout(LayoutKind.Sequential)]
    public struct CameraData
    {
        public Vector2 Translation;
        public ulong Head;
    }
}
