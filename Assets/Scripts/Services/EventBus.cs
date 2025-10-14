// File: Assets/Scripts/Services/EventBus.cs
using System;

namespace Game.Services
{
    /// <summary>Lightweight pub/sub wrapper (static C# events).</summary>
    public static class EventBus
    {
        public static event Action OnSomething; // placeholder
        public static void RaiseSomething() => OnSomething?.Invoke();
    }
}
