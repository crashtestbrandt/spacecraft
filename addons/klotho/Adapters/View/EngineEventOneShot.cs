// Rollback-aware engine-event subscription helper (engine-free).
using System;
using xpTURN.Klotho.Core;

namespace xpTURN.Klotho.Godot
{
    /// <summary>
    /// Helper that bundles the three-way event subscription pattern (Predicted + Confirmed → onPlay,
    /// Canceled → onCancel) with optional actor filter and late-dispatch guard.
    ///
    /// Contract (matches engine's DiffRollbackEvents + DispatchVerifiedEventsPartial):
    ///   - Predicted and Confirmed are hash-dedupe paired → onPlay is called once per logical event in normal cases.
    ///   - On rollback mismatch: Canceled fires first, then Confirmed re-fires onPlay with the corrected event.
    ///   - lateGuard (optional) returns false to skip stale onPlay — typically checks ActionLockTicks > 0
    ///     to avoid late-rollback (rollback delay > action duration) firing onPlay after action's natural end.
    /// </summary>
    public static class EngineEventOneShot
    {
        public static EngineEventSubscription Subscribe<TEvent>(
            IKlothoEngine engine,
            Predicate<TEvent> filter,
            Action<TEvent> onPlay,
            Action<TEvent> onCancel = null,
            Func<bool> lateGuard = null)
            where TEvent : SimulationEvent
        {
            var sub = new EngineEventSubscription();
            sub.Bind(engine, filter, onPlay, onCancel, lateGuard);
            return sub;
        }
    }

    /// <summary>
    /// Subscription handle returned by <see cref="EngineEventOneShot.Subscribe{TEvent}"/>.
    /// Dispose unsubscribes all event channels and releases captured lambdas — required at OnDeactivate
    /// to avoid keeping the view alive via engine event delegate references.
    /// </summary>
    public sealed class EngineEventSubscription : IDisposable
    {
        private IKlothoEngine _engine;
        private Action<int, SimulationEvent> _predictedHandler;
        private Action<int, SimulationEvent> _confirmedHandler;
        private Action<int, SimulationEvent> _canceledHandler;

        /// <summary>
        /// Internal — game code should call <see cref="EngineEventOneShot.Subscribe{TEvent}"/> instead.
        /// Direct invocation bypasses the public entry point.
        /// </summary>
        internal void Bind<TEvent>(
            IKlothoEngine engine,
            Predicate<TEvent> filter,
            Action<TEvent> onPlay,
            Action<TEvent> onCancel,
            Func<bool> lateGuard)
            where TEvent : SimulationEvent
        {
            _engine = engine;

            _predictedHandler = (tick, e) =>
            {
                if (e is TEvent te && (filter == null || filter(te))
                    && (lateGuard == null || lateGuard()))
                    onPlay(te);
            };
            _confirmedHandler = (tick, e) =>
            {
                if (e is TEvent te && (filter == null || filter(te))
                    && (lateGuard == null || lateGuard()))
                    onPlay(te);
            };

            engine.OnEventPredicted += _predictedHandler;
            engine.OnEventConfirmed += _confirmedHandler;

            if (onCancel != null)
            {
                _canceledHandler = (tick, e) =>
                {
                    if (e is TEvent te && (filter == null || filter(te)))
                        onCancel(te);
                };
                engine.OnEventCanceled += _canceledHandler;
            }
        }

        public void Dispose()
        {
            if (_engine == null) return;
            if (_predictedHandler != null) _engine.OnEventPredicted -= _predictedHandler;
            if (_confirmedHandler != null) _engine.OnEventConfirmed -= _confirmedHandler;
            if (_canceledHandler  != null) _engine.OnEventCanceled  -= _canceledHandler;
            _engine = null;
            _predictedHandler = null;
            _confirmedHandler = null;
            _canceledHandler  = null;
        }
    }
}
