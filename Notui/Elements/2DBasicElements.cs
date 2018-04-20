using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using md.stdl.Interaction;
using md.stdl.Mathematics;

namespace Notui.Elements
{
    /// <inheritdoc />
    /// <summary>
    /// Base class for elements on a single plane defined by the element transforms
    /// </summary>
    public abstract class PlanarElement : NotuiElement
    {
        protected override void MainloopBeforeBehaviors()
        {
            foreach (var touch in Touching.Keys)
            {
                Touching[touch] = PreparePlanarShapeHitTest(touch);
            }
        }

        /// <summary>
        /// General Hittesting on the infinite plane defined by the element transforms
        /// </summary>
        /// <param name="touch">The touch to be tested</param>
        /// <returns>If the touch hits then an Intersection point otherwise null</returns>
        public IntersectionPoint PreparePlanarShapeHitTest(Touch touch)
        {
            // when first hit consider the display transformation then
            // for the rest of the interaction consider the interaction transform
            var hit = Intersections.PlaneRay(
                touch.WorldPosition,
                touch.ViewDir,
                DisplayMatrix,
                out var ispoint,
                out var planarpoint);
            return hit ? new IntersectionPoint(ispoint, planarpoint, this) : null;
        }

        protected PlanarElement(ElementPrototype prototype, NotuiContext context, NotuiElement parent = null) :
            base(prototype, context, parent) { }
    }

    /// <inheritdoc />
    /// <summary>
    /// Planar rectangle element prototype
    /// </summary>
    public class RectangleElementPrototype : ElementPrototype
    {
        public RectangleElementPrototype(string id = null, ElementPrototype parent = null) :
            base(typeof(RectangleElement), id, parent) { }

        public RectangleElementPrototype(NotuiElement fromInstance, bool newId = true) : base(fromInstance, newId) { }
    }
    /// <inheritdoc />
    /// <summary>
    /// Planar rectangle element instance
    /// </summary>
    public class RectangleElement : PlanarElement
    {
        public override IntersectionPoint HitTest(Touch touch)
        {
            var intersection = PreparePlanarShapeHitTest(touch);
            var phit = intersection != null;
            if (!phit) return null;
            var hit = intersection.ElementSpace.X <= 0.5 && intersection.ElementSpace.X >= -0.5 &&
                      intersection.ElementSpace.Y <= 0.5 && intersection.ElementSpace.Y >= -0.5;
            return hit ? intersection : null;
        }

        public RectangleElement(ElementPrototype prototype, NotuiContext context, NotuiElement parent = null) :
            base(prototype, context, parent) { }
    }

    /// <inheritdoc />
    /// <summary>
    /// Planar circle element prototype
    /// </summary>
    public class CircleElementPrototype : ElementPrototype
    {
        public CircleElementPrototype(string id = null, ElementPrototype parent = null) :
            base(typeof(CircleElement), id, parent)
        { }

        public CircleElementPrototype(NotuiElement fromInstance, bool newId = true) : base(fromInstance, newId) { }
    }
    /// <inheritdoc />
    /// <summary>
    /// Planar circle element instance
    /// </summary>
    public class CircleElement : PlanarElement
    {
        public override IntersectionPoint HitTest(Touch touch)
        {
            var intersection = PreparePlanarShapeHitTest(touch);
            var phit = intersection != null;
            if (!phit) return null;
            return intersection.ElementSpace.xy().Length() < 0.5 ? intersection : null;
        }

        public CircleElement(ElementPrototype prototype, NotuiContext context, NotuiElement parent = null) :
            base(prototype, context, parent) { }
    }

    /// <inheritdoc />
    /// <summary>
    /// Planar circular segment element prototype
    /// </summary>
    public class SegmentElementPrototype : ElementPrototype
    {
        public float HoleRadius { get; set; } = 0;
        public float Cycles { get; set; } = 1;

        public SegmentElementPrototype(string id = null, ElementPrototype parent = null) :
            base(typeof(SegmentElement), id, parent)
        { }

        public SegmentElementPrototype(NotuiElement fromInstance, bool newId = true) : base(fromInstance, newId) { }

        public override void UpdateFrom(ElementPrototype other)
        {
            base.UpdateFrom(other);
            if (other is SegmentElementPrototype prot)
            {
                HoleRadius = prot.HoleRadius;
                Cycles = prot.Cycles;
            }
        }

        public override void UpdateFrom(NotuiElement other)
        {
            base.UpdateFrom(other);
            if (other is SegmentElement element)
            {
                HoleRadius = element.HoleRadius;
                Cycles = element.Cycles;
            }
        }
    }

    /// <inheritdoc />
    /// <summary>
    /// Planar circular segment element instance
    /// </summary>
    public class SegmentElement : PlanarElement
    {
        public float HoleRadius { get; set; } = 0;
        public float Cycles { get; set; } = 1;
        public float Phase { get; set; } = 0;

        public override IntersectionPoint HitTest(Touch touch)
        {
            var intersection = PreparePlanarShapeHitTest(touch);
            var phit = intersection != null;
            if (!phit) return null;
            var polar = Coordinates.RectToPolar(intersection.ElementSpace.xy());
            polar.X = (float)Math.PI + polar.X + Phase * (float)Math.PI*2;
            var hit = polar.Y * 2 < 1 && polar.Y * 2 >= HoleRadius && (polar.X + Math.PI) % (Math.PI * 2) <= (Cycles * Math.PI * 2);
            return hit ? intersection : null;
        }

        public SegmentElement(ElementPrototype prototype, NotuiContext context, NotuiElement parent = null) :
            base(prototype, context, parent)
        {
            if (prototype is SegmentElementPrototype seprot)
            {
                HoleRadius = seprot.HoleRadius;
                Cycles = seprot.Cycles;
            }
        }

        public override void UpdateFrom(ElementPrototype other)
        {
            base.UpdateFrom(other);
            if (other is SegmentElementPrototype prot)
            {
                HoleRadius = prot.HoleRadius;
                Cycles = prot.Cycles;
            }
        }
    }

    /// <inheritdoc />
    /// <summary>
    /// Planar arbitrary polygon element
    /// </summary>
    public class PolygonElementPrototype : ElementPrototype
    {
        public List<Vector2> Vertices { get; private set; } = new List<Vector2>();

        public PolygonElementPrototype(string id = null, ElementPrototype parent = null) :
            base(typeof(SegmentElement), id, parent)
        { }

        public PolygonElementPrototype(NotuiElement fromInstance, bool newId = true) : base(fromInstance, newId) { }

        public override void UpdateFrom(ElementPrototype other)
        {
            base.UpdateFrom(other);
            if (other is PolygonElementPrototype prot)
            {
                Vertices = prot.Vertices.ToList();
            }
        }

        public override void UpdateFrom(NotuiElement other)
        {
            base.UpdateFrom(other);
            if (other is PolygonElement element)
            {
                Vertices = element.Vertices.ToList();
            }
        }
    }

    /// <inheritdoc />
    /// <summary>
    /// Planar arbitrary polygon element instance
    /// </summary>
    public class PolygonElement : PlanarElement
    {
        public List<Vector2> Vertices { get; private set; } = new List<Vector2>();

        public override IntersectionPoint HitTest(Touch touch)
        {
            if (Vertices.Count < 3)
                return null;

            var intersection = PreparePlanarShapeHitTest(touch);
            var phit = intersection != null;
            if (!phit) return null;

            var vold = Vertices[Vertices.Count - 1];
            var vt = intersection.ElementSpace.xy();
            var hit = false;
            foreach (var vnew in Vertices)
            {
                Vector2 v1, v2;
                if (vnew.X > vold.X)
                {
                    v1 = vold;
                    v2 = vnew;
                }
                else
                {
                    v1 = vnew;
                    v2 = vold;
                }

                if ((vnew.X < vt.X == vt.X <= vold.X) && (vt.Y - v1.Y) * (v2.X - v1.X) < (v2.Y - v1.Y) * (vt.X - v1.X))
                {
                    hit = !hit;
                }
                vold = vnew;
            }
            return hit ? intersection : null;
        }

        public PolygonElement(ElementPrototype prototype, NotuiContext context, NotuiElement parent = null) :
            base(prototype, context, parent)
        {
            if (prototype is PolygonElementPrototype prot)
            {
                Vertices = prot.Vertices.ToList();
            }
        }

        public override void UpdateFrom(ElementPrototype other)
        {
            base.UpdateFrom(other);
            if (other is PolygonElementPrototype prot)
            {
                Vertices = prot.Vertices.ToList();
            }
        }
    }
}
