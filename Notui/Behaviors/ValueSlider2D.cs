using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using md.stdl.Coding;
using static System.Math;

namespace Notui.Behaviors
{
    /// <summary>
    /// A behavior which makes a XY slider for the attached values out of the assigned elements
    /// </summary>
    public class ValueSlider2D : InteractionBehavior
    {
        /// <summary>
        /// A State for ValueSlider2D used only for determining when ValueSlider2D was first attached
        /// </summary>
        public class BehaviorState : AuxiliaryObject
        {
            /// <inheritdoc cref="AuxiliaryObject"/>
            public override IAuxiliaryObject Copy()
            {
                return new BehaviorState();
            }

            /// <inheritdoc cref="AuxiliaryObject"/>
            public override void UpdateFrom(IAuxiliaryObject other) { }
        }

        /// <summary>
        /// Slide speed along the horizontal (X) or the vertical (Y) axis (0 == off)
        /// </summary>
        [BehaviorParameter]
        public Vector2 AxisCoeff { get; set; } = new Vector2(1.0f);

        /// <summary>
        /// Use the surface space to fetch velocity
        /// </summary>
        [BehaviorParameter]
        public bool UseSurfaceSpace { get; set; } = false;

        /// <summary>
        /// Default values at creation
        /// </summary>
        [BehaviorParameter]
        public Vector2 Default { get; set; } = new Vector2(0.0f);

        /// <summary>
        /// Constrain the sliding value to a range
        /// </summary>
        [BehaviorParameter]
        public bool Constrain { get; set; } = true;

        /// <summary>
        /// Minimum of limit
        /// </summary>
        [BehaviorParameter]
        public Vector2 LimitMin { get; set; } = new Vector2(0.0f);

        /// <summary>
        /// Maximium of limit
        /// </summary>
        [BehaviorParameter]
        public Vector2 LimitMax { get; set; } = new Vector2(1.0f);

        /// <summary>
        /// Offset for the Horizontal axis in the attached value array
        /// </summary>
        [BehaviorParameter]
        public int HorizontalOffs { get; set; } = 0;

        /// <summary>
        /// Offset for the Vertical axis in the attached value array
        /// </summary>
        [BehaviorParameter]
        public int VerticalOffs { get; set; } = 1;

        /// <summary>
        /// Mask for mouse buttons if interacting touch has mouse assigned to it. If empty all touches are allowed.
        /// If it contains Left mouse button regular touches also allowed.
        /// If Left mouse button is not present regular touches will be ignored.
        /// </summary>
        [BehaviorParameter]
        public MouseButtons[] MouseMask { get; set; } = new MouseButtons[0];

        /// <inheritdoc cref="InteractionBehavior"/>
        public override void Behave(NotuiElement element)
        {
            if (element.Value == null)
            {
                element.Value = new AttachedValues();
            }

            var stateAvailable = IsStateAvailable(element);
            var values = element.Value.Values;

            if (!stateAvailable)
            {
                SetState(element, new BehaviorState());
                element.Value.Values = new float[Max(VerticalOffs, HorizontalOffs) + 1];
                element.Value.Values.Fill(values);
                values = element.Value.Values;
                values[HorizontalOffs] = Default.X;
                values[VerticalOffs] = Default.Y;
            }

            if (AxisCoeff.Length() < 0.00001) return;
            if(Constrain && Vector2.Distance(LimitMin, LimitMax) < 0.00001) return;
            if(element.Touching.IsEmpty) return;

            KeyValuePair<Touch, IntersectionPoint> fastesttouch;
            try
            {
                if (MouseMask.Length == 0)
                    fastesttouch = element.Touching.OrderByDescending(t => t.Key.Velocity.LengthSquared()).First();
                else if (MouseMask.Length == 1 && MouseMask[0] == MouseButtons.Left)
                {
                    fastesttouch = (from touch in element.Touching
                            where touch.Key.AttachadMouse == null || touch.Key.MouseDelta.MouseClicks.Values.Where(mc => mc.Button != MouseButtons.Left).All(mc => !mc.Pressed)
                            select touch)
                        .OrderByDescending(t => t.Key.Velocity.LengthSquared()).First();
                }
                else
                {
                    fastesttouch = (from touch in element.Touching
                        where touch.Key.AttachadMouse != null
                        where touch.Key.MouseDelta.MouseClicks.Values.Where(mc => mc.Pressed).All(mc => MouseMask.Contains(mc.Button))
                        select touch)
                        .OrderByDescending(t => t.Key.Velocity.LengthSquared()).First();
                }
            }
            catch
            {
                return;
            }

            if (values.Length <= Max(VerticalOffs, HorizontalOffs))
            {
                element.Value.Values = new float[Max(VerticalOffs, HorizontalOffs) + 1];
                element.Value.Values.Fill(values);
                values = element.Value.Values;
            }

            Vector3 vel;
            if (UseSurfaceSpace)
            {
                var origis = fastesttouch.Value;
                if (origis == null) return;
                element.PureHitTest(fastesttouch.Key, true, out var previs);
                if(previs == null) return;
                vel = (origis.SurfaceSpace - previs.SurfaceSpace) * 0.5f;
            }
            else vel = fastesttouch.Key.GetPlanarVelocity(element.DisplayMatrix, element.Context, out var cpos, out var ppos);

            if(Constrain)
            {
                values[HorizontalOffs] = Max(LimitMin.X, Min(LimitMax.X, values[HorizontalOffs] + vel.X * AxisCoeff.X * 0.5f));
                values[VerticalOffs] = Max(LimitMin.Y, Min(LimitMax.Y, values[VerticalOffs] + vel.Y * AxisCoeff.Y * 0.5f));
            }
            else
            {
                values[HorizontalOffs] += vel.X * AxisCoeff.X * 0.5f;
                values[VerticalOffs] += vel.Y * AxisCoeff.Y * 0.5f;
            }
        }
    }
}
