using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using md.stdl.Coding;
using md.stdl.Interaction;
using md.stdl.Mathematics;

namespace Notui
{
    public static class NotuiExtensions
    {
        /// <summary>
        /// Translate transformation with a delta
        /// </summary>
        /// <param name="tr"></param>
        /// <param name="diff">Delta</param>
        public static void Translate(this ElementTransformation tr, Vector3 diff)
        {
            tr.Position += diff;
        }

        /// <summary>
        /// Resize transformation with a delta
        /// </summary>
        /// <param name="tr"></param>
        /// <param name="diff">Delta</param>
        public static void Resize(this ElementTransformation tr, Vector3 diff)
        {
            tr.Scale += diff;
        }

        /// <summary>
        /// Rotate transformation with global delta pitch yaw roll
        /// </summary>
        /// <param name="tr"></param>
        /// <param name="dPitchYawRoll">Delta pitch yaw roll</param>
        public static void GlobalRotate(this ElementTransformation tr, Vector3 dPitchYawRoll)
        {
            tr.Rotation = Quaternion.Normalize(tr.Rotation * Quaternion.CreateFromYawPitchRoll(dPitchYawRoll.Y, dPitchYawRoll.X, dPitchYawRoll.Z));
        }

        /// <summary>
        /// Rotate transformation with local delta pitch yaw roll
        /// </summary>
        /// <param name="tr"></param>
        /// <param name="dPitchYawRoll">Delta pitch yaw roll</param>
        public static void LocalRotate(this ElementTransformation tr, Vector3 dPitchYawRoll)
        {
            tr.Rotation = Quaternion.Normalize(Quaternion.CreateFromYawPitchRoll(dPitchYawRoll.Y, dPitchYawRoll.X, dPitchYawRoll.Z) * tr.Rotation);
        }

        /// <summary>
        /// Rotate transformation with global delta quaternion
        /// </summary>
        /// <param name="tr"></param>
        /// <param name="q">Delta quaternion</param>
        public static void GlobalRotate(this ElementTransformation tr, Quaternion q)
        {
            tr.Rotation = Quaternion.Normalize(tr.Rotation * q);
        }

        /// <summary>
        /// Rotate transformation with local delta pitch yaw roll
        /// </summary>
        /// <param name="tr"></param>
        /// <param name="q">Delta quaternion</param>
        public static void LocalRotate(this ElementTransformation tr, Quaternion q)
        {
            tr.Rotation = Quaternion.Normalize(q * tr.Rotation);
        }

        /// <summary>
        /// Update an IElementCommon from another IElementCommon with transformation
        /// </summary>
        /// <param name="element">Receiving element, Can be a prototype or an instance</param>
        /// <param name="prototype">Reference element, Can be a prototype or an instance</param>
        /// <param name="selectivetr"></param>
        public static void UpdateCommon(this IElementCommon element, IElementCommon prototype, ApplyTransformMode selectivetr)
        {
            UpdateCommon(element, prototype);
            if (element is NotuiElement elinst)
            {
                if (prototype is NotuiElement el)
                    el.TargetTransformation.UpdateFrom(elinst.TargetTransformation, selectivetr);
            }
            element.DisplayTransformation.UpdateFrom(prototype.DisplayTransformation, selectivetr);
        }

        /// <summary>
        /// Update an IElementCommon from another IElementCommon without transformation
        /// </summary>
        /// <param name="element">Receiving element, Can be a prototype or an instance</param>
        /// <param name="prototype">Reference element, Can be a prototype or an instance</param>
        /// <param name="selectivetr"></param>
        public static void UpdateCommon(this IElementCommon element, IElementCommon prototype)
        {
            element.Id = prototype.Id;

            if (element is NotuiElement elinst)
            {
                if (prototype is ElementPrototype prot) elinst.Prototype = prot;
                if (prototype is NotuiElement el) elinst.Prototype = el.Prototype;
            }

            element.Name = prototype.Name;
            element.Active = prototype.Active;
            element.Transparent = prototype.Transparent;
            element.FadeOutTime = prototype.FadeOutTime;
            element.FadeOutDelay = prototype.FadeOutDelay;
            element.FadeInTime = prototype.FadeInTime;
            element.FadeInDelay = prototype.FadeInDelay;
            element.TransformationFollowTime = prototype.TransformationFollowTime;
            element.Behaviors = prototype.Behaviors;
        }

        /// <summary>
        /// Get the planar velocity of a touch and its current and previous intersection-point in the selected plane's space
        /// </summary>
        /// <param name="touch"></param>
        /// <param name="plane">Arbitrary matrix of the XY plane to do the intersection with</param>
        /// <param name="context">Notui context to provide screen space/alignment information</param>
        /// <param name="currpos">Current intersection point in the space of the plane</param>
        /// <param name="prevpos">Previous intersection point in the space of the plane</param>
        /// <returns>Velocity of the touch relative to the space of the plane</returns>
        public static Vector3 GetPlanarVelocity(this TouchContainer touch, Matrix4x4 plane, NotuiContext context, out Vector3 currpos, out Vector3 prevpos)
        {
            // get planar coords for current touch position
            var hit = Intersections.PlaneRay(touch.WorldPosition, touch.ViewDir, plane, out var capos, out var crpos);
            currpos = crpos;

            // get planar coords for the previous touch position
            var prevpoint = touch.Point - touch.Velocity;
            Coordinates.GetPointWorldPosDir(prevpoint, context.ProjectionWithAspectRatioInverse, context.ViewInverse, out var popos, out var pdir);
            var phit = Intersections.PlaneRay(popos, pdir, plane, out var papos, out var prpos);
            prevpos = prpos;
            return crpos - prpos;
        }

        public static List<NotuiElement> Opaq(this NotuiElement element, string path, string separator = "/", bool useName = true)
        {
            IEnumerable<NotuiElement> GetChildren(NotuiElement el, string k)
            {
                if(el.Children.Count == 0) return Enumerable.Empty<NotuiElement>();
                if (useName) return el.Children.Values.Where(c => c.Name == k);
                if (el.Children.ContainsKey(k)) return new[] { el.Children[k] };
                return Enumerable.Empty<NotuiElement>();
            }
            return element.Opaq(path, separator,
                el => useName ? el.Children.Values.Select(c => c.Name) : el.Children.Keys,
                el => useName ? el.Children.Values.Select(c => c.Name) : el.Children.Keys,
                GetChildren, GetChildren
            );
        }

        public static List<ElementPrototype> Opaq(this ElementPrototype element, string path, string separator = "/", bool useName = true)
        {
            IEnumerable<ElementPrototype> GetChildren(ElementPrototype el, string k)
            {
                if (el.Children.Count == 0) return Enumerable.Empty<ElementPrototype>();
                if (useName) return el.Children.Values.Where(c => c.Name == k);
                if (el.Children.ContainsKey(k)) return new[] { el.Children[k] };
                return Enumerable.Empty<ElementPrototype>();
            }
            return element.Opaq(path, separator,
                el => useName ? el.Children.Values.Select(c => c.Name) : el.Children.Keys,
                el => useName ? el.Children.Values.Select(c => c.Name) : el.Children.Keys,
                GetChildren, GetChildren
            );
        }
    }
}
