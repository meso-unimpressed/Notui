using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using md.stdl.Time;

namespace Notui
{
    /// <inheritdoc />
    /// <summary>
    /// Simple strongly typed wrapper for ICloneable
    /// </summary>
    /// <typeparam name="T">Any type</typeparam>
    public interface ICloneable<out T> : ICloneable
    {
        /// <summary>
        /// Clone the object with strong typing
        /// </summary>
        /// <returns>The clone of the original object</returns>
        T Copy();
    }
    
    /// <summary>
    /// Interface for updateable but cloneable objects
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public interface IUpdateable<in T>
    {
        /// <summary>
        /// Update data of this object from an other object
        /// </summary>
        /// <param name="other">The other instance to update from</param>
        void UpdateFrom(T other);
    }

    /// <inheritdoc cref="IUpdateable{T}" />
    /// <inheritdoc cref="ICloneable{T}" />
    /// <summary>
    /// Base class for auxiliary attached element object
    /// </summary>
    public abstract class AuxiliaryObject : ICloneable<AuxiliaryObject>, IUpdateable<AuxiliaryObject>
    {
        public abstract AuxiliaryObject Copy();
        public abstract void UpdateFrom(AuxiliaryObject other);
        public object Clone() => Copy();
    }

    /// <inheritdoc cref="IUpdateable{T}" />
    /// <inheritdoc cref="ICloneable{T}" />
    /// <summary>
    /// A general purpose parameter holder for any IGuiElement
    /// </summary>
    public class AttachedValues : ICloneable<AttachedValues>, IUpdateable<AttachedValues>
    {
        /// <summary>
        /// N axis float values
        /// </summary>
        public float[] Values = new float[1];

        /// <summary>
        /// N number of strings
        /// </summary>
        public string[] Texts = new string[1];

        /// <summary>
        /// Whatever you want as long as it's clonable
        /// </summary>
        public Dictionary<string, AuxiliaryObject> Auxiliary = new Dictionary<string, AuxiliaryObject>();

        public AttachedValues Copy()
        {
            var res = new AttachedValues
            {
                Values = Values.ToArray(),
                Texts = Texts.ToArray()
            };
            foreach (var kvp in Auxiliary)
            {
                res.Auxiliary.Add(kvp.Key, kvp.Value.Copy());
            }

            return res;
        }

        public object Clone()
        {
            return Copy();
        }

        public void UpdateFrom(AttachedValues other)
        {
            if(other == null) return;
            Values = other.Values.ToArray();
            Texts = other.Texts.ToArray();
            var removable = (from auxkey in Auxiliary.Keys where !other.Auxiliary.ContainsKey(auxkey) select auxkey).ToArray();

            foreach (var auxkey in removable)
            {
                Auxiliary.Remove(auxkey);
            }

            foreach (var auxval in other.Auxiliary)
            {
                if (Auxiliary.ContainsKey(auxval.Key))
                    Auxiliary[auxval.Key].UpdateFrom(auxval.Value);
                else Auxiliary.Add(auxval.Key, auxval.Value.Copy());
            }
        }
    }
    
    /// <summary>
    /// Interface for an element inside a Notui Context
    /// </summary>
    public interface IElementCommon
    {
        /// <summary>
        /// General name used for identification
        /// </summary>
        string Name { get; set; }

        /// <summary>
        /// A unique identifier used comparing elements
        /// </summary>
        string Id { get; set; }

        /// <summary>
        /// Is element reacting to touches?
        /// </summary>
        bool Active { get; set; }

        /// <summary>
        /// Whether this element blocks touches
        /// </summary>
        bool Transparent { get; set; }

        /// <summary>
        /// Delete this element after this amount of seconds passed in the Dethklok
        /// </summary>
        float FadeOutTime { get; set; }

        /// <summary>
        /// Seconds to fade in this element based on the Age
        /// </summary>
        float FadeInTime { get; set; }

        /// <summary>
        /// Transformation to work with during user manipulation like smoothed dragging. This is used for hittesting touches already interacting with this element.
        /// </summary>
        ElementTransformation InteractionTransformation { get; set; }

        /// <summary>
        /// Transformation to be used during the display of the element. This is used for hittesting newly interacting touches, and for the parent transformation for children.
        /// </summary>
        ElementTransformation DisplayTransformation { get; set; }
        
        /// <summary>
        /// Set of interaction behaviors assigned to this element executed in the order of from first to last
        /// </summary>
        List<InteractionBehavior> Behaviors { get; set; }

        /// <summary>
        /// Optional value to be manipulated with the element
        /// </summary>
        AttachedValues Value { get; set; }

        /// <summary>
        /// Optional environment specific object for implementations so Value is not polluted
        /// </summary>
        AuxiliaryObject EnvironmentObject { get; set; }
    }
}
