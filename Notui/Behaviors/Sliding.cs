using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using md.stdl.Interaction;
using md.stdl.Mathematics;
using static System.Math;

namespace Notui.Behaviors
{
    /// <inheritdoc />
    /// <summary>
    /// Specifying a behavior where the element can be dragged, rotated and scaled freely or within constraints
    /// </summary>
    public class SlidingBehavior : InteractionBehavior
    {
        public enum SelectedPlane
        {
            /// <summary>
            /// The plane parallel to the view and offset by the element's center position.
            /// </summary>
            ViewAligned,

            /// <summary>
            /// The plane defined by the elements DisplayMatrix
            /// </summary>
            OwnPlane,

            /// <summary>
            /// If exists the plane defined by the DisplayMatrix of the element's parent. Otherwise use ViewAligned
            /// </summary>
            ParentPlane
        }

        public class BehaviorState : AuxiliaryObject
        {
            public Vector2 DeltaPos = Vector2.Zero;
            public float DeltaAngle = 0;
            public float DeltaSize = 0;

            public float TotalAngle = 0;

            public override AuxiliaryObject Copy()
            {
                return new BehaviorState
                {
                    DeltaPos = DeltaPos,
                    DeltaAngle = DeltaAngle,
                    DeltaSize = DeltaSize,
                    TotalAngle = TotalAngle
                };
            }

            public override void UpdateFrom(AuxiliaryObject other)
            {
                if (!(other is BehaviorState bs)) return;
                DeltaPos = bs.DeltaPos;
                DeltaAngle = bs.DeltaAngle;
                DeltaSize = bs.DeltaSize;
                TotalAngle = bs.TotalAngle;
            }
        }

        /// <summary>
        /// Can the element be dragged in the parent context?
        /// </summary>
        [BehaviorParameter]
        public bool Draggable { get; set; } = true;

        /// <summary>
        /// Dragging sensitivity per axis.
        /// </summary>
        /// <remarks>
        /// 0 on an axis means that axis is locked, below 0 means axis movement is reversed
        /// </remarks>
        [BehaviorParameter]
        public Vector2 DraggingCoeffitient { get; set; } = new Vector2(1.0f);

        /// <summary>
        /// Can the user scale this element?
        /// </summary>
        [BehaviorParameter]
        public bool Scalable { get; set; } = false;

        /// <summary>
        /// Scaling sensitivity
        /// </summary>
        /// <remarks>
        /// 0 means that scaling is locked (same as Scalable = false), below 0 means scaling movement is reversed
        /// </remarks>
        [BehaviorParameter]
        public float ScalingCoeffitient { get; set; } = 1.0f;

        /// <summary>
        /// Can the user rotate this element?
        /// </summary>
        [BehaviorParameter]
        public bool Pivotable { get; set; } = false;

        /// <summary>
        /// Rotation sensitivity
        /// </summary>
        /// <remarks>
        /// 0 means that rotation is locked (same as Pivotable = false), below 0 means rotation movement is reversed
        /// </remarks>
        [BehaviorParameter]
        public float RotationCoeffitient { get; set; } = 1.0f;

        /// <summary>
        /// Slide element in the selected plane.
        /// </summary>
        /// <remarks>
        /// If this is true then constraints will be also relative to the element's parent, instead of the world space.
        /// </remarks>
        [BehaviorParameter]
        public SelectedPlane SlideInSelectedPlane { get; set; } = SelectedPlane.ViewAligned;

        /// <summary>
        /// Slide when children are hit as well
        /// </summary>
        [BehaviorParameter]
        public bool SlideOnChildrenInteracting { get; set; }

        /// <summary>
        /// Slide only when this amount of touches interacting with the element
        /// </summary>
        [BehaviorParameter(Minimum = 1)]
        public int MinimumTouches { get; set; } = 1;

        /// <summary>
        /// Limit rotation to a minimum and maximum cycles
        /// </summary>
        [BehaviorParameter]
        public bool LimitRotation { get; set; }

        /// <summary>
        /// Limit the dragging to a bounding box
        /// </summary>
        [BehaviorParameter]
        public bool LimitTranslation { get; set; }

        /// <summary>
        /// Minimum and maximum cycles if rotation is limited.
        /// </summary>
        [BehaviorParameter]
        public Vector2 RotationMinMax { get; set; } = new Vector2(-1, 1);

        /// <summary>
        /// Minimum and maximum size of the element
        /// </summary>
        [BehaviorParameter(Minimum = 0)]
        public Vector2 ScaleMinMax { get; set; } = new Vector2(0.1f, 3);

        /// <summary>
        /// Minimum of bounding box in world or parent space
        /// </summary>
        [BehaviorParameter]
        public Vector3 BoundingBoxMin { get; set; } = new Vector3(-1, -1, -1);
        /// <summary>
        /// Maximum of bounding box in world or parent space
        /// </summary>
        [BehaviorParameter]
        public Vector3 BoundingBoxMax { get; set; } = new Vector3(1, 1, 1);

        /// <summary>
        /// After the interaction ended how long the element should continue sliding in seconds
        /// </summary>
        /// <remarks>
        /// While the element is flicking constraints are still applied. SlidingBehavior will attempt to approach constraint borders smoothly.
        /// </remarks>
        [BehaviorParameter(Minimum = 0)]
        public float FlickTime { get; set; } = 0;

        private SelectedPlane _actualPlaneSelection;

        private void Move(NotuiElement element, BehaviorState state, Matrix4x4 usedplane)
        {
            var disptr = element.DisplayTransformation;
            if (Draggable)
            {
                var worldvel = Vector4.Transform(new Vector4(state.DeltaPos * DraggingCoeffitient * 0.5f, 0, 0), usedplane).xyz();
                if (element.Parent != null)
                {
                    Matrix4x4.Invert(element.Parent.DisplayMatrix, out var invparenttr);
                    worldvel = Vector4.Transform(new Vector4(worldvel, 0), invparenttr).xyz();
                }

                disptr.Translate(worldvel);
                if (LimitTranslation)
                    disptr.Position = Intersections.BoxPointLimit(BoundingBoxMin, BoundingBoxMax, disptr.Position);
            }
            if (Scalable)
            {
                var sclvel = state.DeltaSize * ScalingCoeffitient * 0.5f;
                disptr.Scale = Vector3.Max(
                    new Vector3(ScaleMinMax.X),
                    Vector3.Min(
                        new Vector3(ScaleMinMax.Y),
                        disptr.Scale * new Vector3(1 + sclvel)
                    )
                );

            }
            if (Pivotable)
            {
                // see if rotation is still inside boundaries
                var targetrot = state.TotalAngle + state.DeltaAngle * RotationCoeffitient * 0.5f * (1/disptr.Scale.Length());
                if (!LimitRotation || RotationMinMax.X <= targetrot && targetrot <= RotationMinMax.Y)
                {
                    state.TotalAngle = targetrot;

                    var worldaxis = Vector3.TransformNormal(Vector3.UnitZ, usedplane);
                    if (element.Parent != null)
                    {
                        Matrix4x4.Invert(element.Parent.DisplayMatrix, out var invparenttr);
                        worldaxis = Vector3.TransformNormal(worldaxis, invparenttr);
                    }
                    var worldrot = Quaternion.CreateFromAxisAngle(worldaxis, state.DeltaAngle * RotationCoeffitient);
                    disptr.LocalRotate(worldrot);
                }
            }
            element.UpdateFromDisplayToInteraction(element);
        }

        private void FlickProgress(BehaviorState state, NotuiContext context)
        {
            if (FlickTime < context.DeltaTime)
            {
                state.DeltaPos = Vector2.Zero;
                state.DeltaAngle = 0;
                state.DeltaSize = 0;
            }
            else
            {
                // TODO: that 6 there is a rough estimation magic number coming from a vague memory of the integral of something something low-pass filter
                // It was years ago, only the 6 part stuck and it was good enough for animation
                var frametime = (6 / FlickTime) * context.DeltaTime;
                state.DeltaPos = Filters.Velocity(state.DeltaPos, Vector2.Zero, frametime * Max(0.001f, state.DeltaPos.Length()));
                state.DeltaAngle = Filters.Velocity(state.DeltaAngle, 0, frametime * Max(0.001f, Abs(state.DeltaAngle)));
                state.DeltaSize = Filters.Velocity(state.DeltaSize, 0, frametime * Max(0.001f, Abs(state.DeltaSize)));
            }
        }

        private void AddChildrenTouches(NotuiElement element, List<TouchContainer<NotuiElement[]>> touches)
        {
            foreach (var child in element.Children.Values)
            {
                touches.AddRange(child.Touching.Keys);
                AddChildrenTouches(child, touches);
            }
        }

        /// <summary>
        /// Calculate deltas from 2 touches and their previous state
        /// </summary>
        /// <param name="curr0">Current first point</param>
        /// <param name="prev0">Previous first point</param>
        /// <param name="curr1">Current second point</param>
        /// <param name="prev1">Previos second point</param>
        /// <returns>Delta XY, delta angle, delta scale</returns>
        private Vector4 CalcDeltaFromTwoTouch(Vector2 curr0, Vector2 prev0, Vector2 curr1, Vector2 prev1)
        {
            var currpolar = Coordinates.RectToPolar(curr0 - curr1);
            var prevpolar = Coordinates.RectToPolar(prev0 - prev1);
            var curravg = (curr0 + curr1) / 2;
            var prevavg = (prev0 + prev1) / 2;
            
            return new Vector4(curravg - prevavg, currpolar.X - prevpolar.X, currpolar.Y - prevpolar.Y);
        }

        public override void Behave(NotuiElement element)
        {
            // Select the plane of interaction
            _actualPlaneSelection = SlideInSelectedPlane == SelectedPlane.ParentPlane ?
                (element.Parent != null ? SelectedPlane.ParentPlane : SelectedPlane.ViewAligned) :
                SlideInSelectedPlane;

            Matrix4x4 usedplane;
            switch (_actualPlaneSelection)
            {
                case SelectedPlane.ParentPlane:
                    usedplane = element.Parent.DisplayMatrix;
                    break;
                case SelectedPlane.OwnPlane:
                    usedplane = element.DisplayMatrix;
                    break;
                case SelectedPlane.ViewAligned:
                    usedplane = Matrix4x4.CreateFromQuaternion(element.Context.ViewOrientation) *
                                Matrix4x4.CreateTranslation(element.DisplayMatrix.Translation);
                    break;
                default:
                    // same as above
                    usedplane = Matrix4x4.CreateFromQuaternion(element.Context.ViewOrientation) *
                                Matrix4x4.CreateTranslation(element.DisplayMatrix.Translation);
                    break;
            }
            
            // Get state from element
            var hasstate = IsStateAvailable(element);
            var currstate = hasstate ? GetState<BehaviorState>(element) : new BehaviorState();

            // Merge touches from children if SlideOnChildrenInteracting is true
            var touches = element.Touching.Keys.ToList();
            if(SlideOnChildrenInteracting)
                AddChildrenTouches(element, touches);

            // Do interaction if there are minimum specified touches
            if(touches.Count >= MinimumTouches)
            {
                // if Draggable is true and there's only 1 touch do a simple translation move only
                if (Draggable && touches.Count <= 1)
                {
                    var relvel = touches.First().GetPlanarVelocity(usedplane, element.Context,
                        out var crelpos, out var prelpos);
                    currstate.DeltaPos = relvel.xy();

                    // reset delta size and angle 
                    currstate.DeltaAngle = 0;
                    currstate.DeltaSize = 0;

                    // Finalize
                    Move(element, currstate, usedplane);
                    FlickProgress(currstate, element.Context);
                    SetState(element, currstate);
                    return;
                }
                
                // If Draggable is off but Pivotable or Scalable is still on do a rotation around the element center
                if (!Draggable && touches.Count <= 1)
                {
                    touches.First().GetPlanarVelocity(usedplane, element.Context,
                        out var crelpos, out var prelpos);
                    var deltarn = Vector4.Zero;

                    if (crelpos.xy().Length() > 0.00001)
                        deltarn = CalcDeltaFromTwoTouch(crelpos.xy(), prelpos.xy(), crelpos.xy() * -1, prelpos.xy() * -1);

                    // reset translation delta
                    currstate.DeltaPos = Vector2.Zero;

                    currstate.DeltaAngle = deltarn.Z;
                    currstate.DeltaSize = deltarn.W;

                    // Finalize
                    Move(element, currstate, usedplane);
                    FlickProgress(currstate, element.Context);
                    SetState(element, currstate);
                    return;
                }

                // If there are more than one touches see which 2 moves the fastest and use those for scaling/moving
                var orderedbyfastest = touches.OrderByDescending(t => t.Velocity.LengthSquared()).ToArray();
                var t0 = orderedbyfastest[0];
                var t1 = orderedbyfastest[1];

                t0.GetPlanarVelocity(usedplane, element.Context, out var cp0, out var pp0);
                t1.GetPlanarVelocity(usedplane, element.Context, out var cp1, out var pp1);
                var delta = CalcDeltaFromTwoTouch(cp0.xy(), pp0.xy(), cp1.xy(), pp1.xy());

                currstate.DeltaPos = delta.xy();
                currstate.DeltaAngle = delta.Z;
                currstate.DeltaSize = delta.W;
            }
            // Finalize
            Move(element, currstate, usedplane);
            FlickProgress(currstate, element.Context);
            SetState(element, currstate);
        }
    }
}
