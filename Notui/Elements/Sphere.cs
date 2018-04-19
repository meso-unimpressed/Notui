using System;
using System.Collections.Generic;
using System.IO.IsolatedStorage;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace Notui.Elements
{
    public class SphereElementPrototype : ElementPrototype
    {
        public SphereElementPrototype(string id = null, ElementPrototype parent = null) :
            base(typeof(SphereElement), id, parent)
        { }

        public SphereElementPrototype(NotuiElement fromInstance, bool newId = true) : base(fromInstance, newId) { }
    }

    public class SphereElement : NotuiElement
    {
        private bool SolveQuadratic(float a, float b, float c, out float x0, out float x1)
        {
            float discr = b * b - 4 * a * c;
            if (discr < 0)
            {
                x0 = 0; x1 = 0;
                return false;
            }
            else if (discr == 0) x0 = x1 = -0.5f * b / a;
            else
            {
                float q = (b > 0) ?
                    -0.5f * (b + (float)Math.Sqrt(discr)) :
                    -0.5f * (b - (float)Math.Sqrt(discr));
                x0 = q / a;
                x1 = c / q;
            }
            if (x0 > x1)
            {
                var tt = x0; x0 = x1; x1 = tt;
            }
            return true;
        }
        public override IntersectionPoint HitTest(Touch touch)
        {
            Matrix4x4.Invert(DisplayMatrix, out var invdispmat);
            var trelpos = Vector3.Transform(touch.WorldPosition, invdispmat);
            var treldir = Vector3.TransformNormal(touch.ViewDir, invdispmat);
            var L = touch.WorldPosition * -1;
            var a = Vector3.Dot(treldir, treldir);
            var b = 2 * Vector3.Dot(treldir, L);
            var c = Vector3.Dot(L, L) - 1;
            if (!SolveQuadratic(a, b, c, out var t0, out var t1)) return null;

            if (t0 > t1)
            {
                var tt = t0; t0 = t1; t1 = tt;
            }

            if (t0 < 0)
            {
                t0 = t1;
                if (t0 < 0) return null;
            }

            var rispos = treldir * t0 + trelpos;
            var aispos = Vector3.Transform(rispos, DisplayMatrix);
            var ispoint = new IntersectionPoint(aispos, rispos, this);

            var zd = Vector3.Normalize(rispos);
            var xd = Vector3.Cross(zd, Vector3.UnitY);
            var yd = Vector3.Cross(xd, zd);
            
            var ismat = Matrix4x4.CreateWorld(aispos, Vector3.TransformNormal(zd, DisplayMatrix), Vector3.TransformNormal(yd, DisplayMatrix));
            ispoint.CustomMatrix = ismat;
            ispoint.UseCustomMatrix = true;

            return ispoint;
        }

        public SphereElement(ElementPrototype prototype, NotuiContext context, NotuiElement parent = null) :
            base(prototype, context, parent)
        { }
    }
}
