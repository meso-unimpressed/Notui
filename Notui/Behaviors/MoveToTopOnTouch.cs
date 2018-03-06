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

        /// <summary>
        /// Use interaction transform to get element current depth
        /// </summary>
        [BehaviorParameter]
        public bool UseInteractionTransform { get; set; }

        private ElementTransformation SelectTransform(NotuiElement element)
        {
            return UseInteractionTransform ? element.InteractionTransformation : element.DisplayTransformation;
        }

        public override void Behave(NotuiElement element)
        {
            if (!element.Touched) return;
            var mindist = element.Children.Values.Min(child => SelectTransform(child).GetViewPosition(child.Context).Z);
            var moveby = Math.Max(mindist - Distance, 0);
            var movedir = Vector3.Normalize(element.Context.ViewPosition - SelectTransform(element).Position);
            element.DisplayTransformation.Translate(movedir * moveby);
            element.InteractionTransformation.Translate(movedir * moveby);
            foreach (var el in element.Context.RootElements.Values)
            {
                el.DisplayTransformation.Translate(movedir * mindist * -1);
                el.InteractionTransformation.Translate(movedir * mindist * -1);
            }
        }
    }
}
