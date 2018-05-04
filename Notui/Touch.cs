using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using md.stdl.Interaction;
using VVVV.Utils.IO;

namespace Notui
{
    /// <summary>
    /// Notui specific touches
    /// </summary>
    public class Touch : TouchContainer
    {
        /// <summary>
        /// Number of frames since the touch pressed with force over the threshold
        /// </summary>
        public int FramesSincePressed { get; protected set; }

        /// <summary>
        /// An optional Mouse device in case this touch represents a cursor pointer.
        /// </summary>
        public Mouse AttachadMouse { get; private set; }
        /// <summary>
        /// If a Mouse is attached this property stores the mouse delta, otherwise null.
        /// </summary>
        public AccumulatingMouseObserver MouseDelta { get; private set; }

        /// <summary>
        /// If you want to make this touch represent a mouse cursor, call this.
        /// </summary>
        /// <param name="mouse">The mouse device</param>
        /// <param name="mousedelta">An accumulating delta observer of that same mouse device</param>
        public void AttachMouse(Mouse mouse, AccumulatingMouseObserver mousedelta)
        {
            AttachadMouse = mouse;
            MouseDelta = mousedelta;
        }

        /// <summary>
        /// Elements this touch currently hitting
        /// </summary>
        public List<NotuiElement> HittingElements { get; } = new List<NotuiElement>();

        /// <summary>
        /// The context this touch is created in
        /// </summary>
        public readonly NotuiContext Context;

        /// <summary>
        /// The touch is pressed (aka its force over the context minimum)
        /// </summary>
        public bool Pressed { get; protected set; } = false;

        /// <summary>
        /// Check for pressing criteria (only for contexts)
        /// </summary>
        /// <param name="minforce"></param>
        /// <returns></returns>
        public bool Press(float minforce)
        {
            Pressed = Force > minforce;
            return Pressed;
        }

        /// <inheritdoc />
        public Touch(int id, NotuiContext context) : base(id)
        {
            OnMainLoopEnd += (sender, args) =>
            {
                if (Pressed) FramesSincePressed++;
                else FramesSincePressed = 0;
            };
            CustomAttachedObject = HittingElements;
            Context = context;
        }

        /// <inheritdoc />
        public override bool Equals(object obj)
        {
            switch (obj)
            {
                case null:
                    return false;
                case Touch touch:
                    return Id == touch.Id && Context == touch.Context;
                default:
                    return false;
            }
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            return Id ^ Context.GetHashCode();
        }
    }
}
