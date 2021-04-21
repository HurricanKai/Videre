// This file is part of Silk.NET.
// 
// You may modify and distribute Silk.NET under the terms
// of the MIT license. See the LICENSE file for details.

using System;
using System.Runtime.InteropServices;
using Silk.NET.Vulkan;

namespace Videre
{
    public sealed class GCAllocationCallbacks : IDisposable
    {
        private void Trace(string s, params object[] objs)
        {
            // Log.Verbose(s, objs);
            // Console.WriteLine(s, objs);
        }
        
        private void Warning(string s, params object[] objs)
        {
            // Log.Warning(s, objs);
            Console.WriteLine(s, objs);
        }
        
        public AllocationCallbacks AllocationCallbacks { get; }
        private GCHandle _selfHandle;

        public unsafe GCAllocationCallbacks()
        {
            _selfHandle = GCHandle.Alloc(this);
            var pfnAllocation = (void*) (delegate* unmanaged<void*, UIntPtr, UIntPtr, SystemAllocationScope, void*>) &Allocation;
            var pfnReallocation = (void*) (delegate* unmanaged<void*, void*, UIntPtr, UIntPtr, SystemAllocationScope, void*>) &Reallocation;
            var pfnFree = (void*) (delegate* unmanaged<void*, void*, void>) &Free;
            var pfnInternalAllocation = (void*) (delegate* unmanaged<void*, UIntPtr, InternalAllocationType, SystemAllocationScope, void>) &InternalAllocationNotification;
            var pfnInternalFree = (void*) (delegate* unmanaged<void*, UIntPtr, InternalAllocationType, SystemAllocationScope, void>) &InternalFreeNotification;
            AllocationCallbacks = new AllocationCallbacks
            (
                pUserData: (void*) GCHandle.ToIntPtr(_selfHandle),
                pfnAllocation: *(PfnAllocationFunction*)(&pfnAllocation),
                pfnReallocation: *(PfnReallocationFunction*)(&pfnReallocation),
                pfnFree: *(PfnFreeFunction*)(&pfnFree),
                pfnInternalAllocation: *(PfnInternalAllocationNotification*)(&pfnInternalAllocation),
                pfnInternalFree: *(PfnInternalFreeNotification*)(&pfnInternalFree)
            );
        }
        
        private static unsafe void* Allocate(nuint size, nuint alignment)
        {
            var arr = GC.AllocateUninitializedArray<byte>((int) (size + alignment - 1) + sizeof(IntPtr) + sizeof(UIntPtr), true);
            var allocatedHandle = GCHandle.Alloc(arr, GCHandleType.Normal);
            var handlePtr = GCHandle.ToIntPtr(allocatedHandle);
            fixed (byte* pArr = arr)
            {
                var address = (nint) (pArr + sizeof(IntPtr) + sizeof(IntPtr));
                address += address % (nint) alignment;
                *((IntPtr*) address - 1) = handlePtr;
                *((UIntPtr*) address - 2) = alignment;

                return (void*) address;
            }
        }

        private static unsafe void Free(void* memory)
        {
            var handle = GCHandle.FromIntPtr(*((IntPtr*) memory - 1));
            handle.Free();
        }

        [UnmanagedCallersOnly]
        private static unsafe void* Allocation
            (void* pUserData, nuint size, nuint alignment, SystemAllocationScope allocationScope)
        {
            var handle = GCHandle.FromIntPtr((IntPtr) pUserData);
            var @this = (GCAllocationCallbacks?) handle.Target;
            if (@this is null)
                return null;

            @this.Trace("Allocation: {0} {1} {2}", size, alignment, allocationScope);
            return Allocate(size, alignment);
        }

        [UnmanagedCallersOnly]
        private static unsafe void* Reallocation(void* pUserData, void* pOriginal, nuint size, nuint alignment,
            SystemAllocationScope allocationScope)
        {
            bool isAllocation = pOriginal is null;
            bool isFree = size == 0;
            var handle = GCHandle.FromIntPtr((IntPtr) pUserData);
            var @this = (GCAllocationCallbacks?)handle.Target;
            if (@this is null)
                return null;

            if (isAllocation)
            {
               @this.Trace
                    ("Reallocation as Allocation: {0} {1} {2}", size, alignment, allocationScope);
                return Allocate(size, alignment);
            }

            if (isFree)
            {
               @this.Trace("Reallocation as Free: {0} {1}", alignment, allocationScope);
                Free(pOriginal);
                return null;
            }

            @this.Trace("Reallocation: {0} {1} {2}", size, alignment, allocationScope);
            var oldAlignment = *((UIntPtr*) pOriginal - 2);
            if (alignment != oldAlignment)
                @this.Warning("Old alignment was unequal to new alignment, the spec forbids this. ({old}, {new})", oldAlignment, alignment);

            var newMem = Allocate(size, alignment);
            var oldHandle = GCHandle.FromIntPtr(*((IntPtr*) pOriginal - 1));
            var oldSize = ((byte[]) oldHandle.Target!).Length;
            var oldSpan = new Span<byte>(pOriginal, (int) oldSize);
            var newSpan = new Span<byte>(newMem, (int) size);
            if (size < (ulong) oldSize)
                oldSpan.Slice(0, (int) size).CopyTo(newSpan);
            else
                oldSpan.CopyTo(newSpan);
            oldHandle.Free();
            
            return newMem;
        }

        [UnmanagedCallersOnly]
        private static unsafe void Free(void* pUserData, void* pMemory)
        {
            if (pMemory is null)
                return;
            
            var handle = GCHandle.FromIntPtr((IntPtr) pUserData);
            var @this = (GCAllocationCallbacks?)handle.Target;
            if (@this is null)
                return;

            Free(pMemory);
            
           @this.Trace("Free");
        }

        [UnmanagedCallersOnly]
        private static unsafe void InternalAllocationNotification
            (void* pUserData, nuint size, InternalAllocationType allocationType, SystemAllocationScope allocationScope)
        {
            Console.WriteLine("Internal Allocation!");
            
            var handle = GCHandle.FromIntPtr((IntPtr) pUserData);
            var @this = (GCAllocationCallbacks?)handle.Target;
            if (@this is null)
                return;
            
            @this.Trace("Internal Allocation: {0} {1}", allocationType, allocationScope);
            GC.AddMemoryPressure((long) size);
        }

        [UnmanagedCallersOnly]
        private static unsafe void InternalFreeNotification
            (void* pUserData, nuint size, InternalAllocationType allocationType, SystemAllocationScope allocationScope)
        {
            Console.WriteLine("Internal Free!");
            
            var handle = GCHandle.FromIntPtr((IntPtr) pUserData);
            var @this = (GCAllocationCallbacks?)handle.Target;
            if (@this is null)
                return;
            
            @this.Trace("Internal Free: {0} {1}", allocationType, allocationScope);
            GC.RemoveMemoryPressure((long) size);
        }

        public void Dispose()
        {
            _selfHandle.Free();
        }
    }
}
