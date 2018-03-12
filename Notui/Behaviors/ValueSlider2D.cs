using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using md.stdl.Coding;
using static System.Math;

namespace Notui.Behaviors
{
    public class ValueSlider2D : InteractionBehavior
    {
        public class BehaviorState : AuxiliaryObject
        {
            public override AuxiliaryObject Copy()
            {
                return new BehaviorState();
            }

            public override void UpdateFrom(AuxiliaryObject other) { }
        }

        /// <summary>
        /// Slide speed along the horizontal (X) or the vertical (Y) axis (0 == off)
        /// </summary>
        [BehaviorParameter]
        public Vector2 AxisCoeff { get; set; } = new Vector2(1.0f);

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

            var fastesttouch = element.Touching.Keys.OrderByDescending(t => t.Velocity.LengthSquared()).First();
            if (values.Length <= Max(VerticalOffs, HorizontalOffs))
            {
                element.Value.Values = new float[Max(VerticalOffs, HorizontalOffs) + 1];
                element.Value.Values.Fill(values);
                values = element.Value.Values;
            }

            var vel = fastesttouch.GetPlanarVelocity(element.DisplayMatrix, element.Context, out var cpos, out var ppos);
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
