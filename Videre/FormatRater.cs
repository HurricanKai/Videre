// This file is part of Silk.NET.
// 
// You may modify and distribute Silk.NET under the terms
// of the MIT license. See the LICENSE file for details.

using System;
using Silk.NET.Vulkan;

namespace Aliquip
{
    internal sealed class FormatRater
    {
        public static int Rate(Format format)
        {
            int R(int value) => value switch { < 8 => 100, > 32 => 100, _ => 200 } * 2 + value;
            int G(int value) => value switch { < 8 => 100, > 32 => 100, _ => 200 } * 2 + value;
            int B(int value) => value switch { < 8 => 100, > 32 => 100, _ => 200 } * 2 + value;
            int A(int value) => value switch { < 8 => 400, > 16 => 100, _ => 200 } * 1 + value;
            int Srgb = 100;
            switch (format)
            {
                case Format.R4G4B4A4UnormPack16:
                    return R(4) + G(4) + B(4) + A(4);
                case Format.B4G4R4A4UnormPack16:
                    return B(4) + G(4) + R(4) + A(4);
                case Format.R5G5B5A1UnormPack16:
                    return R(5)  + G(5) + B(5) + A(1);
                case Format.B5G5R5A1UnormPack16:
                    return B(5) + G(5) + R(5) + A(1);
                case Format.R8G8B8A8Unorm:
                    return R(8) + G(8) + B(8) + A(8);
                case Format.R8G8B8A8SNorm:
                    return R(8) + G(8) + B(8) + A(8);
                case Format.R8G8B8A8Uscaled:
                    return R(8) + G(8) + B(8) + A(8);
                case Format.R8G8B8A8Sscaled:
                    return R(8) + G(8) + B(8) + A(8);
                case Format.R8G8B8A8Uint:
                    return R(8) + G(8) + B(8) + A(8);
                case Format.R8G8B8A8Sint:
                    return R(8) + G(8) + B(8) + A(8);
                case Format.R8G8B8A8Srgb:
                    return R(8) + G(8) + B(8) + A(8) + Srgb;
                case Format.B8G8R8A8Unorm:
                    return R(8) + G(8) + B(8) + A(8);
                case Format.B8G8R8A8SNorm:
                    return R(8) + G(8) + B(8) + A(8);
                case Format.B8G8R8A8Uscaled:
                    return R(8) + G(8) + B(8) + A(8);
                case Format.B8G8R8A8Sscaled:
                    return R(8) + G(8) + B(8) + A(8);
                case Format.B8G8R8A8Uint:
                    return R(8) + G(8) + B(8) + A(8);
                case Format.B8G8R8A8Sint:
                    return R(8) + G(8) + B(8) + A(8);
                case Format.B8G8R8A8Srgb:
                    return R(8) + G(8) + B(8) + A(8) + Srgb;
                case Format.A8B8G8R8UnormPack32:
                    return R(8) + G(8) + B(8) + A(8);
                case Format.A8B8G8R8SNormPack32:
                    return R(8) + G(8) + B(8) + A(8);
                case Format.A8B8G8R8UscaledPack32:
                    return R(8) + G(8) + B(8) + A(8);
                case Format.A8B8G8R8SscaledPack32:
                    return R(8) + G(8) + B(8) + A(8);
                case Format.A8B8G8R8UintPack32:
                    return R(8) + G(8) + B(8) + A(8);
                case Format.A8B8G8R8SintPack32:
                    return R(8) + G(8) + B(8) + A(8);
                case Format.A8B8G8R8SrgbPack32:
                    return R(8) + G(8) + B(8) + A(8) + Srgb;
                case Format.A2R10G10B10UnormPack32:
                    return R(10) + G(10) + B(10) + A(2);
                case Format.A2R10G10B10SNormPack32:
                    return R(10) + G(10) + B(10) + A(2);
                case Format.A2R10G10B10UscaledPack32:
                    return R(10) + G(10) + B(10) + A(2);
                case Format.A2R10G10B10SscaledPack32:
                    return R(10) + G(10) + B(10) + A(2);
                case Format.A2R10G10B10UintPack32:
                    return R(10) + G(10) + B(10) + A(2);
                case Format.A2R10G10B10SintPack32:
                    return R(10) + G(10) + B(10) + A(2);
                case Format.A2B10G10R10UnormPack32:
                    return R(10) + G(10) + B(10) + A(2);
                case Format.A2B10G10R10SNormPack32:
                    return R(10) + G(10) + B(10) + A(2);
                case Format.A2B10G10R10UscaledPack32:
                    return R(10) + G(10) + B(10) + A(2);
                case Format.A2B10G10R10SscaledPack32:
                    return R(10) + G(10) + B(10) + A(2);
                case Format.A2B10G10R10UintPack32:
                    return R(10) + G(10) + B(10) + A(2);
                case Format.A2B10G10R10SintPack32:
                    return R(10) + G(10) + B(10) + A(2);
                case Format.R16G16B16A16Unorm:
                    return R(16) + G(16) + B(16) + A(16);
                case Format.R16G16B16A16SNorm:
                    return R(16) + G(16) + B(16) + A(16);
                case Format.R16G16B16A16Uscaled:
                    return R(16) + G(16) + B(16) + A(16);
                case Format.R16G16B16A16Sscaled:
                    return R(16) + G(16) + B(16) + A(16);
                case Format.R16G16B16A16Uint:
                    return R(16) + G(16) + B(16) + A(16);
                case Format.R16G16B16A16Sint:
                    return R(16) + G(16) + B(16) + A(16);
                case Format.R16G16B16A16Sfloat:
                    return R(16) + G(16) + B(16) + A(16);
                case Format.R32G32B32A32Uint:
                    return R(32) + G(32) + B(32) + A(32);
                case Format.R32G32B32A32Sint:
                    return R(32) + G(32) + B(32) + A(32);
                case Format.R32G32B32A32Sfloat:
                    return R(32) + G(32) + B(32) + A(32);
                case Format.R64G64B64A64Uint:
                    return R(64) + G(64) + B(64) + A(64);
                case Format.R64G64B64A64Sint:
                    return R(64) + G(64) + B(64) + A(64);
                case Format.R64G64B64A64Sfloat:
                    return R(64) + G(64) + B(64) + A(64);
                default:
                    return 0; // unusable formats.
            }
        }

        public (Format, int) BestPossibleFormat { get; }

        public FormatRater()
        {
            int max = 0;
            Format best = default;
            foreach (var format in Enum.GetValues<Format>())
            {
                var score = Rate(format);
                if (score > max) 
                {
                    best = format;
                    max = score;
                }
            }

            BestPossibleFormat = (best, max);
        }
    }
}
