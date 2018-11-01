using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using md.stdl.Interfaces;
using md.stdl.Mathematics;

namespace Notui.Behaviors
{
    /// <inheritdoc />
    /// <summary>
    /// Move this element closest to the viewer
    /// </summary>
    public class MoveToTopOnTouchBehavior : InteractionBehavior
    {
        /// <summary>
        /// Stateful data for MoveToTopOnTouchBehavior behavior
        /// </summary>
        public class BehaviorState : AuxiliaryObject
        {
            private readonly NotuiElement _element;
            /// <summary>
            /// Is assigned element have been interacted with
            /// </summary>
            public bool Interacted { get; set; }

            private void RecursiveInteracted(NotuiElement element)
            {
                element.OnTouchBegin += (sender, args) => Interacted = true;
                foreach (var child in element.Children.Values)
                {
                    RecursiveInteracted(child);
                }
            }

            /// <summary>
            /// Default constructor
            /// </summary>
            /// <param name="element">Assign an element to this state</param>
            public BehaviorState(NotuiElement element)
            {
                _element = element;
                RecursiveInteracted(element);
            }

            /// <inheritdoc cref="AuxiliaryObject"/>
            public override IAuxiliaryObject Copy()
            {
                return new BehaviorState(_element);
            }

            /// <inheritdoc cref="AuxiliaryObject"/>
            public override void UpdateFrom(IAuxiliaryObject other) { }
        }

        /// <summary>
        /// Distance between elements
        /// </summary>
        [BehaviorParameter]
        public float Distance { get; set; } = 1.0f;

        /// <summary>
        /// Top Depth
        /// </summary>
        [BehaviorParameter]
        public float Top { get; set; } = 0.0f;

        /// <inheritdoc cref="InteractionBehavior"/>
        public override void Behave(NotuiElement element)
        {
            var currstate = IsStateAvailable(element) ? GetState<BehaviorState>(element) : new BehaviorState(element);
            SetState(element, currstate);
            if (currstate.Interacted)
            {
                var restsrc = element.Parent?.Children.Values ?? element.Context.RootElements.Values;

                if (restsrc.Count == 1) return;

                var rest = element.Parent?.Children.Values.Where(rel => rel.Id != element.Id) ??
                           element.Context.RootElements.Values.Where(rel => rel.Id != element.Id);

                var ordered = rest.OrderBy(rel => rel.DisplayTransformation.Position.Z);

                var depth = Distance + Top;

                var currp = element.DisplayTransformation.Position;
                element.DisplayTransformation.Position = new Vector3(currp.xy(), Top);
                var currtp = element.TargetTransformation.Position;
                element.TargetTransformation.Position = new Vector3(currtp.xy(), Top);

                foreach (var el in ordered)
                {
                    var elp = el.DisplayTransformation.Position;
                    el.DisplayTransformation.Position = new Vector3(elp.xy(), depth);
                    var eltp = el.TargetTransformation.Position;
                    el.TargetTransformation.Position = new Vector3(eltp.xy(), depth);
                    depth += Distance;
                }

                currstate.Interacted = false;
            }
        }
    }
}
