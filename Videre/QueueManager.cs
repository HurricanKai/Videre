using System;
using System.Collections.Generic;
using System.Linq;
using Silk.NET.Vulkan;
using Silk.NET.Vulkan.Extensions.KHR;

namespace Videre
{
    public sealed class QueueManager
    {
        private sealed record QueueInfo(uint Index, Queue Queue, bool SupportsTransfer, bool SupportsCompute, bool SupportsGraphics, bool SupportsPresent);

        private readonly Vk _vk;
        private readonly KhrSurface _khrSurface;
        private readonly Instance _instance;
        private readonly PhysicalDevice _physicalDevice;
        private readonly Device _device;
        private readonly SurfaceKHR _surface;
        private readonly QueueInfo[] _queues;

        public int QueueCount => _queues.Length;

        public QueueManager(
            Vk vk,
            KhrSurface khrSurface,
            Instance instance,
            PhysicalDevice physicalDevice,
            Device logicalDevice,
            SurfaceKHR surface,
            Span<QueueFamilyProperties> span)
        {
            _vk = vk;
            _khrSurface = khrSurface;
            _instance = instance;
            _physicalDevice = physicalDevice;
            _device = logicalDevice;
            _surface = surface;

            var queues = new List<QueueInfo>();

            for (uint i = 0; i < span.Length; i++)
            {
                var queueFamilyProperties = span[(int)i];
                var supportsGraphics = (queueFamilyProperties.QueueFlags & QueueFlags.QueueGraphicsBit) != 0;
                var supportsCompute = (queueFamilyProperties.QueueFlags & QueueFlags.QueueComputeBit) != 0;
                var supportsTransfer = supportsGraphics || supportsCompute ||
                                       (queueFamilyProperties.QueueFlags & QueueFlags.QueueTransferBit) != 0;
                
                _khrSurface.GetPhysicalDeviceSurfaceSupport(physicalDevice, i, _surface, out var presentSupported).ThrowCode();

                for (uint j = 0; j < queueFamilyProperties.QueueCount; j++)
                {
                    _vk.GetDeviceQueue(_device, i, j, out var queue);
                    queues.Add(new QueueInfo(i, queue, supportsTransfer, supportsCompute, supportsGraphics, presentSupported));
                }
            }

            _queues = queues.ToArray();
            Console.WriteLine($"Found {_queues.Length} queues");
        }

        public (uint queueFamilyIndex, Queue queue) GetQueue(
            bool needsTransfer,
            bool needsCompute,
            bool needsGraphics,
            bool needsPresent)
        {
            QueueInfo bestQueue = null;
            int score = 10000;
            foreach (var v in _queues)
            {
                int i = 0;
                if (needsTransfer)
                {
                    if (!v.SupportsTransfer)
                    {
                        continue;
                    }
                }
                else if (v.SupportsTransfer)
                    i++;
                
                if (needsCompute)
                {
                    if (!v.SupportsCompute)
                    {
                        continue;
                    }
                }
                else if (v.SupportsCompute)
                    i++;
                
                if (needsGraphics)
                {
                    if (!v.SupportsGraphics)
                    {
                        continue;
                    }
                }
                else if (v.SupportsGraphics)
                    i++;
                
                if (needsPresent)
                {
                    if (!v.SupportsPresent)
                    {
                        continue;
                    }
                }
                else if (v.SupportsPresent)
                    i++;

                if (i < score)
                {
                    bestQueue = v;
                    score = i;
                }
            }

            return (bestQueue.Index, bestQueue.Queue);
        }
    }
}
