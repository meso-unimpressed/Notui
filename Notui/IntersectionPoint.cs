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
        public readonly NotuiElement Element;

        /// <summary>
        /// The touch in question
        /// </summary>
        public readonly Touch Touch;

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

        /// <summary></summary>
        /// <param name="wpos">Intersection point in absolute world space</param>
        /// <param name="epos">Intersection point in element/object space</param>
        /// <param name="spos">Intersection point in the element's surface or UV space</param>
        /// <param name="str">Absolute world space transformation representing the element's surface tangents</param>
        /// <param name="element">The element in question</param>
        /// <param name="touch">The touch in question</param>
        public IntersectionPoint(Vector3 wpos, Vector3 epos, Vector3 spos, Matrix4x4 str, NotuiElement element, Touch touch)
        {
            WorldSpace = wpos;
            ElementSpace = epos;
            SurfaceSpace = spos;
            WorldSurfaceTangentTransform = str;
            Element = element;
            Touch = touch;
        }

        /// <inheritdoc />
        /// <summary></summary>
        /// <param name="wpos">Intersection point in absolute world space</param>
        /// <param name="epos">Intersection point in element/object space</param>
        /// <param name="element">The element in question</param>
        /// <param name="touch">The touch in question</param>
        public IntersectionPoint(Vector3 wpos, Vector3 epos, NotuiElement element, Touch touch)
            : this(wpos, epos, epos, Matrix4x4.CreateTranslation(epos) * element.DisplayMatrix, element, touch)
        { }

        /// <summary>
        /// Intersection equality only depends on the element and Touch equality. It makes it easier to organize them into Dictionaries and the like
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        public override bool Equals(object obj)
        {
            if (obj == null) return false;
            if (obj is IntersectionPoint ispoint)
            {
                return Element == ispoint.Element && Touch == ispoint.Touch;
            }
            else return false;
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            return Element.GetHashCode() ^ Touch.GetHashCode();
        }
    }
}
