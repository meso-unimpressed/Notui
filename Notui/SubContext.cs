using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using md.stdl.Interaction;
using md.stdl.Interfaces;
using md.stdl.Mathematics;
using VVVV.Utils.IO;

namespace Notui
{
    public class SubContextOptions : ICloneable<SubContextOptions>
    {
        public enum IntersectionSpaceSelection
        {
            FromSurfaceSpace,
            FromElementSpace
        }
        public bool IncludeHitting { get; set; } = true;

        public IntersectionSpaceSelection TouchSpaceSource { get; set; } =
            IntersectionSpaceSelection.FromSurfaceSpace;

        public SubContextOptions Copy()
        {
            return new SubContextOptions
            {
                IncludeHitting = IncludeHitting,
                TouchSpaceSource = TouchSpaceSource
            };
        }

        public object Clone()
        {
            return Copy();
        }
    }
    public class SubContext : IMainlooping, ICloneable<SubContext>, IUpdateable<SubContextOptions>, IDisposable
    {
        public SubContextOptions Options { get; private set; }
        public NotuiElement AttachedElement { get; private set; }
        public NotuiContext ParentContext => AttachedElement.Context;
        public NotuiContext Context { get; }
        public bool Detached { get; private set; }

        public void Mainloop(float deltatime)
        {
            OnMainLoopBegin?.Invoke(this, EventArgs.Empty);
            var touchsrc = Options.IncludeHitting ? AttachedElement.Hitting : AttachedElement.Touching.Where(kvp => kvp.Value != null);
            var inputtouches = touchsrc.Select(kvp =>
            {
                switch (Options.TouchSpaceSource)
                {
                    case SubContextOptions.IntersectionSpaceSelection.FromElementSpace:
                        return (kvp.Value.ElementSpace.xy(), kvp.Key.Id, kvp.Key.Force);
                    case SubContextOptions.IntersectionSpaceSelection.FromSurfaceSpace:
                        return (kvp.Value.SurfaceSpace.xy(), kvp.Key.Id, kvp.Key.Force);
                    default:
                        return (kvp.Value.SurfaceSpace.xy(), kvp.Key.Id, kvp.Key.Force);
                }
            });
            Context.SubmitTouches(inputtouches);
            Context.Mainloop(ParentContext.DeltaTime);

            if (ParentContext.AttachableMouse != null && ParentContext.MouseDelta != null)
            {
                if (Context.Touches.ContainsKey(ParentContext.MouseTouchId))
                {
                    Context.Touches.TryGetValue(ParentContext.MouseTouchId, out var mouseTouch);
                    if (mouseTouch != null && (mouseTouch.AttachadMouse == null || mouseTouch.MouseDelta == null))
                    {
                        mouseTouch.AttachMouse(ParentContext.AttachableMouse, ParentContext.MouseDelta);
                    }
                }
            }
            OnMainLoopEnd?.Invoke(this, EventArgs.Empty);
        }

        public event EventHandler OnMainLoopBegin;
        public event EventHandler OnMainLoopEnd;

        public void SwitchElement(NotuiElement newelement)
        {
            AttachedElement.OnMainLoopEnd -= AttachedElementMainloopListener;
            AttachedElement = newelement;
            AttachedElement.OnMainLoopEnd += AttachedElementMainloopListener;
        }

        public SubContext(NotuiElement element, SubContextOptions options)
        {
            AttachedElement = element;
            Options = options.Copy();
            element.OnMainLoopEnd += AttachedElementMainloopListener;
            Context = new NotuiContext();
        }

        private void AttachedElementMainloopListener(object sender, EventArgs e)
        {
            Mainloop(ParentContext.DeltaTime);
        }

        public SubContext Copy()
        {
            return new SubContext(AttachedElement, Options.Copy());
        }

        public SubContext Copy(NotuiElement destination)
        {
            return new SubContext(destination, Options.Copy());
        }

        public object Clone()
        {
            throw new NotImplementedException();
        }

        public void UpdateFrom(SubContextOptions other)
        {
            Options = other.Copy();
        }

        public void Dispose()
        {
            if(Detached) return;
            AttachedElement.OnMainLoopEnd -= AttachedElementMainloopListener;
            Detached = true;
        }
    }
}
