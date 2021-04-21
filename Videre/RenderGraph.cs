using System;
using System.Collections.Generic;
using System.Data.SqlTypes;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;
using Silk.NET.Core;
using Silk.NET.Core.Native;
using Silk.NET.Maths;
using Silk.NET.Vulkan;
using Silk.NET.Vulkan.Extensions.EXT;
using VMASharp;
using Buffer = Silk.NET.Vulkan.Buffer;

namespace Videre
{
    public sealed class RenderGraph : IDisposable
    {
        private QueueManager _queueManager;
        private Vk _vk;
        private Instance _instance;
        private Device _device;
        public Queue Queue { get; private set; }
        public uint QueueFamilyIndex { get; private set; }
        public EngineContext EngineContext { get; private set; }

        private FrameData[] _frames;
        private ReadOnlyMemory<Image> _targetImages;
        private ImageView[] _targetImageViews;
        private Vector2D<uint> _targetSize;
        private MemoryType[] _memoryTypes;
        private VulkanMemoryAllocator _vma;
        private ComputeShader _computeShader;
        private PipelineLayout _pipelineLayout;
        private DescriptorPool _descriptorPool;
        private DescriptorSetLayout _setLayout;
        private DescriptorSet[] _targetDescriptorSets;
#if DEBUG
        private ExtDebugUtils _debugUtils;
#endif
        private Buffer[] _commandBuffers;
        private Allocation[] _commandAllocs;
        private int[] _commandBufferCapacities;

        private sealed record FrameData(CommandPool CommandPool);
        

        [Conditional("DEBUG")]
        private unsafe void Name(ObjectType type, ulong handle, string name)
        {
#if DEBUG
            if (_debugUtils is not null)
            {
                var ptr = SilkMarshal.StringToPtr(name);
                _debugUtils.SetDebugUtilsObjectName(_device,
                        new DebugUtilsObjectNameInfoEXT(objectType: type, objectHandle: handle,
                            pObjectName: (byte*) ptr))
                    .ThrowCode();
                SilkMarshal.Free(ptr);
            }
#endif
        }
        
        public unsafe void Initialize(
            int maxFrames,
            QueueManager queueManager,
            Vk vk,
            Instance instance,
            Device device,
            PhysicalDevice physicalDevice
#if DEBUG
            , ExtDebugUtils debugUtils
#endif
            )
        {
#if DEBUG
            _debugUtils = debugUtils;
#endif
            _vma = new VulkanMemoryAllocator(new VulkanMemoryAllocatorCreateInfo(new Version32(1, 1, 0), vk, instance, physicalDevice, device, AllocatorCreateFlags.BufferDeviceAddress));
            _frames = new FrameData[maxFrames];
            _queueManager = queueManager;
            _vk = vk;
            _instance = instance;
            _device = device;
            // if (!_vk.TryGetDeviceExtension(_instance, _device, out _khrSynchronization2))
            //     throw new Exception($"{KhrSynchronization2.ExtensionName} not found!");
            Console.WriteLine("Initializing Render Graph");

            (QueueFamilyIndex, Queue) = _queueManager.GetQueue(true, true, false, true);

            Console.WriteLine("Creating Command Pools");
            for (int i = 0; i < _frames.Length; i++)
            {
                _vk.CreateCommandPool(_device, new CommandPoolCreateInfo(queueFamilyIndex: QueueFamilyIndex), null,
                    out var commandPool).ThrowCode();

                _frames[i] = new FrameData(commandPool);
            }

            Console.WriteLine("Creating Descriptor Layout");
            var bindings = stackalloc DescriptorSetLayoutBinding[]
            {
                new DescriptorSetLayoutBinding(0, DescriptorType.StorageImage, 1,
                    ShaderStageFlags.ShaderStageComputeBit, null),
                new DescriptorSetLayoutBinding(1, DescriptorType.StorageBuffer, 1,
                    ShaderStageFlags.ShaderStageComputeBit, null),
            };
            _vk.CreateDescriptorSetLayout(_device,
                new DescriptorSetLayoutCreateInfo(bindingCount: 2, pBindings: bindings), null, out var setLayout).ThrowCode();
            _setLayout = setLayout;
            Name(ObjectType.DescriptorSetLayout, _setLayout.Handle, "Primary Set Layout");

            var pushConstantRange = new PushConstantRange(ShaderStageFlags.ShaderStageComputeBit, 0, sizeof(uint) * 2);
            _vk.CreatePipelineLayout(_device,
                new PipelineLayoutCreateInfo(setLayoutCount: 1, pSetLayouts: &setLayout, pushConstantRangeCount: 1,
                    pPushConstantRanges: &pushConstantRange), null, out _pipelineLayout).ThrowCode();
            
            Name(ObjectType.PipelineLayout, _pipelineLayout.Handle, "Primary Pipeline Layout");

            Console.WriteLine("Loading Primary Compute");
            _computeShader = new ComputeShader(vk, instance, device, physicalDevice, _pipelineLayout,
                (ReadOnlySpan<byte>) File.ReadAllBytes("./shaders/compute.spv"), "main", null);

            _vk.GetPhysicalDeviceMemoryProperties(physicalDevice, out var memoryProperties);
            _memoryTypes = new MemoryType[32];
            new Span<MemoryType>(&memoryProperties.MemoryTypes.Element0, 32).CopyTo(_memoryTypes);

            EngineContext = new EngineContext();
            Console.WriteLine("Done Initializing Render Graph");
        }

        public unsafe void ChangeTargetImages(
            Vector2D<uint> targetSize,
            ReadOnlyMemory<Image> targetImages,
            Format format,
            ColorSpaceKHR colorSpace)
        {
            _commandAllocs = new Allocation[targetImages.Length];
            _commandBuffers = new Buffer[targetImages.Length];
            _commandBufferCapacities = new int[targetImages.Length];
            _commandBufferCapacities.AsSpan().Fill(-1);
            
            if (_descriptorPool.Handle != 0)
                _vk.DestroyDescriptorPool(_device, _descriptorPool, null);

            _targetSize = targetSize;
            _targetImages = targetImages;
            _targetImageViews = new ImageView[targetImages.Length];

            var poolSizes = stackalloc  DescriptorPoolSize[]
                {
                    new DescriptorPoolSize(DescriptorType.StorageImage, (uint) targetImages.Length),
                    new DescriptorPoolSize(DescriptorType.StorageBuffer, (uint) targetImages.Length),
                };
            _vk.CreateDescriptorPool(_device,
                new DescriptorPoolCreateInfo(maxSets: (uint) targetImages.Length, poolSizeCount: 2,
                    pPoolSizes: poolSizes), null, out _descriptorPool).ThrowCode();
            Name(ObjectType.DescriptorPool, _descriptorPool.Handle, $"Target Descriptor Pool");

            _targetDescriptorSets = new DescriptorSet[targetImages.Length];
            var setLayouts = stackalloc DescriptorSetLayout[targetImages.Length];
            new Span<DescriptorSetLayout>(setLayouts, targetImages.Length).Fill(_setLayout);
            var info = new DescriptorSetAllocateInfo(descriptorPool: _descriptorPool,
                descriptorSetCount: (uint) targetImages.Length, pSetLayouts: setLayouts);
            _vk.AllocateDescriptorSets(_device, &info, _targetDescriptorSets.AsSpan()).ThrowCode();

            var imageInfos = stackalloc DescriptorImageInfo[targetImages.Length];
            Span<WriteDescriptorSet> writes = stackalloc WriteDescriptorSet[targetImages.Length];
            for (int i = 0; i < targetImages.Length; i++)
            {
                Name(ObjectType.Image, _targetImages.Span[i].Handle, $"Target Image {i}");
                
                // _vk.CreateImage(_device,
                //     new ImageCreateInfo(imageType: ImageType.ImageType2D, format: Format.R8G8B8A8Srgb,
                //         extent: new Extent3D(_targetSize.X, _targetSize.Y, 1), mipLevels: 1, arrayLayers: 1,
                //         samples: SampleCountFlags.SampleCount1Bit, tiling: ImageTiling.Optimal,
                //         initialLayout: ImageLayout.Undefined, usage: ImageUsageFlags.ImageUsageStorageBit,
                //         sharingMode: SharingMode.Exclusive, queueFamilyIndexCount: 0, pQueueFamilyIndices: null), null,
                //     out _ownImages[i]).ThrowCode();

                _vk.CreateImageView(_device,
                    new ImageViewCreateInfo(image: _targetImages.Span[i], viewType: ImageViewType.ImageViewType2D,
                        format: format,
                        components: new ComponentMapping(ComponentSwizzle.Identity, ComponentSwizzle.Identity,
                            ComponentSwizzle.Identity, ComponentSwizzle.Identity),
                        subresourceRange: new ImageSubresourceRange(ImageAspectFlags.ImageAspectColorBit, 0, 1, 0, 1)),
                    null, out _targetImageViews[i]).ThrowCode();
                Name(ObjectType.ImageView, _targetImageViews[i].Handle, $"Target Image View {i}");

                imageInfos[i] = new DescriptorImageInfo(imageView: _targetImageViews[i], imageLayout: ImageLayout.General);
                writes[i] = new WriteDescriptorSet(dstSet: _targetDescriptorSets[i], dstBinding: 0, dstArrayElement: 0,
                    descriptorCount: 1, descriptorType: DescriptorType.StorageImage, pImageInfo: (imageInfos + i));
                
                Name(ObjectType.DescriptorSet, _targetDescriptorSets[i].Handle, $"Target Descriptor {i}"); 
            }

            _vk.UpdateDescriptorSets(_device, (uint) targetImages.Length, writes, 0, (CopyDescriptorSet*) null);
            
        }

        public unsafe void PreRun(int frameIndex)
        {
            var oldCapacity = _commandBufferCapacities[frameIndex];
            if (oldCapacity < EngineContext.Data.Capacity)
            {
                _commandAllocs[frameIndex]?.Dispose();
                var oldBuffer = _commandBuffers[frameIndex];
                if (oldBuffer.Handle != 0)
                    _vk.DestroyBuffer(_device, oldBuffer, null);

                _commandBuffers[frameIndex] = _vma.CreateBuffer(
                    new BufferCreateInfo(size: (ulong) (EngineContext.Data.Capacity * sizeof(uint)),
                        usage: BufferUsageFlags.BufferUsageStorageBufferBit, sharingMode: SharingMode.Exclusive),
                    new AllocationCreateInfo(AllocationCreateFlags.Mapped, 0, MemoryUsage.GPU_Only,
                        MemoryPropertyFlags.MemoryPropertyHostVisibleBit), out _commandAllocs[frameIndex]);
                _commandBufferCapacities[frameIndex] = EngineContext.Data.Capacity;
            }
            
            var ptr = (uint*) _commandAllocs[frameIndex].MappedData;
            if (ptr is null)
            {
                Console.WriteLine("AAA");
                return;
            }

            var data = EngineContext.Data;
            for (int i = 0; i < data.Count; i++)
            {
                ptr[i] = data[i];
            }

            _commandAllocs[frameIndex].Invalidate(0, sizeof(uint) * EngineContext.Data.Count);
        }
        
        public unsafe void Run(int currentFrameIndex, int imageIndex, Semaphore waitSemaphore, Semaphore signalSemaphore, Fence signal)
        {
            var bufferInfo = new DescriptorBufferInfo(_commandBuffers[currentFrameIndex], 0,
                (ulong) (sizeof(uint) * _commandBufferCapacities[currentFrameIndex]));
            _vk.UpdateDescriptorSets(_device, 1,
                new WriteDescriptorSet(dstSet: _targetDescriptorSets[currentFrameIndex], dstBinding: 1, dstArrayElement: 0,
                    descriptorCount: 1, descriptorType: DescriptorType.StorageBuffer, pBufferInfo: &bufferInfo), 0,
                null);
            
            
            var currentFrame = _frames[currentFrameIndex];
            _vk.ResetCommandPool(_device, currentFrame.CommandPool, 0).ThrowCode();
            _vk.AllocateCommandBuffers(_device,
                new CommandBufferAllocateInfo(commandPool: currentFrame.CommandPool, level: CommandBufferLevel.Primary,
                    commandBufferCount: 1), out var commandBuffer).ThrowCode();
            
            Name(ObjectType.CommandBuffer, (ulong) commandBuffer.Handle, $"Command Buffer Frame {currentFrameIndex}");

            var renderContext = new RenderContext(_targetImageViews[imageIndex],
                _targetSize, currentFrameIndex);
            _vk.BeginCommandBuffer(commandBuffer,
                    new CommandBufferBeginInfo(flags: CommandBufferUsageFlags.CommandBufferUsageOneTimeSubmitBit))
                .ThrowCode();

            // var memoryBarrier = new ImageMemoryBarrier2KHR(srcStageMask: PipelineStageFlags2KHR.PipelineStage2TopOfPipeBitKhr,
            //     srcAccessMask: AccessFlags2KHR.Access2NoneKhr, dstStageMask: PipelineStageFlags2KHR.PipelineStage2TransferBitKhr,
            //     dstAccessMask: AccessFlags2KHR.Access2TransferWriteBitKhr,
            //     oldLayout: ImageLayout.Undefined, newLayout: ImageLayout.TransferDstOptimal,
            //     srcQueueFamilyIndex: Vk.QueueFamilyIgnored, dstQueueFamilyIndex: Vk.QueueFamilyIgnored,
            //     image: _targetImages.Span[imageIndex], subresourceRange: new ImageSubresourceRange(ImageAspectFlags.ImageAspectColorBit, 0, 1, 0, 1),
            //     sType: StructureType.ImageMemoryBarrier2Khr, pNext: null);
            // _khrSynchronization2.CmdPipelineBarrier2(commandBuffer, new DependencyInfoKHR(imageMemoryBarrierCount: 1,
            //     pImageMemoryBarriers: &memoryBarrier, dependencyFlags: 0, memoryBarrierCount: 0, pMemoryBarriers: null, bufferMemoryBarrierCount: 0, pBufferMemoryBarriers: null));

            _vk.CmdPipelineBarrier(commandBuffer, PipelineStageFlags.PipelineStageTopOfPipeBit,
                PipelineStageFlags.PipelineStageTransferBit, 0, 0, null, 0, null, 1,
                new ImageMemoryBarrier(srcAccessMask: AccessFlags.AccessNoneKhr,
                    dstAccessMask: AccessFlags.AccessTransferWriteBit, oldLayout: ImageLayout.Undefined,
                    newLayout: ImageLayout.TransferDstOptimal, srcQueueFamilyIndex: Vk.QueueFamilyIgnored,
                    dstQueueFamilyIndex: Vk.QueueFamilyIgnored, image: _targetImages.Span[imageIndex],
                    subresourceRange: new ImageSubresourceRange(ImageAspectFlags.ImageAspectColorBit, 0, 1, 0, 1)));

           _vk.CmdClearColorImage(commandBuffer, _targetImages.Span[imageIndex], ImageLayout.TransferDstOptimal,
               new ClearColorValue(0f, 0f, 0f, 1f), 1,
               new ImageSubresourceRange(ImageAspectFlags.ImageAspectColorBit, 0, 1, 0, 1));

           // var memoryBarrier2 = new ImageMemoryBarrier2KHR(srcStageMask: PipelineStageFlags2KHR.PipelineStage2TransferBitKhr,
           //     srcAccessMask: AccessFlags2KHR.Access2TransferWriteBitKhr, dstStageMask: PipelineStageFlags2KHR.PipelineStage2ComputeShaderBitKhr,
           //     dstAccessMask: AccessFlags2KHR.Access2ShaderReadBitKhr | AccessFlags2KHR.Access2ShaderWriteBitKhr,
           //     oldLayout: ImageLayout.TransferDstOptimal, newLayout: ImageLayout.General,
           //     srcQueueFamilyIndex: Vk.QueueFamilyIgnored, dstQueueFamilyIndex: Vk.QueueFamilyIgnored,
           //     image: _targetImages.Span[imageIndex], subresourceRange: new ImageSubresourceRange(ImageAspectFlags.ImageAspectColorBit, 0, 1, 0, 1));
           // _khrSynchronization2.CmdPipelineBarrier2(commandBuffer, new DependencyInfoKHR(imageMemoryBarrierCount: 1,
           //     pImageMemoryBarriers: &memoryBarrier2));

           _vk.CmdPipelineBarrier(commandBuffer, PipelineStageFlags.PipelineStageTransferBit,
               PipelineStageFlags.PipelineStageComputeShaderBit, 0, 0, null, 0, null, 1,
               new ImageMemoryBarrier(srcAccessMask: AccessFlags.AccessTransferWriteBit,
                   dstAccessMask: AccessFlags.AccessShaderReadBit | AccessFlags.AccessShaderWriteBit, oldLayout: ImageLayout.TransferDstOptimal,
                   newLayout: ImageLayout.General, srcQueueFamilyIndex: Vk.QueueFamilyIgnored,
                   dstQueueFamilyIndex: Vk.QueueFamilyIgnored, image: _targetImages.Span[imageIndex],
                   subresourceRange: new ImageSubresourceRange(ImageAspectFlags.ImageAspectColorBit, 0, 1, 0, 1)));
       
           Span<uint> pValues = stackalloc uint[]
           {
               _targetSize.X, _targetSize.Y
           }; 
           _vk.CmdPushConstants(commandBuffer, _pipelineLayout, ShaderStageFlags.ShaderStageComputeBit, 0, (uint) pValues.Length * sizeof(uint), pValues);
           _vk.CmdBindDescriptorSets(commandBuffer, PipelineBindPoint.Compute, _pipelineLayout, 0, 1,
               _targetDescriptorSets[imageIndex], 0, null);
           _computeShader.Record(renderContext, commandBuffer);

               // var memoryBarrier3 = new ImageMemoryBarrier2KHR(srcStageMask: PipelineStageFlags2KHR.PipelineStage2ComputeShaderBitKhr,
           //      srcAccessMask: AccessFlags2KHR.Access2ShaderReadBitKhr | AccessFlags2KHR.Access2ShaderWriteBitKhr, dstStageMask: PipelineStageFlags2KHR.PipelineStage2BottomOfPipeBitKhr,
           //      dstAccessMask: AccessFlags2KHR.Access2NoneKhr,
           //      oldLayout: ImageLayout.General, newLayout: ImageLayout.PresentSrcKhr,
           //      srcQueueFamilyIndex: Vk.QueueFamilyIgnored, dstQueueFamilyIndex: Vk.QueueFamilyIgnored,
           //      image: _targetImages.Span[imageIndex], subresourceRange: new ImageSubresourceRange(ImageAspectFlags.ImageAspectColorBit, 0, 1, 0, 1));
           //  _khrSynchronization2.CmdPipelineBarrier2(commandBuffer, new DependencyInfoKHR(imageMemoryBarrierCount: 1,
           //      pImageMemoryBarriers: &memoryBarrier3));

           _vk.CmdPipelineBarrier(commandBuffer, PipelineStageFlags.PipelineStageComputeShaderBit,
               PipelineStageFlags.PipelineStageBottomOfPipeBit, 0, 0, null, 0, null, 1,
               new ImageMemoryBarrier(srcAccessMask: AccessFlags.AccessShaderReadBit | AccessFlags.AccessShaderWriteBit,
                   dstAccessMask: AccessFlags.AccessNoneKhr, oldLayout: ImageLayout.General,
                   newLayout: ImageLayout.PresentSrcKhr, srcQueueFamilyIndex: Vk.QueueFamilyIgnored,
                   dstQueueFamilyIndex: Vk.QueueFamilyIgnored, image: _targetImages.Span[imageIndex],
                   subresourceRange: new ImageSubresourceRange(ImageAspectFlags.ImageAspectColorBit, 0, 1, 0, 1)));
            
            _vk.EndCommandBuffer(commandBuffer);

            var waitDstStageMask = PipelineStageFlags.PipelineStageTransferBit;
            _vk.QueueSubmit(Queue, 1,
                new SubmitInfo(waitSemaphoreCount: 1, pWaitSemaphores: &waitSemaphore, commandBufferCount: 1,
                    pCommandBuffers: &commandBuffer, signalSemaphoreCount: 1, pSignalSemaphores: &signalSemaphore,
                    pWaitDstStageMask: &waitDstStageMask), signal).ThrowCode();
        }

        public unsafe void Dispose()
        {
            _computeShader.Dispose();

            foreach (var v in _commandBuffers)
                _vk.DestroyBuffer(_device, v, null);
            foreach(var v in _commandAllocs)
                v.Dispose();
            
        }
    }
}
