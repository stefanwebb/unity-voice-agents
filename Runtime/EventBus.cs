using GenericEventBus;
using UnityEngine;

namespace GenerativeGamedev {

public interface IEvent {}

public static class EventBus
{

    private static GenericEventBus<IEvent> _eventBus;

    // [RuntimeInitializeOnLoadMethod] only fires when entering Play mode or in
    // a built player — never in pure Editor mode. [ExecuteAlways] components
    // (e.g. SttClient) call into EventBus from OnEnable in Editor mode too, so
    // Init() also needs to run there; [InitializeOnLoadMethod] is the Editor
    // counterpart, firing whenever scripts load in the Editor.
#if UNITY_EDITOR
    [UnityEditor.InitializeOnLoadMethod]
    private static void InitInEditor() => Init();
#endif

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Init()
    {
#if UNITY_EDITOR
        // If domain reload is disabled, this can still be assigned from last play mode.
        _eventBus?.Dispose();
#endif

        _eventBus = new GenericEventBus<IEvent>();
    }

    /// <summary>
    /// Has the current raised event been consumed?
    /// </summary>
    public static bool CurrentEventIsConsumed => _eventBus.CurrentEventIsConsumed;

    /// <summary>
    /// Is an event currently being raised?
    /// </summary>
    public static bool IsEventBeingRaised => _eventBus.IsEventBeingRaised;

    /// <summary>
    /// <para>Raises the given event immediately, regardless if another event is currently still being raised.</para>
    /// </summary>
    /// <param name="event">The event to raise.</param>
    /// <typeparam name="TEvent">The type of event to raise.</typeparam>
    /// <returns>Returns true if the event was consumed with <see cref="ConsumeCurrentEvent"/>.</returns>
    public static bool RaiseImmediately<TEvent>(TEvent @event) where TEvent : IEvent
    {
        return RaiseImmediately(ref @event);
    }

    /// <summary>
    /// <para>Raises the given event immediately, regardless if another event is currently still being raised.</para>
    /// </summary>
    /// <param name="event">The event to raise.</param>
    /// <typeparam name="TEvent">The type of event to raise.</typeparam>
    /// <returns>Returns true if the event was consumed with <see cref="ConsumeCurrentEvent"/>.</returns>
    public static bool RaiseImmediately<TEvent>(ref TEvent @event) where TEvent : IEvent
    {
        return _eventBus.RaiseImmediately(ref @event);
    }

    /// <summary>
    /// <para>Raises the given event. If there are other events currently being raised, this event will be raised after those events finish.</para>
    /// </summary>
    /// <param name="event">The event to raise.</param>
    /// <typeparam name="TEvent">The type of event to raise.</typeparam>
    /// <returns>If the event was raised immediately, returns true if the event was consumed with <see cref="ConsumeCurrentEvent"/>.</returns>
    public static bool Raise<TEvent>(in TEvent @event) where TEvent : IEvent
    {
        return _eventBus.Raise(in @event);
    }

    /// <summary>
    /// Subscribe to a given event type.
    /// </summary>
    /// <param name="handler">The method that should be invoked when the event is raised.</param>
    /// <param name="priority">Higher priority means this listener will receive the event earlier than other listeners with lower priority.
    ///                        If multiple listeners have the same priority, they will be invoked in the order they subscribed.</param>
    /// <typeparam name="TEvent">The event type to subscribe to.</typeparam>
    public static void SubscribeTo<TEvent>(GenericEventBus<IEvent>.EventHandler<TEvent> handler, float priority = 0)
        where TEvent : IEvent
    {
        _eventBus.SubscribeTo(handler, priority);
    }

    /// <summary>
    /// Unsubscribe from a given event type.
    /// </summary>
    /// <param name="handler">The method that was previously given in SubscribeTo.</param>
    /// <typeparam name="TEvent">The event type to unsubscribe from.</typeparam>
    public static void UnsubscribeFrom<TEvent>(GenericEventBus<IEvent>.EventHandler<TEvent> handler)
        where TEvent : IEvent
    {
        _eventBus.UnsubscribeFrom(handler);
    }

    /// <summary>
    /// Consumes the current event being raised, which stops the propagation to other listeners.
    /// </summary>
    public static void ConsumeCurrentEvent()
    {
        _eventBus.ConsumeCurrentEvent();
    }

    /// <summary>
    /// Removes all the listeners of the given event type.
    /// </summary>
    /// <typeparam name="TEvent">The event type.</typeparam>
    /// <exception cref="System.InvalidOperationException">Thrown if an event is currently being raised.</exception>
    public static void ClearListeners<TEvent>() where TEvent : IEvent
    {
        _eventBus.ClearListeners<TEvent>();
    }
}

}