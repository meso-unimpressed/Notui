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
    /// <summary>
    /// Options class for prototyping a sub-context
    /// </summary>
    public class SubContextOptions : ICloneable<SubContextOptions>
    {
        /// <summary>
        /// Select the space where the XY coordinates should be derived from
        /// </summary>
        public enum IntersectionSpaceSelection
        {
            /// <summary></summary>
            FromSurfaceSpace,
            /// <summary></summary>
            FromElementSpace
        }

        /// <summary>
        /// Channel touches which are only hitting too instead of only interacting touches
        /// </summary>
        public bool IncludeHitting { get; set; } = true;

        /// <summary>
        /// Select the space where the XY coordinates should be derived from
        /// </summary>
        public IntersectionSpaceSelection TouchSpaceSource { get; set; } =
            IntersectionSpaceSelection.FromSurfaceSpace;

        public bool UpdateOnlyChangeFlagged { get; set; }

        /// <inheritdoc />
        public SubContextOptions Copy()
        {
            return new SubContextOptions
            {
                IncludeHitting = IncludeHitting,
                TouchSpaceSource = TouchSpaceSource,
                UpdateOnlyChangeFlagged = UpdateOnlyChangeFlagged
            };
        }

        /// <inheritdoc />
        public object Clone()
        {
            return Copy();
        }
    }

    /// <summary>
    /// Subcontexts are good for non Euclidean spaces determined by a host element's surface, or different viewports
    /// </summary>
    public class SubContext : IMainlooping, ICloneable<SubContext>, IUpdateable<SubContextOptions>, IDisposable
    {
        /// <summary>
        /// The options this subcontext is created with
        /// </summary>
        public SubContextOptions Options { get; private set; }

        /// <summary>
        /// The hosting element
        /// </summary>
        public NotuiElement AttachedElement { get; private set; }

        /// <summary>
        /// The context of the host element
        /// </summary>
        public NotuiContext ParentContext => AttachedElement.Context;

        /// <summary>
        /// The actual Notui context
        /// </summary>
        public NotuiContext Context { get; }

        /// <summary>
        /// The hosting element have been destroyed
        /// </summary>
        public bool Detached { get; private set; }

        /// <inheritdoc />
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

        /// <inheritdoc />
        public event EventHandler OnMainLoopBegin;
        /// <inheritdoc />
        public event EventHandler OnMainLoopEnd;

        /// <summary>
        /// Transfer this Subcontext to another element
        /// </summary>
        /// <param name="newelement"></param>
        public void SwitchElement(NotuiElement newelement)
        {
            AttachedElement.OnMainLoopEnd -= AttachedElementMainloopListener;
            AttachedElement = newelement;
            AttachedElement.OnMainLoopEnd += AttachedElementMainloopListener;
        }

        /// <summary></summary>
        /// <param name="element">The hosting element</param>
        /// <param name="options">The creation option</param>
        public SubContext(NotuiElement element, SubContextOptions options)
        {
            AttachedElement = element;
            Options = options.Copy();
            element.OnMainLoopEnd += AttachedElementMainloopListener;
            Context = new NotuiContext
            {
                UpdateOnlyChangeFlagged = Options.UpdateOnlyChangeFlagged
            };
        }

        private void AttachedElementMainloopListener(object sender, EventArgs e)
        {
            Mainloop(ParentContext.DeltaTime);
        }

        /// <inheritdoc />
        public SubContext Copy()
        {
            return new SubContext(AttachedElement, Options.Copy());
        }

        /// <summary>
        /// Copy this subcontext to another element
        /// </summary>
        /// <param name="destination"></param>
        /// <returns>The new SubContext</returns>
        public SubContext Copy(NotuiElement destination)
        {
            return new SubContext(destination, Options.Copy());
        }

        /// <inheritdoc />
        public object Clone()
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc />
        public void UpdateFrom(SubContextOptions other)
        {
            Options = other.Copy();
        }

        /// <inheritdoc />
        public void Dispose()
        {
            if(Detached) return;
            AttachedElement.OnMainLoopEnd -= AttachedElementMainloopListener;
            Detached = true;
        }
    }
}
