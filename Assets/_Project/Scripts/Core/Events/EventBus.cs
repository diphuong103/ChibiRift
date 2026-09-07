using System;
using System.Collections.Generic;

namespace ChibiRift.Core
{
    /// <summary>
    /// Typed publish/subscribe channel. This is the ONLY sanctioned path from gameplay to UI
    /// (SRS 26: "gameplay logic không phụ thuộc trực tiếp vào UI. UI subscribe state/event").
    /// Gameplay publishes; <c>ChibiRift.UI</c> subscribes. The assembly graph makes the reverse
    /// direction impossible to compile.
    /// </summary>
    /// <remarks>
    /// Handlers are invoked synchronously on the calling thread. A handler that throws will
    /// prevent later handlers of the same event from running, so subscribers must not throw.
    /// </remarks>
    public sealed class EventBus
    {
        private readonly Dictionary<Type, Delegate> _handlers = new Dictionary<Type, Delegate>();

        /// <summary>Adds <paramref name="handler"/> to the subscribers of <typeparamref name="TEvent"/>.</summary>
        public void Subscribe<TEvent>(Action<TEvent> handler)
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler));
            _handlers.TryGetValue(typeof(TEvent), out Delegate existing);
            _handlers[typeof(TEvent)] = Delegate.Combine(existing, handler);
        }

        /// <summary>Removes <paramref name="handler"/>. Safe to call when not subscribed.</summary>
        public void Unsubscribe<TEvent>(Action<TEvent> handler)
        {
            if (handler == null) return;
            if (!_handlers.TryGetValue(typeof(TEvent), out Delegate existing)) return;

            Delegate remaining = Delegate.Remove(existing, handler);
            if (remaining == null) _handlers.Remove(typeof(TEvent));
            else _handlers[typeof(TEvent)] = remaining;
        }

        /// <summary>Delivers <paramref name="payload"/> to every current subscriber.</summary>
        public void Publish<TEvent>(TEvent payload)
        {
            if (_handlers.TryGetValue(typeof(TEvent), out Delegate existing) &&
                existing is Action<TEvent> typed)
            {
                typed.Invoke(payload);
            }
        }

        /// <summary>Number of subscribers for <typeparamref name="TEvent"/>. Diagnostics and tests.</summary>
        public int SubscriberCount<TEvent>()
        {
            return _handlers.TryGetValue(typeof(TEvent), out Delegate existing)
                ? existing.GetInvocationList().Length
                : 0;
        }

        /// <summary>Drops every subscription. Called on full teardown.</summary>
        public void Clear() => _handlers.Clear();
    }
}
