using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using md.stdl.Coding;
using md.stdl.Interaction;
using md.stdl.Interfaces;
using md.stdl.Mathematics;

namespace Notui
{
    /// <inheritdoc cref="IMainlooping"/>
    /// <summary>
    /// Notui Context to manage GuiElements and Touches
    /// </summary>
    public class NotuiContext : IMainlooping
    {
        /// <summary>
        /// Use PLINQ or not?
        /// </summary>
        public bool UseParallel { get; set; } = true;
        /// <summary>
        /// Consider touches to be new before the age of this amount of frames
        /// </summary>
        public int ConsiderNewBefore { get; set; } = 1;
        /// <summary>
        /// Ignore and delete touches older than this amount of frames
        /// </summary>
        public int ConsiderReleasedAfter { get; set; } = 1;
        /// <summary>
        /// To consider a touch minimum this amount of force have to be applied
        /// </summary>
        public float MinimumForce { get; set; } = -1.0f;

        /// <summary>
        /// Optional camera view matrix
        /// </summary>
        public Matrix4x4 View { get; set; } = Matrix4x4.Identity;
        /// <summary>
        /// Optional camera projection matrix
        /// </summary>
        public Matrix4x4 Projection { get; set; } = Matrix4x4.Identity;
        /// <summary>
        /// Very optional aspect ratio correction matrix a'la vvvv
        /// </summary>
        public Matrix4x4 AspectRatio { get; set; } = Matrix4x4.Identity;
        
        /// <summary>
        /// Inverse of view transform
        /// </summary>
        public Matrix4x4 ViewInverse { get; private set; } = Matrix4x4.Identity;
        /// <summary>
        /// Inverse of projection transform combined with aspect ratio
        /// </summary>
        public Matrix4x4 ProjectionWithAspectRatioInverse { get; private set; } = Matrix4x4.Identity;
        /// <summary>
        /// Projection transform combined with aspect ratio
        /// </summary>
        public Matrix4x4 ProjectionWithAspectRatio { get; private set; } = Matrix4x4.Identity;
        /// <summary>
        /// Camera Position in world
        /// </summary>
        public Vector3 ViewPosition { get; private set; } = Vector3.Zero;
        /// <summary>
        /// Camera view direction in world
        /// </summary>
        public Vector3 ViewDirection { get; private set; } = Vector3.UnitZ;
        /// <summary>
        /// Camera view orientation in world
        /// </summary>
        public Quaternion ViewOrientation { get; private set; } = Quaternion.Identity;
        /// <summary>
        /// Delta time between mainloop calls in seconds
        /// </summary>
        /// <remarks>
        /// This is provided by the implementer in the Mainloop args
        /// </remarks>
        public float DeltaTime { get; private set; } = 0;

        /// <summary>
        /// All the touches in this context
        /// </summary>
        public ConcurrentDictionary<int, TouchContainer<NotuiElement[]>> Touches { get; } =
            new ConcurrentDictionary<int, TouchContainer<NotuiElement[]>>();

        /// <summary>
        /// Elements in this context without a parent (or Root elements)
        /// </summary>
        public Dictionary<string, NotuiElement> RootElements { get; } = new Dictionary<string, NotuiElement>();

        /// <summary>
        /// All the elements in this context including the children of the root elements recursively
        /// </summary>
        public List<NotuiElement> FlatElements { get; } = new List<NotuiElement>();
        
        private readonly List<(Vector2 point, int id, float force)> _inputTouches = new List<(Vector2 point, int id, float force)>();
        
        public event EventHandler OnMainLoopBegin;
        public event EventHandler OnMainLoopEnd;

        public void SubmitTouches(IEnumerable<(Vector2, int, float)> touches)
        {
            _inputTouches.Clear();
            _inputTouches.AddRange(touches);
        }
        
        public void Mainloop(float deltatime)
        {
            OnMainLoopBegin?.Invoke(this, EventArgs.Empty);

            Matrix4x4.Invert(AspectRatio, out var invasp);
            //Matrix4x4.Invert(Projection, out var invproj);
            Matrix4x4.Invert(View, out var invview);
            var aspproj = Projection * invasp;
            Matrix4x4.Invert(aspproj, out var invaspproj);

            ViewInverse = invview;
            ProjectionWithAspectRatio = aspproj;
            ProjectionWithAspectRatioInverse = invaspproj;
            DeltaTime = deltatime;

            Matrix4x4.Decompose(invview, out var vscale, out var vquat, out var vpos);
            ViewOrientation = vquat;
            ViewPosition = vpos;
            ViewDirection = Vector3.Normalize(Vector3.TransformNormal(Vector3.UnitZ, View));

            // Removing expired touches
            var removabletouches = (from touch in Touches.Values
                where touch.ExpireFrames > ConsiderReleasedAfter
                select touch.Id).ToArray();
            foreach (var tid in removabletouches)
            {
                Touches.TryRemove(tid, out var dummy);
            }
            
            Touches.Values.ForEach(touch =>
            {
                touch.Mainloop(deltatime);
                touch.AttachedObject = null;
            });

            // Scan through elements if any of them wants to be killed or if there are new ones
            bool rebuild = false;
            if (_elementsDeleted)
            {
                foreach (var element in FlatElements)
                {
                    if (!element.DeleteMe) continue;
                    if (element.Parent == null) RootElements.Remove(element.Id);
                    else element.Parent.Children.Remove(element.Id);
                }
                rebuild = true;
                _elementsDeleted = false;
                OnElementsDeleted?.Invoke(this, EventArgs.Empty);
            }
            if (_elementsUpdated)
            {
                rebuild = true;
                _elementsUpdated = false;
                OnElementsUpdated?.Invoke(this, EventArgs.Empty);
            }
            if(rebuild) BuildFlatList();

            // Process input touches
            foreach (var touch in _inputTouches)
            {
                TouchContainer<NotuiElement[]> tt;
                if (Touches.ContainsKey(touch.id))
                {
                    tt = Touches[touch.id];
                }
                else
                {
                    tt = new TouchContainer<NotuiElement[]>(touch.id) { Force = touch.force };
                    Touches.TryAdd(tt.Id, tt);
                }
                tt.Update(touch.point, deltatime);
            }

            // preparing elements for hittest
            foreach (var element in FlatElements)
            {
                element.Hovering.Clear();
            }

            // look at which touches hit which element
            void ProcessTouches(TouchContainer<NotuiElement[]> touch)
            {
                // Transform touches into world
                Coordinates.GetPointWorldPosDir(touch.Point, invaspproj, invview, out var tpw, out var tpd);
                touch.WorldPosition = tpw;
                touch.ViewDir = tpd;

                // get hitting intersections and order them from closest to furthest
                var intersections = FlatElements.Select(el =>
                    {
                        var intersection = el.HitTest(touch);
                        if (intersection != null) intersection.Element = el;
                        return intersection;
                    })
                    .Where(insec => insec != null)
                    .Where(insec => insec.Element.Active)
                    .OrderBy(insec =>
                    {
                        var screenpos = Vector4.Transform(new Vector4(insec.WorldSpace, 1), View * aspproj);
                        return screenpos.Z / screenpos.W;
                    });

                // Sift through ordered intersection list until the furthest non-transparent element
                // or in other words ignore all intersected elements which are further away from the closest non-transparent element
                var passedintersections = GetTopInteractableElements(intersections);

                // Add the touch and the corresponding intersection point to the interacting elements
                // and attach those elements to the touch too.
                touch.AttachedObject = passedintersections.Select(insec =>
                {
                    insec.Element.Hovering.TryAdd(touch, insec);
                    return insec.Element;
                }).ToArray();

            }
            if(UseParallel) Touches.Values.AsParallel().ForAll(ProcessTouches);
            else Touches.Values.ForEach(ProcessTouches);

            // Do element logic
            void ProcessElements(NotuiElement el)
            {
                foreach (var touch in Touches.Values)
                {
                    el.ProcessTouch(touch);
                }
                el.Mainloop(deltatime);
            }
            if(UseParallel) FlatElements.AsParallel().ForAll(ProcessElements);
            else FlatElements.ForEach(ProcessElements);

            OnMainLoopEnd?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Instantiate new elements and update existing elements from the input prototypes. Optionally start the deletion of elements which are not present in the input array.
        /// </summary>
        /// <param name="removeNotPresent">When true elements will be deleted if their prototype with the same ID is not found in the input array</param>
        /// <param name="elements">Input prototypes</param>
        /// <returns>List of the newly instantiated elements</returns>
        public List<NotuiElement> AddOrUpdateElements(bool removeNotPresent, params ElementPrototype[] elements)
        {
            var newelements = new List<NotuiElement>();
            if (removeNotPresent)
            {
                var removables = (from el in RootElements.Values where elements.All(c => c.Id != el.Id) select el).ToArray();
                foreach (var el in removables)
                {
                    el.StartDeletion();
                }
            }

            foreach (var el in elements)
            {
                if (RootElements.ContainsKey(el.Id))
                    RootElements[el.Id].UpdateFrom(el);
                else
                {
                    var elinst = el.Instantiate(this);
                    RootElements.Add(el.Id, elinst);
                    newelements.Add(elinst);
                }
            }
            _elementsUpdated = true;
            return newelements;
        }

        private void BuildFlatList()
        {
            foreach (var element in FlatElements)
            {
                element.OnDeleting -= OnIndividualElementDeletion;
                element.OnChildrenUpdated -= OnIndividualElementUpdate;
            }
            FlatElements.Clear();
            foreach (var element in RootElements.Values)
            {
                element.FlattenElements(FlatElements);
            }

            foreach (var element in FlatElements)
            {
                element.OnDeleting += OnIndividualElementDeletion;
                element.OnChildrenUpdated += OnIndividualElementUpdate;
            }
        }

        private bool _elementsDeleted;
        private bool _elementsUpdated;

        private void OnIndividualElementUpdate(object sender, ChildrenUpdatedEventArgs childrenAddedEventArgs)
        {
            _elementsUpdated = true;
        }

        private void OnIndividualElementDeletion(object sender, EventArgs eventArgs)
        {
            _elementsDeleted = true;
        }

        private static IEnumerable<IntersectionPoint> GetTopInteractableElements(IEnumerable<IntersectionPoint> orderedhitinsecs)
        {
            if (orderedhitinsecs == null) yield break;

            foreach (var insec in orderedhitinsecs)
            {
                yield return insec;
                if (insec.Element.Transparent) continue;
                yield break;
            }
        }

        /// <summary>
        /// Fired when elements added or updated
        /// </summary>
        public event EventHandler OnElementsUpdated;

        /// <summary>
        /// Fired when elements got deleted
        /// </summary>
        public event EventHandler OnElementsDeleted;

        /// <summary>
        /// Get elements with Opaq (from RootElements)
        /// </summary>
        /// <param name="path">Opaq path</param>
        /// <param name="separator"></param>
        /// <param name="usename">If true it will use Element names, otherwise their ID</param>
        /// <returns></returns>
        public List<NotuiElement> Opaq(string path, string separator = "/", bool usename = true)
        {
            IEnumerable<NotuiElement> GetChildren(NotuiContext context, string k)
            {
                if (context.RootElements.Count == 0) return Enumerable.Empty<NotuiElement>();
                if (usename) return context.RootElements.Values.Where(c => c.Name == k);
                if (context.RootElements.ContainsKey(k)) return new[] { context.RootElements[k] };
                return Enumerable.Empty<NotuiElement>();
            }
            
            var children = new List<NotuiElement>();
            var nextpath = this.OpaqNonRecursive(path, children, children, separator,
                context => usename ? RootElements.Values.Select(el => el.Name) : RootElements.Keys,
                context => usename ? RootElements.Values.Select(el => el.Name) : RootElements.Keys,
                GetChildren, GetChildren
            );

            if (string.IsNullOrWhiteSpace(nextpath))
                return children;

            var results = new List<NotuiElement>();

            foreach (var child in children)
                results.AddRange(child.Opaq(nextpath, separator, usename));

            return results;
        }
    }
}
