using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Silk.NET.Maths;
using Silk.NET.Vulkan;
using VMASharp;

namespace Videre
{
    public sealed class EngineContext
    {
        public List<uint> Data { get; } = new();
        private int _commandIndex = 0;
        private Matrix3X3<float> _transform = Matrix3X3<float>.Identity;
        private Matrix3X3<float> _lastTransform = Matrix3X3<float>.Identity;
        private Vector2D<float> _scale = Vector2D<float>.One;
        private Vector2D<float> _lastScale = Vector2D<float>.One;

        private static AllocationCreateInfo AllocationCreateInfo(VulkanMemoryPool? pool)
        {
            return new(AllocationCreateFlags.Mapped, 0, MemoryUsage.GPU_Only,
                MemoryPropertyFlags.MemoryPropertyHostVisibleBit, MemoryPropertyFlags.MemoryPropertyHostCoherentBit, 0,
                pool, null);
        }

        internal void PreUpdate()
        {
            _commandIndex = 0;
            Data.Clear();
        }

        internal void PostUpdate()
        {
            Write(Command.End);
        }

        private unsafe void Write(Command command) => Write((uint) command);

        private unsafe void Write(float value) => Write((uint)BitConverter.SingleToInt32Bits(value));
        
        private unsafe void Write(uint value)
        {
            Data.Add(value);
        }

        private void Write(Matrix3X3<float> matrix)
        {
            var m = Unsafe.As<Matrix3X3<float>, Matrix3X3<uint>>(ref matrix);
            Write(m.M11);
            Write(m.M12);
            Write(m.M13);
            Write(m.M21);
            Write(m.M22);
            Write(m.M23);
            Write(m.M31);
            Write(m.M32);
            Write(m.M33);
        }

        private void Write(Vector2D<float> vector)
        {
            Write(vector.X);
            Write(vector.Y);
        }
        
        public void Translation(float x, float y, Action<EngineContext> next)
        {
            var oldTransform = _transform;
            _transform = new Matrix3X3<float>(new Vector3D<float>(1, 0, -x), new Vector3D<float>(0, 1, -y),
                new Vector3D<float>(0, 0, 1)) * _transform;
            next(this);
            _transform = oldTransform;
        }

        public void Rotation(float rotation, Action<EngineContext> next)
        {
            var oldTransform = _transform;
            _transform = Matrix3X3.CreateRotationZ(rotation) * _transform;
            next(this);
            _transform = oldTransform;
        }

        public void Scale(float x, float y, Action<EngineContext> next)
        {
            var oldScale = _scale;
            _scale *= new Vector2D<float>(x, y);
            next(this);
            _scale = oldScale;
        }

        public void Union(Action<EngineContext> a, Action<EngineContext> b)
        {
            a(this);
            b(this);
            Write(Command.Union);
        }

        public void UnionMany(params Action<EngineContext>[] actions) => UnionManyCore(0, actions);

        private void UnionManyCore(int offset, Action<EngineContext>[] actions)
        {
            switch (actions.Length - offset)
            {
                case 0:
                    return;
                case 1:
                    actions[offset](this);
                    return;
                case 2:
                    Union(actions[offset], actions[offset + 1]);
                    return;
                default:
                    Union(actions[offset], x => x.UnionManyCore(offset + 1, actions));
                    return;
            }
        }

        public void Subtraction(Action<EngineContext> a, Action<EngineContext> b)
        {
            a(this);
            b(this);
            Write(Command.Subtraction);
        }

        public void Intersection(Action<EngineContext> a, Action<EngineContext> b)
        {
            a(this);
            b(this);
            Write(Command.Intersection);
        }
        
        public void SmoothUnion(float radius, Action<EngineContext> a, Action<EngineContext> b)
        {
            a(this);
            b(this);
            Write(Command.SmoothUnion);
            Write(radius);
        }

        public void SmoothSubtraction(float radius, Action<EngineContext> a, Action<EngineContext> b)
        {
            a(this);
            b(this);
            Write(Command.SmoothSubtraction);
            Write(radius);
        }

        public void SmoothIntersection(float radius, Action<EngineContext> a, Action<EngineContext> b)
        {
            a(this);
            b(this);
            Write(Command.SmoothIntersection);
            Write(radius);
        }

        public void Round(float radius, Action<EngineContext> next)
        {
            next(this);
            Write(Command.Round);
            Write(radius);
        }
        
        public void Annular(float radius, Action<EngineContext> next)
        {
            next(this);
            Write(Command.Annular);
            Write(radius);
        }

        private void WriteShapeData()
        {
            if (_lastTransform != _transform)
            {
                Write(Command.Transform);
                Write(_transform);
                _lastTransform = _transform;
            }

            if (_lastScale != _scale)
            {
                Write(Command.Scale);
                Write(_scale);
                _lastScale = _scale;
            }
        }
        
        public void Circle(float radius)
        {
            WriteShapeData();
            Write(Command.Circle);
            Write(radius);
        }

        public void NoneShape()
        {
            Write(Command.NoneShape);
        }

        public void RoundedBox(Vector2D<float> sideLengths, Vector4D<float> edgeRadius)
        {
            WriteShapeData();
            Write(Command.RoundedBox);
            Write(sideLengths);
            Write(edgeRadius.X);
            Write(edgeRadius.Y);
            Write(edgeRadius.Z);
            Write(edgeRadius.W);
        }

        public void Box(Vector2D<float> sideLength)
        {
            WriteShapeData();
            Write(Command.Box);
            Write(sideLength);
        }

        public void OrientedBox(Vector2D<float> a, Vector2D<float> b, float theta)
        {
            WriteShapeData();
            Write(Command.OrientedBox);
            Write(a);
            Write(b);
            Write(theta);
        }

        public void Segment(Vector2D<float> a, Vector2D<float> b)
        {
            WriteShapeData();
            Write(Command.Segment);
            Write(a);
            Write(b);
        }

        public void Rhombus(Vector2D<float> b)
        {
            WriteShapeData();
            Write(Command.Rhombus);
            Write(b);
        }

        public void Bezier(Vector2D<float> a, Vector2D<float> b, Vector2D<float> c)
        {
            WriteShapeData();
            Write(Command.Bezier);
            Write(a);
            Write(b);
            Write(c);
        }

        public void Polygon(params Vector2D<float>[] points)
        {
            WriteShapeData();
            Write(Command.Polygon);
            Write((uint)points.Length);
            foreach (var t in points)
                Write(t);
        }
        
        private enum Command : uint
        {
            // !!! KEEP IN SYNC WITH SHADER !!!
            End = 0,
            Transform = 1,
            Scale = 2,

            Union = 10,
            Subtraction = 11,
            Intersection = 12,
            SmoothUnion = 13,
            SmoothSubtraction = 14,
            SmoothIntersection = 15,
            Round = 16,
            Annular = 17,
            
            Circle = 20,
            NoneShape = 21,
            RoundedBox = 22,
            Box = 23,
            OrientedBox = 24,
            Segment = 25,
            Rhombus = 26,
            Bezier = 27,
            Polygon = 28
        }
    }
}
