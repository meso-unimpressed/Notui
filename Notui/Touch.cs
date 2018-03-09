using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using md.stdl.Interaction;

namespace Notui
{
    public class Touch : TouchContainer
    {
        /// <summary>
        /// Number of frames since the touch pressed with force over the threshold
        /// </summary>
        public int FramesSincePressed { get; protected set; }
        
        /// <summary>
        /// Elements this touch currently hitting
        /// </summary>
        public List<NotuiElement> HittingElements { get; } = new List<NotuiElement>();

        public bool Pressed { get; protected set; } = false;

        public bool Press(float minforce)
        {
            Pressed = Force > minforce;
            return Pressed;
        }

        public Touch(int id) : base(id)
        {
            OnMainLoopEnd += (sender, args) =>
            {
                if (Pressed) FramesSincePressed++;
                else FramesSincePressed = 0;
            };
            CustomAttachedObject = HittingElements;
        }
    }
}
