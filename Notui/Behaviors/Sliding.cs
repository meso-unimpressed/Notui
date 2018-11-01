using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Reactive.Linq;
using System.Runtime.Remoting.Channels;
using System.Text;
using System.Threading.Tasks;
using md.stdl.Interaction;
using md.stdl.Interfaces;
using md.stdl.Mathematics;
using md.stdl.Time;
using static System.Math;

namespace Notui.Behaviors
{
    /// <inheritdoc />
    /// <summary>
    /// Specifying a behavior where the element can be dragged, rotated and scaled freely or within constraints
    /// </summary>
    public class SlidingBehavior : PlanarBehavior
    {

        /// <summary>
        /// Stateful data for MoveToTopOnTouchBehavior behavior
        /// </summary>
        public class BehaviorState : AuxiliaryObject, IMainlooping
        {
            /// <summary>
            /// The delta position between frames
            /// </summary>
            public Vector2 DeltaPos = Vector2.Zero;

            /// <summary>
            /// The delta angle between frames
            /// </summary>
            public float DeltaAngle = 0;

            /// <summary>
            /// The delta size between frames
            /// </summary>
            public float DeltaSize = 0;
            
            /// <summary>
            /// The total angle this Sliding behavior inflicted onto the assigned element
            /// </summary>
            public float TotalAngle = 0;

            /// <summary>
            /// Is it currently flicking
            /// </summary>
            public bool Flicking = false;

            /// <summary>
            /// Delayed deltas for more natural determination of flicking inertia
            /// </summary>
            public Delay<Vector4> DelayedDeltas = new Delay<Vector4>(TimeSpan.FromSeconds(1.0));

            /// <inheritdoc cref="AuxiliaryObject"/>
            public override IAuxiliaryObject Copy()
            {
                return new BehaviorState
                {
                    DeltaPos = DeltaPos,
                    DeltaAngle = DeltaAngle,
                    DeltaSize = DeltaSize,
                    TotalAngle = TotalAngle
                };
            }

            /// <inheritdoc cref="AuxiliaryObject"/>
            public override void UpdateFrom(IAuxiliaryObject other)
            {
                if (!(other is BehaviorState bs)) return;
                DeltaPos = bs.DeltaPos;
                DeltaAngle = bs.DeltaAngle;
                DeltaSize = bs.DeltaSize;
                TotalAngle = bs.TotalAngle;
            }

            /// <inheritdoc cref="IMainlooping"/>
            public void Mainloop(float deltatime)
            {
                OnMainLoopBegin?.Invoke(this, EventArgs.Empty);
                DelayedDeltas.Submit(new Vector4(DeltaPos, DeltaAngle, DeltaSize));
                OnMainLoopEnd?.Invoke(this, EventArgs.Empty);
            }

            /// <inheritdoc cref="IMainlooping"/>
            public event EventHandler OnMainLoopBegin;
            /// <inheritdoc cref="IMainlooping"/>
            public event EventHandler OnMainLoopEnd;
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
        /// While the element is flicking constraints are still applied.
        /// </remarks>
        [BehaviorParameter(Minimum = 0)]
        public float FlickTime { get; set; } = 0;
        
        /// <summary>
        /// The delay amount in seconds at which to sample the velocity for flicking
        /// </summary>
        /// <remarks>
        /// This fixes behavior when touches release at slighty different times
        /// </remarks>
        [BehaviorParameter(Minimum = 0)]
        public float FlickVelocityDelay { get; set; } = 0.08f;

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
            state.Mainloop(element.Context.DeltaTime);
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
                state.DeltaPos = Filters.Damper(state.DeltaPos, Vector2.Zero, FlickTime, context.DeltaTime);
                state.DeltaAngle = Filters.Damper(state.DeltaAngle, 0, FlickTime, context.DeltaTime);
                state.DeltaSize = Filters.Damper(state.DeltaSize, 0, FlickTime, context.DeltaTime);
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

        /// <inheritdoc cref="InteractionBehavior"/>
        public override void Behave(NotuiElement element)
        {
            var usedplane = GetUsedPlane(element);
            
            var currstate = IsStateAvailable(element) ? GetState<BehaviorState>(element) : new BehaviorState();

            var touches = GetBehavingTouches(element);

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
            else
            {
                if (!currstate.Flicking)
                {
                    var deldelta = currstate.DelayedDeltas.GetAt(TimeSpan.FromSeconds(FlickVelocityDelay));
                    currstate.DeltaPos = deldelta.xy();
                    currstate.DeltaAngle = deldelta.Z;
                    currstate.DeltaSize = deldelta.W;
                }
                currstate.Flicking = true;
            }
            // Finalize
            Move(element, currstate, usedplane);
            FlickProgress(currstate, element.Context);
            SetState(element, currstate);
        }
    }
}
