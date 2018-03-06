using System;
using System.Collections.Generic;
using System.Numerics;
using md.stdl.Mathematics;
using Matrix4x4 = System.Numerics.Matrix4x4;

namespace Notui
{
    /// <summary>
    /// Enum flags to selectively apply transform components
    /// </summary>
    [Flags]
    public enum ApplyTransformMode
    {
        /// <summary>
        /// No transformation will be updated
        /// </summary>
        None = 0x0,

        /// <summary>
        /// All components should be updated
        /// </summary>
        All = 0xFF,

        /// <summary>
        /// Translation should be updated
        /// </summary>
        Translation = 0x1,

        /// <summary>
        /// Rotation should be updated
        /// </summary>
        Rotation = 0x2,

        /// <summary>
        /// Scale should be updated
        /// </summary>
        Scale = 0x4
    }

    public class ElementTransformation : ICloneable<ElementTransformation>, IUpdateable<ElementTransformation>
    {
        /// <summary>
        /// Position of the element relative to its parent possibly in 3D world
        /// </summary>
        public Vector3 Position
        {
            get => _position;
            set
            {
                _position = value;
                InvokeChange();
                InvalidateCache();
            }
        }
        public Vector3 PosVelocity { get; set; }

        /// <summary>
        /// Scale of the element relative to its parent possibly in 3D world
        /// </summary>
        public Vector3 Scale
        {
            get => _scale;
            set
            {
                _scale = value;
                InvokeChange();
                InvalidateCache();
            }
        }

        /// <summary>
        /// Rotation of the element relative to its parent possibly in 3D world
        /// </summary>
        public Quaternion Rotation
        {
            get => _rotation;
            set
            {
                _rotation = value;
                InvokeChange();
                InvalidateCache();
            }
        }

        public ElementTransformation()
        {
            Position = Vector3.Zero;
            Scale = Vector3.One;
            Rotation = Quaternion.Identity;
            _matrix = Matrix4x4.Identity;
            Cached = true;
        }
        
        public void UpdateFrom(ElementTransformation other)
        {
            Position = other.Position;
            Rotation = other.Rotation;
            Scale = other.Scale;
        }

        public void UpdateFrom(ElementTransformation other, ApplyTransformMode selective)
        {
            if (((byte)selective & 0x1) != 0x0) Position = other.Position;
            if (((byte)selective & 0x2) != 0x0) Rotation = other.Rotation;
            if (((byte)selective & 0x4) != 0x0) Scale = other.Scale;
        }

        /// <summary>
        /// Follow reference transform with inertial filtering on position and lowpass on rest
        /// </summary>
        /// <param name="reference">The transformation to follow</param>
        /// <param name="force">The maximum force to use</param>
        /// <param name="alpha">Alpha component of the lowpass filters</param>
        /// <param name="deltaT">Delta time of a hypothetical frame in seconds</param>
        public void FollowWithInertia(ElementTransformation reference, float force, float alpha, float deltaT)
        {
            Filters.Inertial(Position, PosVelocity, reference.Position, force * deltaT, out var pos, out var vel);
            Position = pos;
            PosVelocity = vel;
            Scale = Filters.Lowpass(Scale, reference.Scale, new Vector3(alpha * deltaT));
            Rotation = Filters.Lowpass(Rotation, reference.Rotation, alpha * deltaT);
        }

        /// <summary>
        /// Follow reference transform with lowpass filtering
        /// </summary>
        /// <param name="reference">The transformation to follow</param>
        /// <param name="alpha">Alpha component of the lowpass filters</param>
        /// <param name="deltaT">Delta time of a hypothetical frame in seconds</param>
        public void FollowWithLowpass(ElementTransformation reference, float alpha, float deltaT)
        {
            Position = Filters.Lowpass(Position, reference.Position, new Vector3(alpha * deltaT));
            Scale = Filters.Lowpass(Scale, reference.Scale, new Vector3(alpha * deltaT));
            Rotation = Filters.Lowpass(Rotation, reference.Rotation, alpha * deltaT);
        }

        /// <summary>
        /// Get the position in viewspace
        /// </summary>
        /// <param name="context">The source context</param>
        public Vector3 GetViewPosition(NotuiContext context)
        {
            return Vector3.Transform(Position, context.View);
        }
        /// <summary>
        /// Get the rotation in viewspace
        /// </summary>
        /// <param name="context">The source context</param>
        public Quaternion GetViewRotation(NotuiContext context)
        {
            return Rotation * Quaternion.Inverse(context.ViewOrientation);
        }
        /// <summary>
        /// Get the matrix in viewspace
        /// </summary>
        /// <param name="context">The source context</param>
        public Matrix4x4 GetViewTransform(NotuiContext context)
        {
            return Matrix * context.View;
        }

        /// <summary>
        /// Since the last request for the matrix the transformation didn't change.
        /// </summary>
        public bool Cached { get; private set; }

        /// <summary>
        /// Recompute matrix on next request
        /// </summary>
        public void InvalidateCache()
        {
            Cached = false;
        }

        /// <summary>
        /// The actual Matrix transformation
        /// </summary>
        public Matrix4x4 Matrix
        {
            get
            {
                if (Cached) return _matrix;
                _matrix = Matrix4x4.CreateScale(Scale) *
                          Matrix4x4.CreateFromQuaternion(Rotation) *
                          Matrix4x4.CreateTranslation(Position);
                Cached = true;
                return _matrix;
            }
        }

        /// <summary>
        /// Notification fired when position, rotation or scale is changed.
        /// </summary>
        /// <param name="id">a unique id of the subscriber</param>
        /// <param name="action">A single argument action to be run where that argument is the sender transformation</param>
        /// <remarks>
        /// This will fire on all assignments at position, rotation or scale. Do not do anything expensive here. This is mainly used to invalidate matrix caches on GuiElements
        /// </remarks>
        public void SubscribeToChange(string id, Action<ElementTransformation> action)
        {
            if (_onChangeActions.ContainsKey(id))
                _onChangeActions[id] = action;
            else _onChangeActions.Add(id, action);
        }

        /// <summary>
        /// Unsubscribe from transformation change
        /// </summary>
        /// <param name="id">a unique id of the subscriber</param>
        public void UnsubscribeFromChange(string id)
        {
            if (_onChangeActions.ContainsKey(id))
                _onChangeActions.Remove(id);
        }

        private void InvokeChange()
        {
            foreach (var action in _onChangeActions.Values)
            {
                action(this);
            }
        }

        private readonly Dictionary<string, Action<ElementTransformation>> _onChangeActions =
            new Dictionary<string, Action<ElementTransformation>>();

        private Matrix4x4 _matrix;
        private Vector3 _position;
        private Vector3 _scale;
        private Quaternion _rotation;

        public object Clone()
        {
            return Copy();
        }

        public ElementTransformation Copy()
        {
            var res = new ElementTransformation();
            res.UpdateFrom(this);
            return res;
        }
    }
}
