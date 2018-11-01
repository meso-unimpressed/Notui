using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Reactive;
using System.Reactive.Linq;
using System.Windows.Forms;
using md.stdl.Interaction;
using md.stdl.Interfaces;
using md.stdl.Time;
using SharpDX.RawInput;

namespace Notui
{
    /// <inheritdoc />
    /// <summary>
    /// Event args involving touches
    /// </summary>
    public class TouchInteractionEventArgs : EventArgs
    {
        public Touch Touch;
        public IntersectionPoint IntersectionPoint;
    }
    /// <inheritdoc />
    /// <summary>
    /// Event args involving mice
    /// </summary>
    public class MouseInteractionEventArgs : TouchInteractionEventArgs
    {
        public MouseButtons Buttons;
        public bool Hitting;
        public bool Touching;
    }

    /// <inheritdoc />
    /// <summary>
    /// Event args for changed children
    /// </summary>
    public class ChildrenUpdatedEventArgs : EventArgs
    {
        public IEnumerable<IElementCommon> Elements;
    }

    /// <inheritdoc cref="ElementPrototype" />
    /// <summary>
    /// Simple element base implementing some useful management functions
    /// </summary>
    public abstract class NotuiElement : IElementCommon, ICloneable<NotuiElement>, IUpdateable<ElementPrototype>, IMainlooping
    {
        private Matrix4x4 _displayMatrix;
        private Matrix4x4 _invDisplayMatrix;
        private bool _onFadedInInvoked;
        private SubContext _subContext;

        /// <summary>
        /// Timer for the FadeOut delay
        /// </summary>
        public StopwatchInteractive FadeOutDelayTimer = new StopwatchInteractive();
        /// <summary>
        /// Timer for the FadeIn delay
        /// </summary>
        public StopwatchInteractive FadeInDelayTimer = new StopwatchInteractive();
        
        /// <inheritdoc />
        public string Name { get; set; }
        /// <inheritdoc />
        public string Id { get; set; } = Guid.NewGuid().ToString();
        /// <inheritdoc />
        public float FadeOutTime { get; set; }
        /// <inheritdoc />
        public float FadeInTime { get; set; }

        /// <inheritdoc />
        public float FadeOutDelay { get; set; }

        /// <summary>
        /// Fade out delay taking into account children delays
        /// </summary>
        public float AbsoluteFadeOutDelay => FadeOutDelay + (Children.Count > 0 ? Children.Values.Max(cel => cel.FadeOutDelay) : 0);

        /// <inheritdoc />
        public float FadeInDelay { get; set; }

        /// <summary>
        /// Fade in delay taking into account parent delay
        /// </summary>
        public float AbsoluteFadeInDelay => FadeInDelay + (Parent?.FadeInDelay ?? 0);

        /// <inheritdoc />
        public float TransformationFollowTime { get; set; }

        /// <inheritdoc />
        public bool Active { get; set; }
        /// <inheritdoc />
        public bool Transparent { get; set; }
        /// <inheritdoc />
        public List<InteractionBehavior> Behaviors { get; set; } = new List<InteractionBehavior>();
        /// <inheritdoc />
        public AttachedValues Value { get; set; }
        /// <inheritdoc />
        public IAuxiliaryObject EnvironmentObject { get; set; }
        /// <inheritdoc />
        public bool OnlyHitIfParentIsHit { get; set; }

        /// <inheritdoc />
        public ElementTransformation DisplayTransformation { get; set; } = new ElementTransformation();

        /// <summary>
        /// An immediate target transformation which can be smoothly transitioned towards
        /// </summary>
        public ElementTransformation TargetTransformation { get; set; } = new ElementTransformation();

        /// <summary>
        /// The context which this element is assigned to.
        /// </summary>
        public NotuiContext Context { get; }

        /// <summary>
        /// The prototype this element was created from.
        /// </summary>
        public ElementPrototype Prototype { get; set; }

        /// <summary>
        /// The element this element inherits its transformation from. Null if this element is directly in a context.
        /// </summary>
        public NotuiElement Parent { get; set; }

        /// <summary>
        /// Are there touches over this element?
        /// </summary>
        public bool Hit { get; set; }

        /// <summary>
        /// Does this element has touches interacting with it?
        /// </summary>
        public bool Touched { get; set; }

        /// <summary>
        /// Element fading from 0 (faded out) to 1 (faded in)
        /// </summary>
        public float ElementFade { get; set; }

        /// <summary>
        /// List of touches interacting with this element which is managed by this element
        /// </summary>
        public ConcurrentDictionary<Touch, IntersectionPoint> Touching { get; set; } =
            new ConcurrentDictionary<Touch, IntersectionPoint>(new TouchEqualityComparer());
        
        /// <summary>
        /// List of touches directly over this element which is managed by this element
        /// </summary>
        public ConcurrentDictionary<Touch, IntersectionPoint> Hitting { get; set; } =
            new ConcurrentDictionary<Touch, IntersectionPoint>(new TouchEqualityComparer());

        /// <summary>
        /// List of touches hovering this element which is managed by the context
        /// </summary>
        public ConcurrentDictionary<Touch, IntersectionPoint> Hovering { get; set; } =
            new ConcurrentDictionary<Touch, IntersectionPoint>(new TouchEqualityComparer());

        /// <summary>
        /// List of touches which appear to be mice
        /// </summary>
        public Touch[] Mice { get; set; } = Array.Empty<Touch>();

        /// <summary>
        /// Elements which will inherit the transformation of this element
        /// </summary>
        public Dictionary<string, NotuiElement> Children { get; set; } = new Dictionary<string, NotuiElement>();

        /// <summary>
        /// Requests context to delete this element and its children
        /// </summary>
        public bool DeleteMe { get; set; }

        /// <summary>
        /// The deletion of this element is started
        /// </summary>
        public bool Dying { get; set; }

        /// <summary>
        /// Element age since creation
        /// </summary>
        public StopwatchInteractive Age { get; set; } = new StopwatchInteractive();

        /// <summary>
        /// This stopwatch is started to fade in this element after creation (+ optional delay).
        /// </summary>
        public StopwatchInteractive FadeInStopwatch { get; set; } = new StopwatchInteractive();

        /// <summary>
        /// This stopwatch is started to fade out this element before deletion.
        /// </summary>
        /// <remarks>
        /// Metalocalypse
        /// </remarks>
        public StopwatchInteractive Dethklok { get; set; } = new StopwatchInteractive();

        /// <summary>
        /// Was display matrix already calculated since last request.
        /// </summary>
        public bool DisplayMatrixCached { get; set; }

        /// <summary>
        /// Absolute world display transformation.
        /// </summary>
        public Matrix4x4 DisplayMatrix
        {
            get
            {
                if (!DisplayMatrixCached)
                {
                    _displayMatrix = GetDisplayTransform();
                    Matrix4x4.Invert(_displayMatrix, out _invDisplayMatrix);
                    DisplayMatrixCached = true;
                }
                return _displayMatrix;
            }
        }
        /// <summary>
        /// Inverse of absolute world transformation.
        /// </summary>
        public Matrix4x4 InverseDisplayMatrix
        {
            get
            {
                if (!DisplayMatrixCached)
                {
                    _displayMatrix = GetDisplayTransform();
                    Matrix4x4.Invert(_displayMatrix, out _invDisplayMatrix);
                    DisplayMatrixCached = true;
                }
                return _invDisplayMatrix;
            }
        }

        /// <summary>
        /// If not null this element contain their own Notui context. This is good for viewports, clipping and arbitrary surface deformation.
        /// </summary>
        /// <remarks>
        /// This is like iframes for HTML. SubContexts are not replacing children and elements inside SubContexts are not queried with Opaq.
        /// But unlike iframes in HTML nothing stops you here to have logical connections between different SubContexts of different elements.
        /// </remarks>
        public SubContext SubContext
        {
            get => _subContext;
            set
            {
                if (value == null)
                    _subContext?.Dispose();
                _subContext = value;
            }
        }

        /// <summary>
        /// Pure function for getting the matrix of the display transformation
        /// </summary>
        /// <returns></returns>
        public Matrix4x4 GetDisplayTransform()
        {
            var parent = Matrix4x4.Identity;
            if (Parent != null) parent = Parent.GetDisplayTransform();
            return DisplayTransformation.Matrix * parent;
        }

        /// <summary>
        /// Pure function for flattening the element hiararchy into a single list
        /// </summary>
        /// <param name="flatElements">The list containing the result</param>
        public void FlattenElements(List<NotuiElement> flatElements)
        {
            flatElements.Add(this);
            foreach (var child in Children.Values)
            {
                child.FlattenElements(flatElements);
            }
        }
        /// <summary>
        /// Immediately follow the transformation of another element (instance or prototype)
        /// </summary>
        /// <param name="element"></param>
        public void FollowTransformation(IElementCommon element)
        {
            DisplayTransformation.UpdateFrom(element.DisplayTransformation);
        }

        /// <summary>
        /// Event on the first of multiple touches interacting with this element until the last touch is released
        /// </summary>
        public event EventHandler<TouchInteractionEventArgs> OnInteractionBegin;

        /// <summary>
        /// Event on the last touch interacting with this element is released
        /// </summary>
        public event EventHandler<TouchInteractionEventArgs> OnInteractionEnd;

        /// <summary>
        /// Event fired when a touch started interacting with this element
        /// </summary>
        public event EventHandler<TouchInteractionEventArgs> OnTouchBegin;

        /// <summary>
        /// Event on the release of a touch interacting with this element
        /// </summary>
        public event EventHandler<TouchInteractionEventArgs> OnTouchEnd;

        /// <summary>
        /// Event fired when a touch got over this element
        /// </summary>
        public event EventHandler<TouchInteractionEventArgs> OnHitBegin;

        /// <summary>
        /// Event fired when a touch left this element
        /// </summary>
        public event EventHandler<TouchInteractionEventArgs> OnHitEnd;

        /// <summary>
        /// Event fired when a button is pressed on a touch with attached mouse above the element
        /// </summary>
        public event EventHandler<MouseInteractionEventArgs> OnMouseButtonPressed;
        /// <summary>
        /// Event fired when a button is released on a touch with attached mouse above the element
        /// </summary>
        public event EventHandler<MouseInteractionEventArgs> OnMouseButtonReleased;

        /// <summary>
        /// Event fired when a touch with attached mouse above the element changes the vertical scroll wheel position
        /// </summary>
        public event EventHandler<MouseInteractionEventArgs> OnVerticalMouseWheelChange;
        /// <summary>
        /// Event fired when a touch with attached mouse above the element changes the horizontal scroll wheel position
        /// </summary>
        public event EventHandler<MouseInteractionEventArgs> OnHorizontalMouseWheelChange;

        /// <summary>
        /// Event fired on every frame while the element is being interacted with
        /// </summary>
        public event EventHandler OnInteracting;

        /// <summary>
        /// Event fired when a child element is added
        /// </summary>
        public event EventHandler<ChildrenUpdatedEventArgs> OnChildrenUpdated;

        /// <summary>
        /// Event fired when the Dethklok is started. Except when FadeOutTime is set to 0.
        /// </summary>
        public event EventHandler OnDeletionStarted;

        /// <summary>
        /// Event fired when the Element requested its deletion
        /// </summary>
        public event EventHandler OnDeleting;

        /// <summary>
        /// Event fired when the Element finished fading
        /// </summary>
        public event EventHandler OnFadedIn;

        /// <summary>
        /// First thing invoked on the mainloop (even before the virtual MainLoopBegin)
        /// </summary>
        public event EventHandler OnMainLoopBegin;

        /// <summary>
        /// Last thing invoked on the mainloop (even after the virtual MainLoopEnd)
        /// </summary>
        public event EventHandler OnMainLoopEnd;

        /// <summary>
        /// Add or update children of this element from prototypes
        /// </summary>
        /// <param name="children">children to be added</param>
        /// <param name="removeNotPresent">Remove Children from elements not present in the input</param>
        /// <returns>New children added to the element</returns>
        public List<NotuiElement> UpdateChildren(bool removeNotPresent = false, params ElementPrototype[] children)
        {
            var newchildren = new List<NotuiElement>();
            if (removeNotPresent)
            {
                var removablechildren = (from child in Children.Values where children.All(c => c.Id != child.Id) select child).ToArray();
                foreach (var child in removablechildren)
                {
                    child.StartDeletion();
                }
            }

            foreach (var child in children)
            {
                if(Children.ContainsKey(child.Id))
                    Children[child.Id].UpdateFrom(child);
                else if(child.Id != Id)
                {
                    var childinst = child.Instantiate(Context, this);
                    Children.Add(child.Id, childinst);
                    newchildren.Add(childinst);
                }
            }

            OnChildrenUpdated?.Invoke(this, new ChildrenUpdatedEventArgs {Elements = children});
            return newchildren;
        }

        /// <summary>
        /// Pure hittest function used by inherited element classes
        /// </summary>
        /// <param name="touch">Current touch</param>
        /// <param name="prevpos">Calculate Intersection point for previous position</param>
        /// <param name="persistentIspoint">
        /// Optional persistent intersection point data in case it makes sense to intersect beyond the boundaries of an element.
        /// Just assign the same as the return if not
        /// </param>
        /// <returns>Return null when the element is not hit by the touch and return the intersection coordinates otherwise</returns>
        public abstract IntersectionPoint PureHitTest(Touch touch, bool prevpos, out IntersectionPoint persistentIspoint);

        /// <summary>
        /// Pure hittest function including parent hitting constraint as well used by the context
        /// </summary>
        /// <param name="touch">Current touch</param>
        /// <param name="prevpos">Calculate Intersection point for previous position</param>
        /// <returns>Return null when the element is not hit by the touch and return the intersection coordinates otherwise</returns>
        public IntersectionPoint HitTest(Touch touch, bool prevpos = false)
        {
            if (Parent == null || !OnlyHitIfParentIsHit)
                return PureHitTest(touch, prevpos, out var dummy);
            else
            {
                return Parent.HitTest(touch) != null ? PureHitTest(touch, prevpos, out var dummy) : null;
            }
        }

        /// <summary>
        /// Used for managing side effects of touch interaction
        /// </summary>
        /// <param name="touch">Current touch</param>
        public virtual void ProcessTouch(Touch touch)
        {
            var hit = Hovering.ContainsKey(touch);
            var eventargs = new TouchInteractionEventArgs
            { 
                Touch = touch,
                IntersectionPoint = hit ? Hovering[touch] : null
            };
            if (hit && Touching.ContainsKey(touch)) Touching[touch] = Hovering[touch];
            if (!hit)
            {
                if (!Hitting.ContainsKey(touch)) return;
                Hitting.TryRemove(touch, out var dummy);
                OnHitEnd?.Invoke(this, eventargs);
                if (Touching.ContainsKey(touch)) Touching[touch] = null;
                return;
            }
            if (!Hitting.ContainsKey(touch))
            {
                OnHitBegin?.Invoke(this, eventargs);
                Hitting.TryAdd(touch, Hovering[touch]);
            }
            FireInteractionTouchBegin(touch);
        }

        /// <summary>
        /// Method invoked before anything happens in mainloop, but after OnMainloopBegin event
        /// </summary>
        protected virtual void MainloopBegin() { }
        /// <summary>
        /// Method invoked before behaviors are executed and OnInteracting is invoked
        /// </summary>
        protected virtual void MainloopBeforeBehaviors() { }
        /// <summary>
        /// Method invoked at the end of mainloop
        /// </summary>
        protected virtual void MainloopEnd() { }

        /// <summary>
        /// This is called every frame by the context
        /// </summary>
        /// <remarks>
        /// The context call this function of all flattened elements in parallel regardless of the element hierarchy, you should take this into account when overriding this function or developing behaviors. You MUST NOT call the Mainloop method of the children elements yourself because the context already does so (unless you are really desperate).
        /// </remarks>
        public void Mainloop(float deltatime)
        {
            OnMainLoopBegin?.Invoke(this, EventArgs.Empty);

            if(Children.Count > 0)
            {
                foreach (var child in Children.Values.ToArray())
                {
                    if (child.Dying && child.Dethklok.Elapsed.TotalSeconds > child.FadeOutTime)
                    {
                        Children.Remove(child.Id);
                        Context.RequestRebuild(true, false);
                    }
                }
            }
            
            DisplayTransformation.SubscribeToChange(Id, transformation => InvalidateMatrices());

            MainloopBegin();

            var endtouches = ( from touch in Touching.Keys
                where touch.ExpireFrames > Context.ConsiderReleasedAfter || !touch.Pressed
                select touch
                ).ToArray();

            foreach (var touch in endtouches)
                FireTouchEnd(touch);

            var endhits = ( from touch in Hitting.Keys
                where touch.ExpireFrames > Context.ConsiderReleasedAfter
                select touch
                ).ToArray();

            foreach (var touch in endhits)
            {
                var eventargs = new TouchInteractionEventArgs
                {
                    Touch = touch,
                    IntersectionPoint = Hitting[touch]
                };
                Hitting.TryRemove(touch, out var dummy);
                OnHitEnd?.Invoke(this, eventargs);
            }

            Hit = Hitting.Count > 0;
            Touched = Touching.Count > 0;

            foreach (var touch in Hitting.Keys)
            {
                var inters = HitTest(touch);
                Hitting[touch] = inters;
                if (Touching.ContainsKey(touch))
                    Touching[touch] = inters;
            }

            FadeOutDelayTimer.Mainloop(deltatime);
            FadeInDelayTimer.Mainloop(deltatime);

            if (FadeInTime > 0)
            {
                ElementFade = Math.Min(Math.Max(0, (float) FadeInStopwatch.Elapsed.TotalSeconds / FadeInTime), 1);
            }
            else if(FadeInStopwatch.IsRunning)
            {
                if(ElementFade < 1) OnFadedIn?.Invoke(this, EventArgs.Empty);
                _onFadedInInvoked = true;
                ElementFade = 1;
            }
            if (FadeInStopwatch.Elapsed.TotalSeconds >= FadeInTime && !_onFadedInInvoked)
            {
                OnFadedIn?.Invoke(this, EventArgs.Empty);
                _onFadedInInvoked = true;
            }

            if (FadeOutTime > 0)
            {
                ElementFade *= Math.Min(Math.Max(0, 1 - (float)Dethklok.Elapsed.TotalSeconds / FadeOutTime), 1);
                if (Dethklok.Elapsed.TotalSeconds > FadeOutTime && Dethklok.IsRunning && Age.Elapsed.TotalSeconds > Context.DeltaTime)
                {
                    ElementFade = 0;
                    if(!DeleteMe) OnDeleting?.Invoke(this, EventArgs.Empty);
                    DeleteMe = true;
                }
            }

            Mice = Touching.Keys.Where(t => t.AttachadMouse != null)
                .Concat(Hitting.Keys.Where(t => t.AttachadMouse != null)).Distinct().ToArray();

            MouseInteractionEventArgs MakeMouseArgs(Touch touch)
            {
                var args = new MouseInteractionEventArgs
                {
                    Touch = touch,
                    Touching = Touching.ContainsKey(touch),
                    Hitting = Hitting.ContainsKey(touch)
                };
                args.IntersectionPoint = args.Touching ? Touching[touch] : Hitting[touch];
                return args;
            }

            if (Mice.Length > 0)
            {
                var hscrolled = false;
                var vscrolled = false;
                var buttonpressed = false;
                var buttonreleased = false;

                foreach (var tmouse in Mice)
                {
                    if (tmouse.MouseDelta.AccumulatedWheelDelta != 0) vscrolled = true;
                    if (tmouse.MouseDelta.AccumulatedHorizontalWheelDelta != 0) hscrolled = true;
                    if (tmouse.MouseDelta.MouseClicks.Values.Any(mc => mc.ButtonDown)) buttonpressed = true;
                    if (tmouse.MouseDelta.MouseClicks.Values.Any(mc => mc.ButtonUp)) buttonreleased = true;
                }
                
                if (vscrolled)
                {
                    foreach (var touch in Mice.Where(mt => mt.MouseDelta.AccumulatedWheelDelta != 0))
                    {
                        OnVerticalMouseWheelChange?.Invoke(this, MakeMouseArgs(touch));
                    }
                }
                if (hscrolled)
                {
                    foreach (var touch in Mice.Where(mt => mt.MouseDelta.AccumulatedHorizontalWheelDelta != 0))
                    {
                        OnHorizontalMouseWheelChange?.Invoke(this, MakeMouseArgs(touch));
                    }
                }
                if (buttonpressed)
                {
                    foreach (var touch in Mice.Where(mt => mt.MouseDelta.MouseClicks.Values.Any(mc => mc.ButtonDown)))
                    {
                        var args = MakeMouseArgs(touch);
                        args.Buttons = MouseButtons.None;
                        foreach (var button in touch.MouseDelta.MouseClicks.Keys.Where(k => touch.MouseDelta.MouseClicks[k].ButtonDown))
                        {
                            args.Buttons = args.Buttons | button;
                        }
                        OnMouseButtonPressed?.Invoke(this, args);
                    }
                }
                if (buttonreleased)
                {
                    foreach (var touch in Mice.Where(mt => mt.MouseDelta.MouseClicks.Values.Any(mc => mc.ButtonUp)))
                    {
                        var args = MakeMouseArgs(touch);
                        args.Buttons = MouseButtons.None;
                        foreach (var button in touch.MouseDelta.MouseClicks.Keys.Where(k => touch.MouseDelta.MouseClicks[k].ButtonUp))
                        {
                            args.Buttons = args.Buttons | button;
                        }
                        OnMouseButtonReleased?.Invoke(this, args);
                    }
                }
            }

            if (TransformationFollowTime > 0)
            {
                DisplayTransformation.FollowWithDamper(TargetTransformation, TransformationFollowTime, Context.DeltaTime, Prototype.TransformApplication);
            }

            MainloopBeforeBehaviors();
            if(Touched) OnInteracting?.Invoke(this, EventArgs.Empty);
            foreach (var behavior in Behaviors)
            {
                behavior.Behave(this);
            }

            MainloopEnd();
            OnMainLoopEnd?.Invoke(this, EventArgs.Empty);
        }

        /// <inheritdoc />
        public virtual NotuiElement Copy()
        {
            var newprot = ElementPrototype.CreateFromInstance(this);
            var newinst = Parent == null ?
                Context.AddOrUpdateElements(false, newprot)[0] :
                Parent.UpdateChildren(false, newprot)[0];

            return newinst;
        }

        /// <summary>
        /// Notify the need for recomputing absolute world matrices
        /// </summary>
        public void InvalidateMatrices()
        {
            DisplayMatrixCached = false;
            foreach (var child in Children.Values)
                child.InvalidateMatrices();
        }

        /// <inheritdoc />
        public virtual void UpdateFrom(ElementPrototype other)
        {
            if(Dying) return;
            if (TransformationFollowTime > 0)
            {
                this.UpdateCommon(other);
                TargetTransformation.UpdateFrom(other.DisplayTransformation);
            }
            else this.UpdateCommon(other, other.TransformApplication);

            UpdateChildren(true, other.Children.Values.ToArray());
        }

        /// <summary>
        /// Base constructor awaiting an element prototype, a context to create element into and an optional parent element
        /// </summary>
        /// <param name="prototype"></param>
        /// <param name="context"></param>
        /// <param name="parent"></param>
        protected NotuiElement(ElementPrototype prototype, NotuiContext context, NotuiElement parent = null)
        {
            this.UpdateCommon(prototype, ApplyTransformMode.All);
            DisplayTransformation.UpdateFrom(prototype.DisplayTransformation);
            TargetTransformation.UpdateFrom(prototype.DisplayTransformation);
            //Value = prototype.Value?.Copy();
            Context = context;
            Parent = parent;

            EnvironmentObject = prototype.EnvironmentObject?.Copy();

            foreach (var child in prototype.Children.Values)
            {
                var newchild = child.Instantiate(context, this);
                Children.Add(child.Id, newchild);
            }

            if (AbsoluteFadeInDelay > 0.0f)
            {
                FadeInDelayTimer.SetTrigger(TimeSpan.FromSeconds(AbsoluteFadeInDelay));
                FadeInDelayTimer.OnTriggerPassed += (sender, args) => FadeInStopwatch.Start();
                FadeInDelayTimer.Start();
            }
            else FadeInStopwatch.Start();
            Age.Start();

            if (prototype.SubContextOptions != null)
            {
                SubContext = new SubContext(this, prototype.SubContextOptions);
            }
        }

        /// <summary>
        /// Override to modify the behavior how interaction should begin
        /// </summary>
        /// <param name="touch"></param>
        protected void FireInteractionTouchBegin(Touch touch)
        {
            if(touch.FramesSincePressed >= Context.ConsiderNewBefore) return;
            if(Touching.ContainsKey(touch)) return;
            if(!touch.Pressed) return;

            var eventargs = new TouchInteractionEventArgs { Touch = touch };
            if (Touching.Count == 0) OnInteractionBegin?.Invoke(this, eventargs);
            OnTouchBegin?.Invoke(this, eventargs);
            Touching.TryAdd(touch, Hovering[touch]);
        }

        /// <summary>
        /// Override to modify the behavior how interaction should end
        /// </summary>
        /// <param name="touch"></param>
        protected void FireInteractionEnd(Touch touch)
        {
            if (Touching.Count == 0) OnInteractionEnd?.Invoke(this, new TouchInteractionEventArgs
            {
                Touch = touch
            });
        }

        /// <summary>
        /// Override to modify the behavior how a touch should be released
        /// </summary>
        /// <param name="touch"></param>
        protected void FireTouchEnd(Touch touch)
        {
            if (!Touching.ContainsKey(touch)) return;
            Touching.TryRemove(touch, out var dummy);
            OnTouchEnd?.Invoke(this, new TouchInteractionEventArgs
            {
                Touch = touch
            });
            FireInteractionEnd(touch);
        }

        /// <inheritdoc />
        public object Clone() => Copy();

        /// <summary>
        /// Start the Dethklok or if FadeOutTime is 0 just set DeleteMe true and invoke OnDeleting
        /// </summary>
        public void StartDeletion()
        {
            if(Dying) return;
            foreach (var child in Children.Values)
            {
                child.StartDeletion();
            }
            if (AbsoluteFadeOutDelay > 0.0f)
            {
                FadeOutDelayTimer.SetTrigger(TimeSpan.FromSeconds(AbsoluteFadeOutDelay));
                FadeOutDelayTimer.OnTriggerPassed += (sender, args) =>
                {
                    Delete();
                };
                FadeOutDelayTimer.Start();
            }
            else Delete();
            Dying = true;
        }

        /// <summary>
        /// Override to modify how element should be deleted
        /// </summary>
        protected void Delete()
        {
            if (FadeOutTime > 0)
            {
                if (!Dethklok.IsRunning) OnDeletionStarted?.Invoke(this, EventArgs.Empty);
                Dethklok.Start();
            }
            else
            {
                ElementFade = 0;
                if (Age.Elapsed.TotalSeconds > Context.DeltaTime) OnDeleting?.Invoke(this, EventArgs.Empty);
                DeleteMe = true;
            }
        }
    }
}

