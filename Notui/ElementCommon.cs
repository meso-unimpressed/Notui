using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using md.stdl.Coding;
using md.stdl.Interfaces;
using md.stdl.Time;

namespace Notui
{
    /// <summary>
    /// Base interface for auxiliary attached element object
    /// </summary>
    public interface IAuxiliaryObject : ICloneable<IAuxiliaryObject>, IUpdateable<IAuxiliaryObject> { }

    /// <summary>
    /// Base class for auxiliary attached element object
    /// </summary>
    public abstract class AuxiliaryObject : IAuxiliaryObject
    {
        /// <inheritdoc cref="ICloneable{T}"/>
        public abstract IAuxiliaryObject Copy();
        /// <inheritdoc cref="IUpdateable{T}"/>
        public abstract void UpdateFrom(IAuxiliaryObject other);
        /// <inheritdoc cref="ICloneable"/>
        public object Clone() => Copy();
    }
    
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
        public readonly Dictionary<string, IAuxiliaryObject> Auxiliary = new Dictionary<string, IAuxiliaryObject>();

        /// <inheritdoc cref="ICloneable{T}"/>
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

        /// <inheritdoc cref="ICloneable"/>
        public object Clone()
        {
            return Copy();
        }

        /// <inheritdoc cref="IUpdateable{T}"/>
        public void UpdateFrom(AttachedValues other)
        {
            if(other == null) return;
            Values.Fill(other.Values);
            Texts.Fill(other.Texts);

            Auxiliary.Clear();
            foreach (var auxval in other.Auxiliary)
            {
                Auxiliary.Add(auxval.Key, auxval.Value);
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
        /// Delay fading out by this amount of seconds
        /// </summary>
        /// <remarks>Good for creating staggering effects</remarks>
        float FadeOutDelay { get; set; }

        /// <summary>
        /// Delay fading in by this amount of seconds
        /// </summary>
        /// <remarks>Good for creating staggering effects</remarks>
        float FadeInDelay { get; set; }

        /// <summary>
        /// How much time it should take for an instance to follow its prototype transform
        /// </summary>
        float TransformationFollowTime { get; set; }

        /// <summary>
        /// Transformation of this element. This is used for hittesting and for the parent transformation of children.
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
        IAuxiliaryObject EnvironmentObject { get; set; }

        /// <summary>
        /// Hittest is only successful if parent hittest is also successful
        /// </summary>
        bool OnlyHitIfParentIsHit { get; set; }
    }
}
