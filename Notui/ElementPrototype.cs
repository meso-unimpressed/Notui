using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Reflection;
using md.stdl.Coding;
using md.stdl.Time;

namespace Notui
{
    /// <inheritdoc cref="ICloneable{T}" />
    /// <inheritdoc cref="IElementCommon"/>
    /// <summary>
    /// A stateless record class serving as schematic for creating the actual Notui elements
    /// </summary>
    public class ElementPrototype : IElementCommon, ICloneable<ElementPrototype>
    {
        public string Name { get; set; }
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public bool Active { get; set; }
        public bool Transparent { get; set; }
        public float FadeOutTime { get; set; }
        public float FadeInTime { get; set; }
        public ElementTransformation InteractionTransformation { get; set; } = new ElementTransformation();
        public ElementTransformation DisplayTransformation { get; set; } = new ElementTransformation();
        public List<InteractionBehavior> Behaviors { get; set; } = new List<InteractionBehavior>();
        public AttachedValues Value { get; set; }
        public AuxiliaryObject EnvironmentObject { get; set; }

        /// <summary>
        /// Selectively apply transform components. Default is All
        /// </summary>
        public ApplyTransformMode TransformApplication { get; set; } = ApplyTransformMode.All;

        /// <summary>
        /// The element this element inherits its transformation from. Null if this element is directly in a context.
        /// </summary>
        public ElementPrototype Parent { get; set; }

        /// <summary>
        /// Elements which will inherit the transformation of this element
        /// </summary>
        public Dictionary<string, ElementPrototype> Children { get; } = new Dictionary<string, ElementPrototype>();

        /// <summary>
        /// The type of the instances which are created from this prototype
        /// </summary>
        public Type InstanceType { get; }

        /// <summary>
        /// Base constructor
        /// </summary>
        /// <param name="insttype">The type of the element instances which this prototype will instantiate</param>
        /// <param name="id">Optional external unique Id. Use carefully!</param>
        public ElementPrototype(Type insttype, string id = null, ElementPrototype parent = null)
        {
            if (!insttype.Is(typeof(NotuiElement)))
            {
                throw new Exception("Element type is not inherited from NotuiElement");
            }

            if (id != null) Id = id;
            Parent = parent;
            InstanceType = insttype;
        }

        /// <summary>
        /// Create a new prototype from an existing element instance
        /// </summary>
        /// <param name="fromInstance">The instance which should be copied</param>
        /// <param name="newId">If the new prototype should have a new Id generated</param>
        /// <remarks>
        /// It is strongly recommended to generate a new Id instead of copying it over from the other instance. This constructor should be only used if you are desperate.
        /// </remarks>
        public ElementPrototype(NotuiElement fromInstance, bool newId = true)
        {
            InstanceType = fromInstance.GetType();
            this.UpdateCommon(fromInstance, ApplyTransformMode.All);

            Value = fromInstance.Value?.Copy();
            EnvironmentObject = fromInstance.EnvironmentObject.Copy();
            Parent = fromInstance.Parent.Prototype;

            if (newId) Id = Guid.NewGuid().ToString();

            foreach (var child in fromInstance.Children.Values)
            {
                Children.Add(child.Id, new ElementPrototype(child, newId));
            }
        }

        /// <summary>
        /// Get the constructor of the element instance type
        /// </summary>
        /// <returns></returns>
        public ConstructorInfo GetElementConstructor()
        {
            return InstanceType.GetConstructor(new[]
            {
                typeof(ElementPrototype),
                typeof(NotuiContext),
                typeof(NotuiElement)
            });
        }

        /// <summary>
        /// Create a NotuiElement instance out of this prototype
        /// </summary>
        /// <param name="context">The context to instantiate into</param>
        /// <param name="parent">An optional parent</param>
        /// <returns></returns>
        public NotuiElement Instantiate(NotuiContext context, NotuiElement parent = null)
        {
            return (NotuiElement)GetElementConstructor().Invoke(new object[] { this, context, parent });
        }

        public ElementPrototype Copy()
        {
            var res = new ElementPrototype(InstanceType, Id, Parent);
            res.UpdateCommon(this, ApplyTransformMode.All);
            return res;
        }

        public object Clone()
        {
            throw new NotImplementedException();
        }
    }
}

