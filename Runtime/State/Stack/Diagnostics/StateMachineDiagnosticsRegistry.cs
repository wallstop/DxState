namespace WallstopStudios.DxState.State.Stack.Diagnostics
{
    using System;
    using System.Collections.Generic;
    using WallstopStudios.DxState.State.Machine;

    public static class StateMachineDiagnosticsRegistry
    {
        private static readonly object Gate = new object();
        private static readonly List<IRegistration> Registrations = new List<IRegistration>();

        public static IDisposable Register<TState>(
            StateMachine<TState> machine,
            StateMachineDiagnostics<TState> diagnostics
        )
        {
            if (machine == null)
            {
                throw new ArgumentNullException(nameof(machine));
            }

            if (diagnostics == null)
            {
                throw new ArgumentNullException(nameof(diagnostics));
            }

            Registration<TState> registration = new Registration<TState>(machine, diagnostics);
            lock (Gate)
            {
                Registrations.Add(registration);
            }

            return registration;
        }

        public static IReadOnlyList<StateMachineDiagnosticsEntry> GetEntries()
        {
            lock (Gate)
            {
                List<StateMachineDiagnosticsEntry> snapshot = new List<StateMachineDiagnosticsEntry>(
                    Registrations.Count
                );

                for (int i = Registrations.Count - 1; i >= 0; i--)
                {
                    IRegistration registration = Registrations[i];
                    if (!registration.IsAlive)
                    {
                        Registrations.RemoveAt(i);
                        continue;
                    }

                    if (registration.TryCreateEntry(out StateMachineDiagnosticsEntry entry))
                    {
                        snapshot.Add(entry);
                    }
                }

                return snapshot;
            }
        }

        private interface IRegistration : IDisposable
        {
            bool IsAlive { get; }

            bool TryCreateEntry(out StateMachineDiagnosticsEntry entry);
        }

        private sealed class Registration<TState> : IRegistration
        {
            private readonly WeakReference<StateMachine<TState>> _machine;
            private readonly WeakReference<StateMachineDiagnostics<TState>> _diagnostics;

            public Registration(
                StateMachine<TState> machine,
                StateMachineDiagnostics<TState> diagnostics
            )
            {
                _machine = new WeakReference<StateMachine<TState>>(machine);
                _diagnostics = new WeakReference<StateMachineDiagnostics<TState>>(diagnostics);
            }

            public bool IsAlive
            {
                get
                {
                    return _machine.TryGetTarget(out _)
                        && _diagnostics.TryGetTarget(out _);
                }
            }

            public bool TryCreateEntry(out StateMachineDiagnosticsEntry entry)
            {
                if (
                    _machine.TryGetTarget(out StateMachine<TState> machine)
                    && _diagnostics.TryGetTarget(out StateMachineDiagnostics<TState> diagnostics)
                )
                {
                    entry = new StateMachineDiagnosticsEntry(
                        typeof(TState),
                        machine,
                        diagnostics
                    );
                    return true;
                }

                entry = default;
                return false;
            }

            public void Dispose()
            {
                lock (Gate)
                {
                    Registrations.Remove(this);
                }
            }
        }
    }

    public readonly struct StateMachineDiagnosticsEntry
    {
        public StateMachineDiagnosticsEntry(Type stateType, object machine, object diagnostics)
        {
            StateType = stateType ?? throw new ArgumentNullException(nameof(stateType));
            Machine = machine;
            Diagnostics = diagnostics ?? throw new ArgumentNullException(nameof(diagnostics));
        }

        public Type StateType { get; }

        public object Machine { get; }

        public object Diagnostics { get; }
    }
}
