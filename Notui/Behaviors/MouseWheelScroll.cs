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
    public class MouseWheelScroll : PlanarBehavior
    {

        public class BehaviorState : AuxiliaryObject
        {
            public Vector2 DeltaPos = Vector2.Zero;

            public override AuxiliaryObject Copy()
            {
                return new BehaviorState
                {
                    DeltaPos = DeltaPos
                };
            }

            public override void UpdateFrom(AuxiliaryObject other)
            {
                if (!(other is BehaviorState bs)) return;
                DeltaPos = bs.DeltaPos;
            }
        }

        /// <summary>
        /// Dragging sensitivity per axis.
        /// </summary>
        /// <remarks>
        /// 0 on an axis means that axis is locked, below 0 means axis movement is reversed
        /// </remarks>
        [BehaviorParameter]
        public Vector2 Coeffitient { get; set; } = new Vector2(1.0f);

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

        private void Move(NotuiElement element, BehaviorState state, Matrix4x4 usedplane)
        {
            var disptr = element.DisplayTransformation;
            var worldvel = Vector4.Transform(new Vector4(state.DeltaPos * Coeffitient * 0.5f, 0, 0), usedplane).xyz();
            if (element.Parent != null)
            {
                Matrix4x4.Invert(element.Parent.DisplayMatrix, out var invparenttr);
                worldvel = Vector4.Transform(new Vector4(worldvel, 0), invparenttr).xyz();
            }

            disptr.Translate(worldvel);
            disptr.Position = Intersections.BoxPointLimit(BoundingBoxMin, BoundingBoxMax, disptr.Position);
        }

        private void FlickProgress(BehaviorState state, NotuiContext context)
        {
            if (FlickTime < context.DeltaTime)
            {
                state.DeltaPos = Vector2.Zero;
            }
            else
            {
                // TODO: that 6 there is a rough estimation magic number coming from a vague memory of the integral of something something low-pass filter
                // It was years ago, only the 6 part stuck and it was good enough for animation
                var frametime = (6 / FlickTime) * context.DeltaTime;
                state.DeltaPos = Filters.Velocity(state.DeltaPos, Vector2.Zero, frametime * Max(0.001f, state.DeltaPos.Length()));
            }
        }

        public override void Behave(NotuiElement element)
        {
            var usedplane = GetUsedPlane(element);

            var currstate = IsStateAvailable(element) ? GetState<BehaviorState>(element) : new BehaviorState();

            var touches = GetBehavingTouches(element, InteractingTouchSource.Mice);
            
            if (touches.Count >= 1)
            {
                var allscroll = touches.Aggregate(Vector2.Zero, (v, touch) => v + new Vector2(
                    (float)touch.MouseDelta.AccumulatedHorizontalWheelDelta / 120,
                    (float)touch.MouseDelta.AccumulatedWheelDelta / 120)
                );
                if(allscroll.Length() > 0)
                    currstate.DeltaPos = allscroll;
            }
            Move(element, currstate, usedplane);
            FlickProgress(currstate, element.Context);
            SetState(element, currstate);
        }
    }
}
