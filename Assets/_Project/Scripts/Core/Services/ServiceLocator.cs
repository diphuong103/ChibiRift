using System;
using System.Collections.Generic;

namespace ChibiRift.Core
{
    /// <summary>
    /// The single composition root of the game (SRS 26: "tránh singleton lạm dụng").
    /// Exactly one instance is created by <see cref="GameBootstrap"/> in the Boot scene and
    /// exposed through <see cref="Current"/>. No other type in the project may declare a
    /// singleton; everything is registered here and resolved by interface.
    /// </summary>
    public sealed class ServiceLocator
    {
        private readonly Dictionary<Type, object> _services = new Dictionary<Type, object>();

        /// <summary>The locator built by the Boot scene. Null until <see cref="GameBootstrap"/> runs.</summary>
        public static ServiceLocator Current { get; private set; }

        /// <summary>Installs <paramref name="locator"/> as the process-wide root. Boot scene only.</summary>
        public static void SetCurrent(ServiceLocator locator) => Current = locator;

        /// <summary>Clears the process-wide root. Used by tests and by a full teardown.</summary>
        public static void ClearCurrent() => Current = null;

        /// <summary>Registers <paramref name="service"/> under interface <typeparamref name="T"/>.</summary>
        /// <exception cref="InvalidOperationException">A service is already registered for <typeparamref name="T"/>.</exception>
        public void Register<T>(T service) where T : class
        {
            if (service == null) throw new ArgumentNullException(nameof(service));
            if (_services.ContainsKey(typeof(T)))
                throw new InvalidOperationException($"Service already registered: {typeof(T).Name}");
            _services[typeof(T)] = service;
        }

        /// <summary>Resolves <typeparamref name="T"/>, throwing when it was never registered.</summary>
        public T Get<T>() where T : class
        {
            if (_services.TryGetValue(typeof(T), out object service)) return (T)service;
            throw new InvalidOperationException(
                $"Service not registered: {typeof(T).Name}. Is the Boot scene the active entry point?");
        }

        /// <summary>Resolves <typeparamref name="T"/> without throwing.</summary>
        public bool TryGet<T>(out T service) where T : class
        {
            if (_services.TryGetValue(typeof(T), out object found))
            {
                service = (T)found;
                return true;
            }
            service = null;
            return false;
        }

        /// <summary>True when a service is registered for <typeparamref name="T"/>.</summary>
        public bool IsRegistered<T>() where T : class => _services.ContainsKey(typeof(T));

        /// <summary>Removes the registration for <typeparamref name="T"/>, if any.</summary>
        public void Unregister<T>() where T : class => _services.Remove(typeof(T));

        /// <summary>Drops every registration. Disposable services are disposed first.</summary>
        public void Clear()
        {
            foreach (object service in _services.Values)
            {
                if (service is IDisposable disposable) disposable.Dispose();
            }
            _services.Clear();
        }
    }
}
