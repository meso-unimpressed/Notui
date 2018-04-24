using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Reflection;
using md.stdl.Coding;
using md.stdl.Interfaces;
using md.stdl.Time;

namespace Notui
{
    public class NoSuchPrototypeConstructorFoundException : Exception
    {
        private string _instType;

        public override string Message => $"Couldn't find the right constructor for {_instType}.";

        public NoSuchPrototypeConstructorFoundException(Type t)
        {
            _instType = t.FullName;
        }
    }
    /// <inheritdoc cref="ICloneable{T}" />
    /// <inheritdoc cref="IElementCommon"/>
    /// <summary>
    /// A stateless record class serving as schematic for creating the actual Notui elements
    /// </summary>
    public abstract class ElementPrototype : IElementCommon, ICloneable<ElementPrototype>, IUpdateable<NotuiElement>, IUpdateable<ElementPrototype>
    {
        protected static ElementPrototype CreateFromPrototype(ElementPrototype element, bool newId = true)
        {
            var prottype = element.GetType();
            var constructor = prottype.GetConstructor(
                new[]
                {
                    typeof(string),
                    typeof(ElementPrototype)
                });
            if (constructor == null)
            {
                throw new NoSuchPrototypeConstructorFoundException(prottype);
            }
            else
            {
                return (ElementPrototype)constructor.Invoke(new object[] { newId ? null : element.Id, element.Parent });
            }
        }

        public static ElementPrototype CreateFromInstance(NotuiElement element, bool newId = true)
        {
            var prottype = element.Prototype.GetType();
            var res = (ElementPrototype)prottype.GetConstructor(
                new[]
                {
                    typeof(NotuiElement),
                    typeof(bool)
                })?
                .Invoke(new object[] {element, newId });
            if (res == null)
            {
                throw new NoSuchPrototypeConstructorFoundException(prottype);
            }
            else
            {
                res.UpdateFrom(element);
                res.SubContextOptions = element.SubContext?.Options;
                return res;
            }
        }

        public string Name { get; set; }
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public bool Active { get; set; }
        public bool Transparent { get; set; }
        public float FadeOutTime { get; set; }
        public float FadeInTime { get; set; }
        public float FadeOutDelay { get; set; }
        public float FadeInDelay { get; set; }
        public float TransformationFollowTime { get; set; }
        public ElementTransformation DisplayTransformation { get; set; } = new ElementTransformation();
        public List<InteractionBehavior> Behaviors { get; set; } = new List<InteractionBehavior>();
        public AttachedValues Value { get; set; }
        public AuxiliaryObject EnvironmentObject { get; set; }
        public bool OnlyHitIfParentIsHit { get; set; }

        /// <summary>
        /// Selectively apply transform components. Default is All
        /// </summary>
        public ApplyTransformMode TransformApplication { get; set; } = ApplyTransformMode.All;

        /// <summary>
        /// Set attached values when updating instances. Default is true
        /// </summary>
        public bool SetAttachedValues { get; set; } = true;

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
        /// If not null element instances will contain their own Notui contexts. This is good for viewports, clipping and arbitrary surface deformation.
        /// </summary>
        /// <remarks>
        /// This is like iframes for HTML. SubContexts are not replacing children and elements inside SubContexts are not queried with Opaq.
        /// But unlike iframes in HTML nothing stops you here to have logical connections between different SubContexts of different elements.
        /// </remarks>
        public SubContextOptions SubContextOptions { get; set; }

        /// <summary>
        /// Base constructor
        /// </summary>
        /// <param name="insttype">The type of the element instances which this prototype will instantiate</param>
        /// <param name="id">Optional external unique Id. Use carefully!</param>
        protected ElementPrototype(Type insttype, string id = null, ElementPrototype parent = null)
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
        protected ElementPrototype(NotuiElement fromInstance, bool newId = true)
        {
            InstanceType = fromInstance.GetType();
            this.UpdateCommon(fromInstance, ApplyTransformMode.All);

            //Value = fromInstance.Value?.Copy();
            EnvironmentObject = fromInstance.EnvironmentObject.Copy();
            Parent = fromInstance.Parent.Prototype;

            if (newId) Id = Guid.NewGuid().ToString();

            foreach (var child in fromInstance.Children.Values)
            {
                Children.Add(child.Id, child.Prototype.Copy());
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
            var res = (NotuiElement)GetElementConstructor().Invoke(new object[] { this, context, parent });
            return res;
        }

        public virtual ElementPrototype Copy()
        {
            var res = CreateFromPrototype(this, false);
            res.UpdateFrom(this);
            return res;
        }

        public object Clone()
        {
            return Copy();
        }

        public virtual void UpdateFrom(NotuiElement other)
        {
            this.UpdateCommon(other, ApplyTransformMode.All);
        }

        public virtual void UpdateFrom(ElementPrototype other)
        {
            this.UpdateCommon(other, ApplyTransformMode.All);
        }
    }
}

