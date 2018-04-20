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
    public class BoxElementPrototype : ElementPrototype
    {
        public Vector3 Size { get; set; } = Vector3.One;
        public BoxElementPrototype(string id = null, ElementPrototype parent = null) :
            base(typeof(BoxElement), id, parent)
        { }

        public BoxElementPrototype(NotuiElement fromInstance, bool newId = true) : base(fromInstance, newId) { }

        public override void UpdateFrom(ElementPrototype other)
        {
            base.UpdateFrom(other);
            if (other is BoxElementPrototype prot)
            {
                Size = prot.Size;
            }
        }

        public override void UpdateFrom(NotuiElement other)
        {
            base.UpdateFrom(other);
            if (other is BoxElement element)
            {
                Size = element.Size;
            }
        }
    }

    public class BoxElement : NotuiElement
    {
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
        public override IntersectionPoint HitTest(Touch touch)
        {
            var sizetr = Matrix4x4.CreateScale(Size);
            //var invsizetr = Matrix4x4.CreateScale(Vector3.One/Size);
            var scldisp = Matrix4x4.Multiply(sizetr, DisplayMatrix);
            Matrix4x4.Invert(scldisp, out var invdispmat);
            var trelpos = Vector3.Transform(touch.WorldPosition, invdispmat);
            var treldir = Vector3.TransformNormal(touch.ViewDir, invdispmat);

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



                ispoint = new IntersectionPoint(Vector3.Transform(aispos, scldisp), aispos, this);
                ispoint.UseCustomMatrix = true;
                var locmat = pmat * scldisp;

                //TODO: create a more analitical method for scale correction, this is dumb
                Matrix4x4.Decompose(locmat, out var locscl, out var qdummy, out var tdummy);
                var invlocscl = Vector3.One / (locscl / DisplayTransformation.Scale);
                ispoint.CustomMatrix = Matrix4x4.CreateScale(invlocscl) * Matrix4x4.CreateTranslation(pispos) * locmat;
                d = diff.Length();
            }
            return ispoint;
        }

        public BoxElement(ElementPrototype prototype, NotuiContext context, NotuiElement parent = null) :
            base(prototype, context, parent)
        {
            if (prototype is BoxElementPrototype prot)
            {
                Size = prot.Size;
            }
        }

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
