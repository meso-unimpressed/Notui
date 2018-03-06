using System;
using System.Linq;
using System.Numerics;

namespace Notui
{
    /// <inheritdoc />
    /// <summary>
    /// Attribute telling some metadata about the behavior's parameter for the host application
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public class BehaviorParameterAttribute : Attribute
    {
        public float Minimum = float.MinValue;
        public float Maximum = float.MaxValue;
    }

    /// <summary>
    /// Abstract class for defining per-frame behavior for NotuiElements.
    /// </summary>
    /// <remarks>
    /// Unlike NotuiElements behaviors should not be stateful. You can store states in the Auxiliary object dictionary of an element
    /// </remarks>
    public abstract class InteractionBehavior
    {
        public const string BehaviorStatePrefix = "Internal.Behavior:";

        /// <summary>
        /// A unique identifier used comparing elements
        /// </summary>
        public Guid Id { get; } = Guid.NewGuid();

        /// <summary>
        /// The method which will be executed for the given element every frame.
        /// </summary>
        public abstract void Behave(NotuiElement element);

        public T GetState<T>(NotuiElement element) where T : AuxiliaryObject
        {
            if (element.Value == null)
                element.Value = new AttachedValues();
            try
            {
                return (T)element.Value.Auxiliary[BehaviorStatePrefix + Id];
            }
            catch (Exception e)
            {
                var we = new Exception("Getting behavior state object failed.", e);
                throw we;
            }
        }

        public void SetState(NotuiElement element, AuxiliaryObject value)
        {
            if (element.Value == null)
                element.Value = new AttachedValues();
            if (IsStateAvailable(element))
                element.Value.Auxiliary[BehaviorStatePrefix + Id] = value;
            else element.Value.Auxiliary.Add(BehaviorStatePrefix + Id, value);
        }

        public bool IsStateAvailable(NotuiElement element)
        {
            if(element.Value == null)
                element.Value = new AttachedValues();
            return element.Value.Auxiliary.ContainsKey(BehaviorStatePrefix + Id);
        }
    }
}
