using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace Notui
{
    /// <summary>
    /// Data about a touch ray and an element intersection
    /// </summary>
    public class IntersectionPoint
    {
        /// <summary>
        /// Intersection point in absolute world space
        /// </summary>
        public Vector3 WorldSpace { get; set; }

        /// <summary>
        /// Intersection point in element/object space
        /// </summary>
        public Vector3 ElementSpace { get; set; }

        /// <summary>
        /// Intersection point in the element's surface or UV space
        /// </summary>
        public Vector3 SurfaceSpace { get; set; }

        /// <summary>
        /// The element in question
        /// </summary>
        public NotuiElement Element { get; }

        /// <summary>
        /// The touch in question
        /// </summary>
        public Touch Touch { get; }

        /// <summary>
        /// Absolute world space transformation
        /// </summary>
        public Matrix4x4 WorldTransform => Matrix4x4.CreateTranslation(ElementSpace) * Element.DisplayMatrix;

        /// <summary>
        /// Absolute world space transformation representing the element's surface tangents
        /// </summary>
        public Matrix4x4 WorldSurfaceTangentTransform { get; set; }

        /// <summary>
        /// Element/object space transformation representing the element's surface tangents
        /// </summary>
        public Matrix4x4 ElementSurfaceTangentTransform => WorldSurfaceTangentTransform * Element.InverseDisplayMatrix;

        public IntersectionPoint(Vector3 wpos, Vector3 epos, Vector3 spos, Matrix4x4 str, NotuiElement element, Touch touch)
        {
            WorldSpace = wpos;
            ElementSpace = epos;
            SurfaceSpace = spos;
            WorldSurfaceTangentTransform = str;
            Element = element;
            Touch = touch;
        }

        public IntersectionPoint(Vector3 wpos, Vector3 epos, NotuiElement element, Touch touch)
            : this(wpos, epos, epos, Matrix4x4.CreateTranslation(epos) * element.DisplayMatrix, element, touch)
        { }
    }
}
