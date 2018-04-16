using System;
using System.Collections.Concurrent;
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
    public abstract class PlanarBehavior : InteractionBehavior
    {
        protected enum InteractingTouchSource
        {
            Touching,
            Hitting,
            Mice
        }
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

        /// <summary>
        /// Behavior should happen on this selected plane.
        /// </summary>
        [BehaviorParameter]
        public SelectedPlane UseSelectedPlane { get; set; } = SelectedPlane.ViewAligned;

        /// <summary>
        /// Behavior should happen on children which are hit as well
        /// </summary>
        [BehaviorParameter]
        public bool UseChildrenInteracting { get; set; }

        /// <summary>
        /// (NotuiElement currElement): NotuiElement Children; Exclusively exectue this behavior only on specific children filtered by a predicate function
        /// </summary>
        [BehaviorParameter]
        public Func<NotuiElement, IEnumerable<NotuiElement>> UseOnlyWithSpecificChildren { get; set; }

        /// <summary>
        /// Plane selection can programatically depend on whether the element has a parent or not. If user selected ParentPlane when there's no parent plane selection defaults to ViewAligned
        /// </summary>
        protected SelectedPlane ActualPlaneSelection;

        protected IEnumerable<Touch> GetTouchesFromSource(NotuiElement element,
            InteractingTouchSource touchsrc = InteractingTouchSource.Touching)
        {
            switch (touchsrc)
            {
                case InteractingTouchSource.Touching:
                    return element.Touching.Keys;
                case InteractingTouchSource.Hitting:
                    return element.Hitting.Keys;
                case InteractingTouchSource.Mice:
                    return element.Mice;
                default:
                    return Enumerable.Empty<Touch>();
            }
        }

        protected void AddChildrenTouches(NotuiElement element, List<Touch> touches, InteractingTouchSource touchsrc = InteractingTouchSource.Touching)
        {
            foreach (var child in element.Children.Values)
            {
                touches.AddRange(GetTouchesFromSource(child, touchsrc));
                AddChildrenTouches(child, touches, touchsrc);
            }
        }

        protected Matrix4x4 GetUsedPlane(NotuiElement element)
        {
            ActualPlaneSelection = UseSelectedPlane == SelectedPlane.ParentPlane ?
                (element.Parent != null ? SelectedPlane.ParentPlane : SelectedPlane.ViewAligned) :
                UseSelectedPlane;

            Matrix4x4 usedplane;
            switch (ActualPlaneSelection)
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
                    usedplane = Matrix4x4.CreateFromQuaternion(element.Context.ViewOrientation) *
                                Matrix4x4.CreateTranslation(element.DisplayMatrix.Translation);
                    break;
            }

            return usedplane;
        }

        protected List<Touch> GetBehavingTouches(NotuiElement element, InteractingTouchSource touchsrc = InteractingTouchSource.Touching)
        {
            List<Touch> touches;
            if (UseOnlyWithSpecificChildren == null)
            {
                touches = GetTouchesFromSource(element, touchsrc).ToList();
                if (UseChildrenInteracting)
                    AddChildrenTouches(element, touches, touchsrc);
            }
            else
            {
                touches = new List<Touch>();
                var selectedChildren = UseOnlyWithSpecificChildren(element);
                foreach (var child in selectedChildren)
                    touches.AddRange(GetTouchesFromSource(child, touchsrc));
            }

            return touches;
        }
    }
}
