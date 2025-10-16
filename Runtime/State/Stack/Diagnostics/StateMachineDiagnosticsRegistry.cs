namespace WallstopStudios.DxState.State.Stack.Diagnostics
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using WallstopStudios.DxState.State.Machine;

    public static class StateMachineDiagnosticsRegistry
    {
        private static readonly ReaderWriterLockSlim _lock =
            new ReaderWriterLockSlim(LockRecursionPolicy.NoRecursion);
        private static readonly List<IRegistration> _registrations = new List<IRegistration>();

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
            _lock.EnterWriteLock();
            try
            {
                _registrations.Add(registration);
            }
            finally
            {
                _lock.ExitWriteLock();
            }

            return registration;
        }

        public static void FillEntries(List<StateMachineDiagnosticsEntry> buffer)
        {
            if (buffer == null)
            {
                throw new ArgumentNullException(nameof(buffer));
            }

            _lock.EnterWriteLock();
            try
            {
                buffer.Clear();
                for (int i = _registrations.Count - 1; i >= 0; i--)
                {
                    IRegistration registration = _registrations[i];
                    if (!registration.IsAlive)
                    {
                        _registrations.RemoveAt(i);
                        continue;
                    }

                    if (registration.TryCreateEntry(out StateMachineDiagnosticsEntry entry))
                    {
                        buffer.Add(entry);
                    }
                }
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }

        private static void RemoveRegistration(IRegistration registration)
        {
            _lock.EnterWriteLock();
            try
            {
                _registrations.Remove(registration);
            }
            finally
            {
                _lock.ExitWriteLock();
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
                RemoveRegistration(this);
            }
        }
    }

    public readonly struct StateMachineDiagnosticsEntry
    {
        public StateMachineDiagnosticsEntry(
            Type stateType,
            object machine,
            IStateMachineDiagnosticsView diagnostics
        )
        {
            StateType = stateType ?? throw new ArgumentNullException(nameof(stateType));
            Machine = machine;
            Diagnostics = diagnostics ?? throw new ArgumentNullException(nameof(diagnostics));
        }

        public Type StateType { get; }

        public object Machine { get; }

        public IStateMachineDiagnosticsView Diagnostics { get; }
    }
}
