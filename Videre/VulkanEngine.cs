using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Aliquip;
using Silk.NET.Core;
using Silk.NET.Core.Native;
using Silk.NET.Input;
using Silk.NET.Maths;
using Silk.NET.Vulkan;
using Silk.NET.Vulkan.Extensions.EXT;
using Silk.NET.Vulkan.Extensions.KHR;
using Silk.NET.Windowing;

namespace Videre
{
    public sealed unsafe class VulkanEngine : IDisposable
    {
        private Vk _vk;
        public IView View { get; private set; }
        public IInputContext Input { get; private set; }
        private Instance _instance;
        private readonly GCAllocationCallbacks _allocationCallbacks;
        private PhysicalDeviceFeatures _physicalDeviceFeatures;
        private PhysicalDevice _physicalDevice;
        private Device _logicalDevice;
        private KhrSwapchain _khrSwapchain;
        private SurfaceKHR _surface;
        private KhrSurface _khrSurface;
        private SwapchainKHR _swapchain;
        private Format _swapchainFormat;
        private ColorSpaceKHR _swapchainColorSpace;
        private PresentModeKHR _presentMode;
        private RenderGraph _renderGraph;
        private QueueManager _queueManager;
        private Image[] _swapchainImages;
        private FrameData[] _frames = new FrameData[3];
        private int _currentFrame;
        private ExtDebugUtils _debugUtils;
        private IInputContext _input;

        private sealed record FrameData(Semaphore AquireSemaphore, Fence RenderFence, Semaphore RenderSemaphore);

        public VulkanEngine(RenderGraph renderGraph)
        {
            _renderGraph = renderGraph;
            _allocationCallbacks = new GCAllocationCallbacks();
        }

        public event Action<EngineContext> Update;

        [Conditional("DEBUG")]
        private unsafe void Name(ObjectType type, ulong handle, string name)
        {
            if (_debugUtils is not null)
            {
                var ptr = SilkMarshal.StringToPtr(name);
                _debugUtils.SetDebugUtilsObjectName(_logicalDevice,
                        new DebugUtilsObjectNameInfoEXT(objectType: type, objectHandle: handle,
                            pObjectName: (byte*) ptr))
                    .ThrowCode();
                SilkMarshal.Free(ptr);
            }
        }

        private DateTime _lastUpdate;

        public void Initialize(string applicationName, uint applicationVersion)
        {
            Console.WriteLine("Initializing Engine");
            var options = WindowOptions.DefaultVulkan;
            // options.Size = new Vector2D<int>(100, 100);
            View = Window.Create(options);
            View.Initialize();
            Input = View.CreateInput();
            View.UpdatesPerSecond = 60;
            View.FramesPerSecond = -1;
            View.VSync = false;

            InitializeVulkan(applicationName, applicationVersion);
            View.FramebufferResize += FramebufferResize;
            _input = View.CreateInput();
            Console.WriteLine("Initialized Windowing & Input");

            _renderGraph.Initialize(_frames.Length, _queueManager, _vk, _instance, _logicalDevice, _physicalDevice
#if DEBUG
                , _debugUtils
#endif
                );

            Console.WriteLine("Initializing Synchronization");
            for (int i = 0; i < _frames.Length; i++)
            {
                _vk.CreateSemaphore(_logicalDevice, new SemaphoreCreateInfo(flags: 0), null, out var semaphore1).ThrowCode();
                _vk.CreateSemaphore(_logicalDevice, new SemaphoreCreateInfo(flags: 0), null, out var semaphore2).ThrowCode();
                _vk.CreateFence(_logicalDevice, new FenceCreateInfo(flags: FenceCreateFlags.FenceCreateSignaledBit), null,
                    out var fence).ThrowCode();
                _frames[i] = new FrameData(semaphore1, fence, semaphore2);
                Name(ObjectType.Semaphore, semaphore1.Handle, $"Frame {i} Aquire");
                Name(ObjectType.Semaphore, semaphore2.Handle, $"Frame {i} Render");
                Name(ObjectType.Fence, fence.Handle, $"Frame {i} Fence");
            }
            
            FramebufferResize(View.FramebufferSize);
            
            View.Render += (_) => Render();
            View.Update += (_) => CoreUpdate();
            Console.WriteLine("Successfully Initialized Engine!");
        }

        private void CoreUpdate()
        {
            var engineCtx = _renderGraph.EngineContext;
            engineCtx.PreUpdate();
            Update?.Invoke(engineCtx);
            engineCtx.PostUpdate();
        }

        private void FramebufferResize(Vector2D<int> obj)
        {
            if (obj.X <= 0 || obj.Y <= 0)
                return;
            
            _khrSwapchain.DestroySwapchain(_logicalDevice, _swapchain, null);
            _khrSwapchain.CreateSwapchain(_logicalDevice,
                    new SwapchainCreateInfoKHR(surface: _surface, minImageCount: 3, imageFormat: _swapchainFormat,
                        imageColorSpace: _swapchainColorSpace, imageExtent: new Extent2D((uint) obj.X, (uint) obj.Y),
                        imageArrayLayers: 1, imageUsage: ImageUsageFlags.ImageUsageTransferDstBit | ImageUsageFlags.ImageUsageStorageBit,
                        imageSharingMode: SharingMode.Exclusive,
                        queueFamilyIndexCount: 0, pQueueFamilyIndices: null, // ignored due to SharingMode.Exclusive
                        presentMode: _presentMode, clipped: false,
                        preTransform: SurfaceTransformFlagsKHR.SurfaceTransformIdentityBitKhr,
                        compositeAlpha: CompositeAlphaFlagsKHR.CompositeAlphaOpaqueBitKhr), null, out _swapchain)
                .ThrowCode();
            
            uint swapchainCount = 0;
            _khrSwapchain.GetSwapchainImages(_logicalDevice, _swapchain, ref swapchainCount, null).ThrowCode();
            _swapchainImages = new Image[swapchainCount];
            fixed (Image* p = _swapchainImages)
                _khrSwapchain.GetSwapchainImages(_logicalDevice, _swapchain, ref swapchainCount, p).ThrowCode();
            
            _renderGraph.ChangeTargetImages((Vector2D<uint>) obj, _swapchainImages, _swapchainFormat, _swapchainColorSpace);
        }

        private void InitializeVulkan(string applicationName, uint applicationVersion)
        {
            Console.WriteLine("Initializing Vulkan");
#if VALIDITION
            DebugUtilsMessengerCreateInfoEXT GetDebugMessenger(void* pNext)
            {
                return new DebugUtilsMessengerCreateInfoEXT(
                    messageSeverity: DebugUtilsMessageSeverityFlagsEXT.DebugUtilsMessageSeverityVerboseBitExt |
                                     DebugUtilsMessageSeverityFlagsEXT.DebugUtilsMessageSeverityInfoBitExt |
                                     DebugUtilsMessageSeverityFlagsEXT.DebugUtilsMessageSeverityWarningBitExt |
                                     DebugUtilsMessageSeverityFlagsEXT.DebugUtilsMessageSeverityErrorBitExt,
                    messageType: DebugUtilsMessageTypeFlagsEXT.DebugUtilsMessageTypeGeneralBitExt |
                                 DebugUtilsMessageTypeFlagsEXT.DebugUtilsMessageTypePerformanceBitExt |
                                 DebugUtilsMessageTypeFlagsEXT.DebugUtilsMessageTypeValidationBitExt,
                    pfnUserCallback: new PfnDebugUtilsMessengerCallbackEXT(DebugCallback), pNext: pNext);
            }
#endif

            _vk = Vk.GetApi();

            const uint engineVersion = 1;
            const string engineName = "Videre";

            var instanceLayers = new List<string>();
            var instanceExtensions = new List<string>();

            var pApplicationName = SilkMarshal.StringToPtr(applicationName);
            var pEngineName = SilkMarshal.StringToPtr(engineName);
            var applicationInfo = new ApplicationInfo(pApplicationName: (byte*) pApplicationName,
                applicationVersion: applicationVersion, pEngineName: (byte*) pEngineName, engineVersion: engineVersion,
                apiVersion: new Version32(1, 1, 0));

            Version32 apiVersion = default;
            _vk.EnumerateInstanceVersion((uint*) &apiVersion);
            Console.WriteLine($"Instance Version: {apiVersion.Major}.{apiVersion.Minor}.{apiVersion.Patch}");
            
            void* instancepNext = default;
            
            // instanceExtensions.Add(KhrSurface.ExtensionName);
            instanceExtensions.AddRange(SilkMarshal.PtrToStringArray((nint)View.VkSurface.GetRequiredExtensions(out var requiredExtensionsCount), (int)requiredExtensionsCount));
            instanceExtensions.Add(ExtDebugUtils.ExtensionName);

            Console.WriteLine($"Creating Instance with {instanceExtensions.Count} extensions");
            VerifyInstanceExtensionsAvailable(_vk, instanceExtensions);

            var ppEnabledLayers = instanceLayers.Count > 0 ? (byte**)SilkMarshal.StringArrayToPtr(instanceLayers) : null;
            var ppEnabledExtensions = instanceExtensions.Count > 0 ? (byte**)SilkMarshal.StringArrayToPtr(instanceExtensions) : null;

            _vk.CreateInstance(
                    new InstanceCreateInfo(pApplicationInfo: &applicationInfo,
                        enabledLayerCount: (uint) instanceLayers.Count, ppEnabledLayerNames: ppEnabledLayers,
                        enabledExtensionCount: (uint) instanceExtensions.Count,
                        ppEnabledExtensionNames: ppEnabledExtensions, pNext: instancepNext), _allocationCallbacks.AllocationCallbacks, out _instance)
                .ThrowCode();
            SilkMarshal.Free((nint) ppEnabledLayers);
            SilkMarshal.Free((nint) ppEnabledExtensions);

            _vk.CurrentInstance = _instance;
            
            if (!_vk.TryGetInstanceExtension(_instance, out _khrSurface))
                Console.WriteLine($"Could not load {KhrSurface.ExtensionName}");

            _vk.TryGetInstanceExtension(_instance, out _debugUtils);
            
            Console.WriteLine("Creating Surface");
            _surface = View.VkSurface.Create(_instance.ToHandle(), (AllocationCallbacks*) null).ToSurface();

            uint deviceCount = 0;
            _vk.EnumeratePhysicalDevices(_instance, ref deviceCount, null).ThrowCode();
            var devices = (PhysicalDevice*)SilkMarshal.Allocate((int) (deviceCount * sizeof(PhysicalDevice)));
            _vk.EnumeratePhysicalDevices(_instance, ref deviceCount, devices).ThrowCode();
            Console.WriteLine($"Found {deviceCount} devices");

            Console.WriteLine("Creating Device");
            // TODO: actually somehow reasonably find the best device.
            for (int i = 0; i < deviceCount; i++)
            {
                var physicalDevice = devices[i];
                _physicalDeviceFeatures = _vk.GetPhysicalDeviceFeature(physicalDevice);

                uint presentModeCount = 0;
                _khrSurface.GetPhysicalDeviceSurfacePresentModes(physicalDevice, _surface, ref presentModeCount, null).ThrowCode();
                if (presentModeCount <= 0)
                    continue;
                
                var presentModes = (PresentModeKHR*)SilkMarshal.Allocate((int) (presentModeCount * sizeof(PresentModeKHR)));
                _khrSurface.GetPhysicalDeviceSurfacePresentModes(physicalDevice, _surface, ref presentModeCount, presentModes).ThrowCode();

                _presentMode = PresentModeKHR.PresentModeFifoKhr;
                View.FramesPerSecond = -1;
                for (int j = 0; j < presentModeCount; j++)
                {
                    if (presentModes[j] == PresentModeKHR.PresentModeMailboxKhr)
                    {
                        _presentMode = PresentModeKHR.PresentModeMailboxKhr;
                        View.FramesPerSecond = -1;
                        break;
                    }
                }

                SilkMarshal.Free((nint) presentModes);

                uint surfaceFormatCount = 0;
                _khrSurface.GetPhysicalDeviceSurfaceFormats(physicalDevice, _surface, ref surfaceFormatCount, null).ThrowCode();
                var surfaceFormats = (SurfaceFormatKHR*)SilkMarshal.Allocate((int) (surfaceFormatCount * sizeof(SurfaceFormatKHR)));
                _khrSurface.GetPhysicalDeviceSurfaceFormats(physicalDevice, _surface, ref surfaceFormatCount, surfaceFormats).ThrowCode();
                int max = int.MinValue;
                SurfaceFormatKHR maxFormat = surfaceFormats[0];
                for (int j = 0; j < surfaceFormatCount; j++)
                {
                    var score = FormatRater.Rate(surfaceFormats[j].Format) + ColorSpaceRater.Rate(surfaceFormats[j].ColorSpace);
                    if (score > max)
                    {
                        max = score;
                        maxFormat = surfaceFormats[j];
                    }
                }
                SilkMarshal.Free((nint) surfaceFormats);
                
                _swapchainFormat = maxFormat.Format;
                _swapchainColorSpace = maxFormat.ColorSpace;
                Console.WriteLine($"Chose Swapchain Properties: {Enum.GetName(typeof(PresentModeKHR), _presentMode)} {Enum.GetName(typeof(Format), _swapchainFormat)} {Enum.GetName(typeof(ColorSpaceKHR), _swapchainColorSpace)}");
                
                _khrSurface.GetPhysicalDeviceSurfaceCapabilities(physicalDevice, _surface, out var surfaceCapabilities).ThrowCode();

                uint queueFamilyPropertyCount = 0;
                _vk.GetPhysicalDeviceQueueFamilyProperties(physicalDevice, ref queueFamilyPropertyCount, null);
                var deviceQueueFamilyProperties = (QueueFamilyProperties*)SilkMarshal.Allocate((int) (queueFamilyPropertyCount * sizeof(QueueFamilyProperties)));
                _vk.GetPhysicalDeviceQueueFamilyProperties(physicalDevice, ref queueFamilyPropertyCount, deviceQueueFamilyProperties);

                var queueCreateInfoList = new List<DeviceQueueCreateInfo>();
                var deviceExtensions = new List<string>();
                var deviceLayers = new List<string>();

                for (int j = 0; j < queueFamilyPropertyCount; j++)
                {
                    var queueCount = deviceQueueFamilyProperties[j].QueueCount;
                    float* pQueuePriorities = stackalloc float[(int)queueCount]; // queue count should generally be 1
                    for (int k = 0; k < queueCount; k++)
                        pQueuePriorities[k] = 1.0f;
                    
                    queueCreateInfoList.Add(new DeviceQueueCreateInfo(queueFamilyIndex: (uint) j, queueCount: queueCount, pQueuePriorities: pQueuePriorities));
                }

                deviceExtensions.Add(KhrSwapchain.ExtensionName);
                // deviceExtensions.Add(KhrSynchronization2.ExtensionName);
                // deviceExtensions.Add(ExtBufferDeviceAddress.ExtensionName);

                var features = new PhysicalDeviceFeatures();
                features.ShaderInt64 = true;
                
                void* devicePNext = null;

//                 var physicalDeviceDescriptorIndexingFeaturesExt = new PhysicalDeviceDescriptorIndexingFeaturesEXT(
//                     descriptorBindingSampledImageUpdateAfterBind: true,
//                     descriptorBindingStorageBufferUpdateAfterBind: true,
//                     descriptorBindingStorageImageUpdateAfterBind: true,
//                     descriptorBindingUniformBufferUpdateAfterBind: true,
//                     descriptorBindingStorageTexelBufferUpdateAfterBind: true,
//                     descriptorBindingUniformTexelBufferUpdateAfterBind: true,
//                     descriptorBindingUpdateUnusedWhilePending: true, 
//                     runtimeDescriptorArray: true,
//                     pNext: devicePNext);
//                 devicePNext = &physicalDeviceDescriptorIndexingFeaturesExt;
// 
                var physicalDeviceBufferDeviceAddressFeatures = new PhysicalDeviceBufferDeviceAddressFeatures(bufferDeviceAddress: true,
#if DEBUG
                    bufferDeviceAddressCaptureReplay: true,
#endif
                    pNext: devicePNext);
               devicePNext = &physicalDeviceBufferDeviceAddressFeatures;

//                 var version12 = new PhysicalDeviceVulkan12Features(bufferDeviceAddress: true,
// #if DEBUG
//                     bufferDeviceAddressCaptureReplay: true,
// #endif
//                    descriptorBindingSampledImageUpdateAfterBind: true,
//                    descriptorBindingStorageBufferUpdateAfterBind: true,
//                    descriptorBindingStorageImageUpdateAfterBind: true,
//                    descriptorBindingUniformBufferUpdateAfterBind: true,
//                    descriptorBindingStorageTexelBufferUpdateAfterBind: true,
//                    descriptorBindingUniformTexelBufferUpdateAfterBind: true,
//                    descriptorBindingUpdateUnusedWhilePending: true, 
//                    runtimeDescriptorArray: true,
//                     pNext: devicePNext);
//                 devicePNext = &version12;
                
                var queueCreateInfos = queueCreateInfoList.Distinct().ToArray();
                queueCreateInfoList = null;

                VerifyDeviceExtensionsAvailable(_vk, devices[i], deviceExtensions, ref deviceLayers);
                
                _physicalDevice = devices[i];
                
                Console.WriteLine("Creating Logical Device");
                var ppDeviceExtensions = deviceExtensions.Count > 0 ? (byte**)SilkMarshal.StringArrayToPtr(deviceExtensions) : null;
                var ppDeviceLayers = deviceLayers.Count > 0 ? (byte**)SilkMarshal.StringArrayToPtr(deviceLayers) : null;
                fixed (DeviceQueueCreateInfo* pQueueCreateInfos = queueCreateInfos)
                    _vk.CreateDevice(physicalDevice,
                            new DeviceCreateInfo(queueCreateInfoCount: (uint) queueCreateInfos.Length,
                                pQueueCreateInfos: pQueueCreateInfos,
                                enabledExtensionCount: (uint) deviceExtensions.Count,
                                enabledLayerCount: (uint) deviceLayers.Count,
                                ppEnabledExtensionNames: ppDeviceExtensions,
                                ppEnabledLayerNames: ppDeviceLayers, pEnabledFeatures: &features, pNext: devicePNext), null,
                            out _logicalDevice)
                        .ThrowCode();
                _vk.CurrentDevice = _logicalDevice;
                
                if (!_vk.TryGetDeviceExtension(_instance, _logicalDevice, out _khrSwapchain))
                    Console.WriteLine($"Could not load {KhrSwapchain.ExtensionName}!");
                
                _queueManager = new(_vk, _khrSurface, _instance, _physicalDevice, _logicalDevice,  _surface, new Span<QueueFamilyProperties>(deviceQueueFamilyProperties, (int)queueFamilyPropertyCount));
                Console.WriteLine($"{_queueManager.QueueCount} queues found");

                SilkMarshal.Free((nint) ppDeviceExtensions);
                SilkMarshal.Free((nint) ppDeviceLayers);
                SilkMarshal.Free((nint) deviceQueueFamilyProperties);
                break;
            }
            SilkMarshal.Free((nint) devices);
            Console.WriteLine("Initialized Vulkan");
        }

        private void VerifyInstanceExtensionsAvailable(Vk vk, List<string> extensions)
        {
            var copy = extensions.ToList();
            uint propertyCount = 0;
            vk.EnumerateInstanceExtensionProperties((byte*) null, ref propertyCount, null).ThrowCode();
            var properties = (ExtensionProperties*)SilkMarshal.Allocate((int) (propertyCount * sizeof(ExtensionProperties)));
            vk.EnumerateInstanceExtensionProperties((byte*) null, ref propertyCount, properties).ThrowCode();
            for (int i = 0; i < propertyCount; i++)
            {
                var name = SilkMarshal.PtrToString((nint) properties[i].ExtensionName);
                copy.Remove(name);
            }

            foreach (var ext in copy)
            {
                Console.WriteLine($"Missing {ext}");
                extensions.Remove(ext);
            }
        }
        
        private void VerifyDeviceExtensionsAvailable(Vk vk, PhysicalDevice physicalDevice, List<string> extensions, ref List<string> layers)
        {
            var copy = extensions.ToList();
            uint propertyCount = 0;
            vk.EnumerateDeviceExtensionProperties(physicalDevice, (byte*) null, ref propertyCount, null).ThrowCode();
            var properties = (ExtensionProperties*)SilkMarshal.Allocate((int) (propertyCount * sizeof(ExtensionProperties)));
            vk.EnumerateDeviceExtensionProperties(physicalDevice, (byte*) null, ref propertyCount, properties).ThrowCode();
            for (int i = 0; i < propertyCount; i++)
            {
                var name = SilkMarshal.PtrToString((nint) properties[i].ExtensionName);
                copy.Remove(name);
            }

            foreach (var ext in copy)
            {
                if (ext == KhrSynchronization2.ExtensionName)
                {
                    layers.Add("VK_LAYER_KHRONOS_synchronization2");
                    Console.WriteLine("Attempting to enable VK_LAYER_KHRONOS_synchronization2");
                }
                
                Console.WriteLine($"Missing {ext}");
            }
        }

        public void Run()
        {
            View.Run(() =>
            {
                View.DoEvents();
                if (View.IsClosing)
                    return;
                View.DoUpdate();
                View.DoRender();
            });
            View.DoEvents();
            View.Reset();
        }

        private void Render()
        {
            _renderGraph.PreRun(_currentFrame);
            var currentFrame = _frames[_currentFrame];
            var waitCode = _vk.WaitForFences(_logicalDevice, 1, currentFrame.RenderFence, true, ulong.MaxValue);
            waitCode.ThrowCode();
            
            _vk.ResetFences(_logicalDevice, 1, currentFrame.RenderFence);
            
            var aquireSemaphore = currentFrame.AquireSemaphore;
            uint imageIndex = 0;
            _khrSwapchain.AcquireNextImage(_logicalDevice, _swapchain, UInt64.MaxValue, aquireSemaphore, default,
                ref imageIndex).ThrowCode();
            var renderSemaphore = currentFrame.RenderSemaphore;
            _renderGraph.Run(_currentFrame, (int)imageIndex, aquireSemaphore, renderSemaphore, currentFrame.RenderFence);

            fixed (SwapchainKHR* pSwapchains = &_swapchain)
                _khrSwapchain.QueuePresent(_renderGraph.Queue,
                    new PresentInfoKHR(waitSemaphoreCount: 1, pWaitSemaphores: &renderSemaphore, swapchainCount: 1,
                        pSwapchains: pSwapchains, pImageIndices: &imageIndex)).ThrowCode();
            
            _currentFrame = (_currentFrame + 1) % _frames.Length;
        }

        public void Dispose()
        {
            _vk.DeviceWaitIdle(_logicalDevice).ThrowCode();

            _renderGraph.Dispose();

            _khrSwapchain.DestroySwapchain(_logicalDevice, _swapchain, null);
            
            _vk.DestroyDevice(_logicalDevice, null);
            _khrSurface.DestroySurface(_instance, _surface, null);
            _vk.DestroyInstance(_instance, _allocationCallbacks.AllocationCallbacks);
            _vk?.Dispose();
            _khrSurface.Dispose();
            _khrSwapchain.Dispose();
            _input.Dispose();
            View?.Dispose();
            _allocationCallbacks.Dispose();
        }
    }
}
