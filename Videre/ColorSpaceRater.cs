// This file is part of Silk.NET.
// 
// You may modify and distribute Silk.NET under the terms
// of the MIT license. See the LICENSE file for details.

using System;
using Silk.NET.Vulkan;

namespace Aliquip
{
    internal sealed class ColorSpaceRater
    {
        public static int Rate(ColorSpaceKHR colorSpace)
        {
            switch (colorSpace)
            {
                case ColorSpaceKHR.ColorSpaceSrgbNonlinearKhr:
                    return 1000;
                case ColorSpaceKHR.ColorSpaceDisplayP3NonlinearExt:
                    return 555;
                case ColorSpaceKHR.ColorSpaceExtendedSrgbLinearExt:
                    return 900;
                case ColorSpaceKHR.ColorSpaceDisplayP3LinearExt:
                    return 540;
                case ColorSpaceKHR.ColorSpaceDciP3NonlinearExt:
                    return 550;
                case ColorSpaceKHR.ColorSpaceBT709LinearExt:
                    return 650;
                case ColorSpaceKHR.ColorSpaceBT709NonlinearExt:
                    return 655;
                case ColorSpaceKHR.ColorSpaceBT2020LinearExt:
                    return 680;
                case ColorSpaceKHR.ColorSpaceHdr10ST2084Ext:
                    return 900;
                case ColorSpaceKHR.ColorSpaceDolbyvisionExt:
                    return 500;
                case ColorSpaceKHR.ColorSpaceHdr10HlgExt:
                    return 900;
                case ColorSpaceKHR.ColorSpaceAdobergbLinearExt:
                    return 799;
                case ColorSpaceKHR.ColorSpaceAdobergbNonlinearExt:
                    return 800;
                case ColorSpaceKHR.ColorSpaceExtendedSrgbNonlinearExt:
                    return 950;
                case ColorSpaceKHR.ColorSpaceDisplayNativeAmd:
                    return 500;
                default:
                    return 0;
            }
        }

        public (ColorSpaceKHR, int) BestPossibleColorspace { get; }

        public ColorSpaceRater()
        {
            int max = 0;
            ColorSpaceKHR best = default;
            foreach (var format in Enum.GetValues<ColorSpaceKHR>())
            {
                var score = Rate(format);
                if (score > max)
                {
                    best = format;
                    max = score;
                }
            }

            BestPossibleColorspace = (best, max);
        }
        
    }
}
