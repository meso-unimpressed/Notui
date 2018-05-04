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
        /// <summary>
        /// A minimum value for a UI
        /// </summary>
        public float Minimum = float.MinValue;
        /// <summary>
        /// A maximum value for a UI
        /// </summary>
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
        /// <summary>
        /// This is a global prefix for behavior states assigned to elements
        /// </summary>
        public const string BehaviorStatePrefix = "Internal.Behavior:";

        /// <summary>
        /// A unique identifier used comparing elements
        /// </summary>
        public Guid Id { get; } = Guid.NewGuid();

        /// <summary>
        /// The method which will be executed for the given element every frame.
        /// </summary>
        public abstract void Behave(NotuiElement element);

        /// <summary>
        /// Convinience function to get a state from an element's attached aux objects corresponding to this behavior
        /// </summary>
        /// <typeparam name="T">The type of the behavior state</typeparam>
        /// <param name="element"></param>
        /// <returns></returns>
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

        /// <summary>
        /// Convinience function to set a state for an element corresponding to this behavior
        /// </summary>
        /// <param name="element"></param>
        /// <param name="value">The state to be assigned</param>
        /// <returns></returns>
        public void SetState(NotuiElement element, AuxiliaryObject value)
        {
            if (element.Value == null)
                element.Value = new AttachedValues();
            if (IsStateAvailable(element))
                element.Value.Auxiliary[BehaviorStatePrefix + Id] = value;
            else element.Value.Auxiliary.Add(BehaviorStatePrefix + Id, value);
        }

        /// <summary>
        /// Determine if a state of this behavior is attached to an element
        /// </summary>
        /// <param name="element"></param>
        /// <returns>True if there's a state for this behavior</returns>
        public bool IsStateAvailable(NotuiElement element)
        {
            if(element.Value == null)
                element.Value = new AttachedValues();
            return element.Value.Auxiliary.ContainsKey(BehaviorStatePrefix + Id);
        }
    }
}
