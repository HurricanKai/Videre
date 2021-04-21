using System;
using Silk.NET.Core.Native;
using Silk.NET.Maths;
using Silk.NET.Vulkan;
using Silk.NET.Vulkan.Extensions.KHR;

namespace Videre
{
    public sealed unsafe class ComputeShader : IDisposable
    {
        private readonly Vk _vk;
        private readonly Device _device;
        private readonly Pipeline _pipeline;

        public ComputeShader(Vk vk, Instance instance, Device device, PhysicalDevice physicalDevice, PipelineLayout pipelineLayout, ReadOnlySpan<byte> data, string functionName, SpecializationInfo* specializationInfo)
        {
            var subgroupProperties = new PhysicalDeviceSubgroupProperties(pNext: null);
            var properties = new PhysicalDeviceProperties2(pNext: &subgroupProperties);
            vk.GetPhysicalDeviceProperties2(physicalDevice, &properties);

            var subgroupSize = subgroupProperties.SubgroupSize;
            Console.WriteLine($"Detected subgroup size {subgroupSize}");

            var data2 = new byte[data.Length + 32];
            data.CopyTo(data2);
            _vk = vk;
            _device = device;
            ShaderModule shaderModule;
            fixed (byte* pData = data2)
                _vk.CreateShaderModule(_device, new ShaderModuleCreateInfo(codeSize: (nuint) data.Length, pCode: (uint*) pData),
                    null, out shaderModule).ThrowCode();

            try
            {
                var pName = SilkMarshal.StringToPtr(functionName);
                try
                {
                    PipelineCreateFlags flags = 0;
                    
                    _vk.CreateComputePipelines(_device, default, 1, new ComputePipelineCreateInfo(flags: flags
                        , layout: pipelineLayout,
                        stage: new PipelineShaderStageCreateInfo(stage: ShaderStageFlags.ShaderStageComputeBit,
                            module: shaderModule, pName: (byte*) pName, pSpecializationInfo: specializationInfo)), null, out _pipeline).ThrowCode();
                }
                finally
                {
                    SilkMarshal.FreeString(pName);
                }
            }
            finally
            {
                _vk.DestroyShaderModule(_device, shaderModule, null);
            }
        }
    
        public void Record(RenderContext renderContext, CommandBuffer commandBuffer)
        {
            _vk.CmdBindPipeline(commandBuffer, PipelineBindPoint.Compute, _pipeline);
            _vk.CmdDispatch(commandBuffer, renderContext.TargetSize.X, renderContext.TargetSize.Y, 1);
        }

        public void Dispose()
        {
            _vk.DestroyPipeline(_device, _pipeline, null);
        }
    }
}
