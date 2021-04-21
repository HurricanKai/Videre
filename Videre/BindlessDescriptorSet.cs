using System;
using System.Runtime.CompilerServices;
using Silk.NET.Vulkan;
using Buffer = Silk.NET.Vulkan.Buffer;

namespace Videre
{
    public sealed unsafe class BindlessDescriptorSet : IDisposable
    {
        private sealed class DescriptorSetInfo
        {
            public DescriptorSetInfo(DescriptorSet descriptorSet)
            {
                DescriptorSet = descriptorSet;
            }
            public DescriptorSet DescriptorSet { get; }
            public int SampledImageIndex { get; set; }
            public int StorageImageIndex { get; set; }
            public int SamplerIndex { get; set; }

            public void Reset()
            {
                SampledImageIndex = 0;
                StorageImageIndex = 0;
                SamplerIndex = 0;
            }
        }
        
        private readonly Vk _vk;
        private readonly Device _device;
        private readonly int _pushConstantSize;
        private readonly int _numSampledImages;
        private readonly int _numStorageImages;
        private readonly int _numSamplers;
        private DescriptorPool _descriptorPool;
        private DescriptorSetInfo[] _descriptorSets;
        private bool _isInitialized = false;
        private readonly DescriptorSetLayout _descriptorSetLayout;
        private readonly PipelineLayout _pipelineLayout;

        public PipelineLayout PipelineLayout => _pipelineLayout;

        private const int SampledImageBinding = 0;
        private const int StorageImageBinding = 1;
        private const int SamplerBinding = 2;

        public BindlessDescriptorSet(Vk vk, Device device, int pushConstantSize, int numSampledImages = 512 * 1024, int numStorageImages = 64 * 1024, int numSamplers = 4 * 1024)
        {
            _vk = vk;
            _device = device;
            _pushConstantSize = pushConstantSize;
            _numSampledImages = numSampledImages;
            _numStorageImages = numStorageImages;
            _numSamplers = numSamplers;

            var pBindings = stackalloc DescriptorSetLayoutBinding[]
            {
                new DescriptorSetLayoutBinding(SampledImageBinding, DescriptorType.SampledImage, (uint) numSampledImages, ShaderStageFlags.ShaderStageAll),
                new DescriptorSetLayoutBinding(StorageImageBinding, DescriptorType.StorageImage, (uint) numStorageImages, ShaderStageFlags.ShaderStageAll),
                new DescriptorSetLayoutBinding(SamplerBinding, DescriptorType.Sampler, (uint) numSamplers, ShaderStageFlags.ShaderStageAll),
            };
            _vk.CreateDescriptorSetLayout(_device, new DescriptorSetLayoutCreateInfo(flags: DescriptorSetLayoutCreateFlags.DescriptorSetLayoutCreateUpdateAfterBindPoolBit,
                bindingCount: 3, pBindings: pBindings), null, out var layout).ThrowCode();
            _descriptorSetLayout = layout;

            if (pushConstantSize > 0)
            {
                var pushConstantRange =
                    new PushConstantRange(ShaderStageFlags.ShaderStageAll, 0, (uint) pushConstantSize);
                _vk.CreatePipelineLayout(_device,
                    new PipelineLayoutCreateInfo(setLayoutCount: 1, pSetLayouts: &layout, pushConstantRangeCount: 1,
                        pPushConstantRanges: &pushConstantRange), null, out _pipelineLayout).ThrowCode();
            }
            else
            {
                _vk.CreatePipelineLayout(_device,
                    new PipelineLayoutCreateInfo(setLayoutCount: 1, pSetLayouts: &layout, pushConstantRangeCount: 0,
                        pPushConstantRanges: null), null, out _pipelineLayout).ThrowCode();
            }
        }

        public void ChangeNumberOfFrames(int newFrameCount)
        {
            if (_isInitialized)
                DestroyFrameResources();

            var pPoolSizes = stackalloc DescriptorPoolSize[]
            {
                new DescriptorPoolSize(DescriptorType.SampledImage, (uint) (newFrameCount * _numSampledImages)),
                new DescriptorPoolSize(DescriptorType.StorageImage, (uint) (newFrameCount * _numStorageImages)),
                new DescriptorPoolSize(DescriptorType.Sampler, (uint) (newFrameCount * _numSamplers)),
            };
            _vk.CreateDescriptorPool(_device, new DescriptorPoolCreateInfo(flags: DescriptorPoolCreateFlags.DescriptorPoolCreateUpdateAfterBindBit,
                    maxSets: (uint) newFrameCount, poolSizeCount: 3, pPoolSizes: pPoolSizes),
                null, out _descriptorPool).ThrowCode();
            
            _descriptorSets = new DescriptorSetInfo[newFrameCount];

            var pSetLayouts = stackalloc DescriptorSetLayout[newFrameCount];
            for (int i = 0; i < newFrameCount; i++)
                pSetLayouts[i] = _descriptorSetLayout;

            var descriptorSets = stackalloc DescriptorSet[newFrameCount];
            
            _vk.AllocateDescriptorSets(_device,  new DescriptorSetAllocateInfo(descriptorPool: _descriptorPool,
                descriptorSetCount: (uint) newFrameCount, pSetLayouts: pSetLayouts), descriptorSets);

            for (int i = 0; i < newFrameCount; i++)
                _descriptorSets[i] = new DescriptorSetInfo(descriptorSets[i]);
            
            _isInitialized = true;
        }

        public DescriptorSet GetDescriptorSet(int frame)
        {
            return _descriptorSets[frame].DescriptorSet;
        }

        private void DestroyFrameResources()
        {
            _vk.DestroyDescriptorPool(_device, _descriptorPool, null);
        }

        public void Dispose()
        {
            _vk.DestroyDescriptorSetLayout(_device, _descriptorSetLayout, null);
            if (_isInitialized)
            {
                DestroyFrameResources();
            }
        }

        public void Bind(CommandBuffer commandBuffer, PipelineBindPoint pipelineBindPoint, int frame)
        {
            _vk.CmdBindDescriptorSets(commandBuffer, pipelineBindPoint, _pipelineLayout, 0, 1, _descriptorSets[frame].DescriptorSet, 0, null);
        }

        public void PushConstants<T>(CommandBuffer commandBuffer, Span<T> values) where T : unmanaged
        {
            _vk.CmdPushConstants(commandBuffer, _pipelineLayout, ShaderStageFlags.ShaderStageAll, 0, (uint) _pushConstantSize, values);
        }

        public int BindStorageImage(in DescriptorImageInfo bufferInfo, int frame)
        {
            var index = _descriptorSets[frame].StorageImageIndex++;
            if (index >= _numStorageImages)
                throw new Exception("Out of storage buffer descriptors");
            
            _vk.UpdateDescriptorSets(_device, 1,
                new WriteDescriptorSet(dstSet: _descriptorSets[frame].DescriptorSet, dstBinding: StorageImageBinding,
                    dstArrayElement: (uint) index, descriptorCount: 1,
                    descriptorType: DescriptorType.StorageImage,
                    pImageInfo: (DescriptorImageInfo*) Unsafe.AsPointer(ref Unsafe.AsRef(in bufferInfo))), 0, null);
            return index;
        }

        public void Reset(int frame)
        {
            _descriptorSets[frame].Reset();
        }
    }
}
