using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace Notui.Behaviors
{
    /// <inheritdoc />
    /// <summary>
    /// Move this element closest to the viewer
    /// </summary>
    public class MoveToTopOnTouchBehavior : InteractionBehavior
    {
        /// <summary>
        /// Distance to move to
        /// </summary>
        [BehaviorParameter]
        public float Distance { get; set; } = 0.0f;

        public override void Behave(NotuiElement element)
        {
            if (!element.Touched) return;
            var mindist = element.Children.Values.Min(child => child.DisplayTransformation.GetViewPosition(child.Context).Z);
            var moveby = Math.Max(mindist - Distance, 0);
            var movedir = Vector3.Normalize(element.Context.ViewPosition - element.DisplayTransformation.Position);
            element.DisplayTransformation.Translate(movedir * moveby);
            foreach (var el in element.Context.RootElements.Values)
            {
                el.DisplayTransformation.Translate(movedir * mindist * -1);
            }
            element.TargetTransformation.UpdateFrom(element.DisplayTransformation);
        }
    }
}
