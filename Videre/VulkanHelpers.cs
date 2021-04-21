// This file is part of Silk.NET.
// 
// You may modify and distribute Silk.NET under the terms
// of the MIT license. See the LICENSE file for details.

using System;
using Silk.NET.Vulkan;

namespace Videre
{
    public static class VulkanHelpers
    {
        public class VkErrorOutOfHostMemory : Exception { public VkErrorOutOfHostMemory(string? message = null) : base(message) { } }
        public class VkErrorOutOfDeviceMemory : Exception { public VkErrorOutOfDeviceMemory(string? message = null) : base(message) { } }
        public class VkErrorInitializationFailed : Exception { public VkErrorInitializationFailed(string? message = null) : base(message) { } }
        public class VkErrorDeviceLostException : Exception { public VkErrorDeviceLostException(string? message = null) : base(message) { } }
        public class VkErrorMemoryMapFailed : Exception { public VkErrorMemoryMapFailed(string? message = null) : base(message) { } }
        public class VkErrorLayerNotPresent : Exception { public VkErrorLayerNotPresent(string? message = null) : base(message) { } }
        public class VkErrorExtensionNotPresent : Exception { public VkErrorExtensionNotPresent(string? message = null) : base(message) { } }
        public class VkFeatureNotPresent : Exception { public VkFeatureNotPresent(string? message = null) : base(message) { } }
        public class VkErrorIncompatibleDriver : Exception { public VkErrorIncompatibleDriver(string? message = null) : base(message) { } }
        public class VkErrorTooManyObjects : Exception { public VkErrorTooManyObjects(string? message = null) : base(message) { } }
        public class VkErrorFormatNotSupported : Exception { public VkErrorFormatNotSupported(string? message = null) : base(message) { } }
        public class VkErrorFragmentedPool : Exception { public VkErrorFragmentedPool(string? message = null) : base(message) { } }
        public class VkErrorUnknown : Exception { public VkErrorUnknown(string? message = null) : base(message) { } }
        public class VkErrorSurfaceLostKhr : Exception { public VkErrorSurfaceLostKhr(string? message = null) : base(message) { } }
        public class VkErrorNativeWindowInUseKhr : Exception { public VkErrorNativeWindowInUseKhr(string? message = null) : base(message) { } }
        public class VkErrorOutOfDateKhr : Exception { public VkErrorOutOfDateKhr(string? message = null) : base(message) { } }
        public class VkErrorIncompatibleDisplayKhr : Exception { public VkErrorIncompatibleDisplayKhr(string? message = null) : base(message) { } }
        public class VkErrorValidationFailedExt : Exception { public VkErrorValidationFailedExt(string? message = null) : base(message) { } }
        public class ErrorInvalidShaderNV : Exception { public ErrorInvalidShaderNV(string? message = null) : base(message) { } }
        public class ErrorOutOfPoolMemoryKhr : Exception { public ErrorOutOfPoolMemoryKhr(string? message = null) : base(message) { } }
        public class ErrorInvalidExternalHandleKhr : Exception { public ErrorInvalidExternalHandleKhr(string? message = null) : base(message) { } }
        public class ErrorIncompatibleVersionKhr : Exception { public ErrorIncompatibleVersionKhr(string? message = null) : base(message) { } }
        public class VkErrorInvalidDrmFormatModifierPlaneLayoutExt : Exception { public VkErrorInvalidDrmFormatModifierPlaneLayoutExt(string? message = null) : base(message) { } }
        public class VkErrorFragmentationExt : Exception { public VkErrorFragmentationExt(string? message = null) : base(message) { } }
        public class VkErrorNotPermittedExt : Exception { public VkErrorNotPermittedExt(string? message = null) : base(message) { } }
        public class VkErrorInvalidDeviceAddressExt : Exception { public VkErrorInvalidDeviceAddressExt(string? message = null) : base(message) { } }
        public class VkErrorFullScreenExclusiveModeLostExt : Exception { public VkErrorFullScreenExclusiveModeLostExt(string? message = null) : base(message) { } }

        public static void ThrowCode(this Result result, string? message = null)
        {
            switch (result)
            {
                case Result.Success:
                    // yey!
                    break;
                // technically not an error, though one may think they are
                case Result.NotReady:
                case Result.Timeout:
                case Result.EventSet:
                case Result.EventReset:
                case Result.Incomplete:
                    break;
                case Result.ErrorOutOfHostMemory:
                    throw new VkErrorOutOfHostMemory(message);
                case Result.ErrorOutOfDeviceMemory:
                    throw new VkErrorOutOfDeviceMemory(message);
                case Result.ErrorInitializationFailed:
                    throw new VkErrorInitializationFailed(message);
                case Result.ErrorDeviceLost:
                    throw new VkErrorDeviceLostException(message);
                case Result.ErrorMemoryMapFailed:
                    throw new VkErrorMemoryMapFailed(message);
                case Result.ErrorLayerNotPresent:
                    throw new VkErrorLayerNotPresent(message);
                case Result.ErrorExtensionNotPresent:
                    throw new VkErrorExtensionNotPresent(message);
                case Result.ErrorFeatureNotPresent:
                    throw new VkFeatureNotPresent(message);
                case Result.ErrorIncompatibleDriver:
                    throw new VkErrorIncompatibleDriver(message);
                case Result.ErrorTooManyObjects:
                    throw new VkErrorTooManyObjects(message);
                case Result.ErrorFormatNotSupported:
                    throw new VkErrorFormatNotSupported(message);
                case Result.ErrorFragmentedPool:
                    throw new VkErrorFragmentedPool(message);
                case Result.ErrorUnknown:
                    throw new VkErrorUnknown(message);
                case Result.ErrorSurfaceLostKhr:
                    throw new VkErrorSurfaceLostKhr(message);
                case Result.ErrorNativeWindowInUseKhr:
                    throw new VkErrorNativeWindowInUseKhr(message);
                case Result.SuboptimalKhr:
                    break;
                case Result.ErrorOutOfDateKhr:
                    throw new VkErrorOutOfDateKhr();
                case Result.ErrorIncompatibleDisplayKhr:
                    throw new VkErrorIncompatibleDisplayKhr();
                case Result.ErrorValidationFailedExt:
                    throw new VkErrorValidationFailedExt();
                case Result.ErrorInvalidShaderNV:
                    throw new ErrorInvalidShaderNV();
                case Result.ErrorOutOfPoolMemoryKhr:
                    throw new ErrorOutOfPoolMemoryKhr();
                case Result.ErrorInvalidExternalHandleKhr:
                    throw new ErrorInvalidExternalHandleKhr();
                    throw new ErrorIncompatibleVersionKhr();
                case Result.ErrorInvalidDrmFormatModifierPlaneLayoutExt:
                    throw new VkErrorInvalidDrmFormatModifierPlaneLayoutExt();
                case Result.ErrorFragmentationExt:
                    throw new VkErrorFragmentationExt();
                case Result.ErrorNotPermittedExt:
                    throw new VkErrorNotPermittedExt();
                case Result.ErrorInvalidDeviceAddressExt:
                    throw new VkErrorInvalidDeviceAddressExt();
                case Result.ErrorFullScreenExclusiveModeLostExt:
                    throw new VkErrorFullScreenExclusiveModeLostExt();
                case Result.ThreadIdleKhr:
                    break;
                case Result.ThreadDoneKhr:
                    break;
                case Result.OperationDeferredKhr:
                    break;
                case Result.OperationNotDeferredKhr:
                    break;
                case Result.PipelineCompileRequiredExt:
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(result), result, message);
            }
        }
    }
}
