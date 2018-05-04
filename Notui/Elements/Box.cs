using System;
using System.Collections.Generic;
using System.Drawing.Text;
using System.IO.IsolatedStorage;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using md.stdl.Mathematics;

namespace Notui.Elements
{
    /// <summary>
    /// Prototype for 3D box elements
    /// </summary>
    public class BoxElementPrototype : ElementPrototype
    {
        /// <summary>
        /// Size of the box on each axis
        /// </summary>
        public Vector3 Size { get; set; } = Vector3.One;

        /// <summary>
        /// Regular constructor
        /// </summary>
        /// <param name="id">If null generate a new ID with System.GUID</param>
        /// <param name="parent">Optional parent element if this prototype is a child</param>
        public BoxElementPrototype(string id = null, ElementPrototype parent = null) :
            base(typeof(BoxElement), id, parent)
        { }

        /// <summary>
        /// Construct based on an instance
        /// </summary>
        /// <param name="fromInstance">The element instance</param>
        /// <param name="newId">Generate a new ID?</param>
        /// <remarks>Static function ElementPrototype.CreateFromInstance(...) is recommended to be used instead of this</remarks>
        public BoxElementPrototype(NotuiElement fromInstance, bool newId = true) : base(fromInstance, newId) { }

        /// <inheritdoc cref="ElementPrototype"/>
        public override void UpdateFrom(ElementPrototype other)
        {
            base.UpdateFrom(other);
            if (other is BoxElementPrototype prot)
            {
                Size = prot.Size;
            }
        }

        /// <inheritdoc cref="ElementPrototype"/>
        public override void UpdateFrom(NotuiElement other)
        {
            base.UpdateFrom(other);
            if (other is BoxElement element)
            {
                Size = element.Size;
            }
        }
    }

    /// <summary>
    /// 3D box element
    /// </summary>
    public class BoxElement : NotuiElement
    {
        /// <summary>
        /// Size of the box on each axis
        /// </summary>
        public Vector3 Size { get; set; } = Vector3.One;
        private readonly Vector3[] _planeCenters =
        {
            new Vector3(1, 0, 0),
            new Vector3(0, 1, 0),
            new Vector3(0, 0, 1),
            new Vector3(-1, 0, 0),
            new Vector3(0, -1, 0),
            new Vector3(0, 0, -1)
        };
        private readonly Vector3[] _planeUps =
        {
            new Vector3(0, 1, 0),
            new Vector3(0, 0, 1),
            new Vector3(1, 0, 0),
            new Vector3(0, -1, 0),
            new Vector3(0, 0, -1),
            new Vector3(-1, 0, 0)
        };

        /// <inheritdoc cref="NotuiElement"/>
        public override IntersectionPoint PureHitTest(Touch touch, bool prevpos, out IntersectionPoint persistentIspoint)
        {
            touch.GetPreviousWorldPosition(Context, out var popos, out var pdir);
            var sizetr = Matrix4x4.CreateScale(Size);
            //var invsizetr = Matrix4x4.CreateScale(Vector3.One/Size);
            var scldisp = sizetr * DisplayMatrix;
            Matrix4x4.Invert(scldisp, out var invdispmat);
            var trelpos = Vector3.Transform(prevpos ? popos : touch.WorldPosition, invdispmat);
            var treldir = Vector3.TransformNormal(prevpos ? pdir : touch.ViewDir, invdispmat);

            IntersectionPoint ispoint = null;
            float d = float.MaxValue;
            
            for (int i = 0; i < 6; i++)
            {
                var pmat = Matrix4x4.CreateWorld(_planeCenters[i] * -0.5f, _planeCenters[i], _planeUps[i]);
                var hit = Intersections.PlaneRay(trelpos, treldir, pmat, out var aispos, out var pispos);
                if(!hit) continue;
                hit = pispos.X <= 0.5 && pispos.X >= -0.5 &&
                      pispos.Y <= 0.5 && pispos.Y >= -0.5;
                if(!hit) continue;
                var diff = aispos - trelpos;
                if (Vector3.Dot(Vector3.Normalize(diff), treldir) < 0) continue;
                if (diff.Length() >= d) continue;

                var locmat = pmat * scldisp;

                //TODO: create a more analitical method for scale correction, this is dumb
                Matrix4x4.Decompose(locmat, out var locscl, out var qdummy, out var tdummy);
                var invlocscl = Vector3.One / (locscl / DisplayTransformation.Scale);

                var smat = Matrix4x4.CreateScale(invlocscl) * Matrix4x4.CreateTranslation(pispos) * locmat;
                ispoint = new IntersectionPoint(Vector3.Transform(aispos, scldisp), aispos, pispos, smat, this, touch);
                d = diff.Length();
            }

            persistentIspoint = ispoint;
            return ispoint;
        }

        /// <inheritdoc cref="NotuiElement"/>
        public BoxElement(ElementPrototype prototype, NotuiContext context, NotuiElement parent = null) :
            base(prototype, context, parent)
        {
            if (prototype is BoxElementPrototype prot)
            {
                Size = prot.Size;
            }
        }

        /// <inheritdoc cref="NotuiElement"/>
        public override void UpdateFrom(ElementPrototype other)
        {
            base.UpdateFrom(other);
            if (other is BoxElementPrototype prot)
            {
                Size = prot.Size;
            }
        }
    }
}
