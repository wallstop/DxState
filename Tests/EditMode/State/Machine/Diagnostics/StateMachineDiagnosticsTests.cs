namespace WallstopStudios.DxState.Tests.EditMode.State.Machine.Diagnostics
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using NUnit.Framework;
    using WallstopStudios.DxState.State.Machine;
    using WallstopStudios.DxState.State.Machine.Component;
    using WallstopStudios.DxState.State.Stack.Diagnostics;

    public sealed class StateMachineDiagnosticsTests
    {
        [Test]
        public void RecordsRecentTransitionsAndMetrics()
        {
            TestState idle = new TestState("Idle");
            TestState active = new TestState("Active");
            Transition<TestState> transition = new Transition<TestState>(
                idle,
                active,
                new ToggleRule(true),
                new TransitionContext(TransitionCause.RuleSatisfied)
            );

            StateMachine<TestState> machine = new StateMachine<TestState>(
                new[] { transition },
                idle
            );

            StateMachineDiagnostics<TestState> diagnostics = new StateMachineDiagnostics<TestState>(
                4
            );
            machine.AttachDiagnostics(diagnostics);

            machine.Update();

            Assert.AreEqual(1, diagnostics.TransitionCount);
            Assert.AreEqual(0, diagnostics.DeferredTransitionCount);
            Assert.AreEqual(
                1,
                diagnostics.GetTransitionCauseCount(TransitionCause.RuleSatisfied)
            );

            IReadOnlyCollection<StateMachineDiagnosticEvent<TestState>> events =
                diagnostics.RecentTransitions;
            Assert.AreEqual(1, events.Count);
            StateMachineDiagnosticEvent<TestState> recordedEvent = events.First();
            Assert.AreEqual(StateMachineDiagnosticEventType.TransitionExecuted, recordedEvent.EventType);
            Assert.AreSame(idle, recordedEvent.PreviousState);
            Assert.AreSame(active, recordedEvent.RequestedState);
            Assert.IsTrue(recordedEvent.HasExecutionContext);

            Assert.IsTrue(diagnostics.TryGetLastTransition(out TransitionExecutionContext<TestState> lastContext));
            Assert.AreSame(active, lastContext.CurrentState);
            Assert.AreSame(idle, lastContext.PreviousState);

            Assert.IsTrue(diagnostics.LastTransitionUtc.HasValue);

            Assert.IsTrue(diagnostics.TryGetStateMetrics(active, out StateMachineStateMetrics activeMetrics));
            Assert.AreEqual(1, activeMetrics.EnterCount);
            Assert.AreEqual(0, activeMetrics.ExitCount);
            Assert.IsTrue(activeMetrics.LastEnteredUtc.HasValue);

            Assert.IsTrue(diagnostics.TryGetStateMetrics(idle, out StateMachineStateMetrics idleMetrics));
            Assert.AreEqual(0, idleMetrics.EnterCount);
            Assert.AreEqual(1, idleMetrics.ExitCount);
            Assert.IsTrue(idleMetrics.LastExitedUtc.HasValue);

            List<StateMachineStateMetricsRecord> metricsRecords = new List<StateMachineStateMetricsRecord>();
            diagnostics.CopyStateMetrics(metricsRecords);
            Assert.AreEqual(2, metricsRecords.Count);

            List<StateMachineDiagnosticEventRecord> eventRecords = new List<StateMachineDiagnosticEventRecord>();
            diagnostics.CopyRecentEvents(eventRecords, 4);
            Assert.AreEqual(1, eventRecords.Count);
        }

        [Test]
        public void RecordsDeferredTransitions()
        {
            TestState idle = new TestState("Idle");
            TestState active = new TestState("Active");
            TestState standby = new TestState("Standby");

            Transition<TestState> idleToActive = new Transition<TestState>(
                idle,
                active,
                new ToggleRule(true),
                new TransitionContext(TransitionCause.RuleSatisfied)
            );
            Transition<TestState> activeToStandby = new Transition<TestState>(
                active,
                standby,
                new ToggleRule(true),
                new TransitionContext(TransitionCause.RuleSatisfied)
            );

            StateMachine<TestState> machine = new StateMachine<TestState>(
                new[] { idleToActive, activeToStandby },
                idle
            );

            StateMachineDiagnostics<TestState> diagnostics = new StateMachineDiagnostics<TestState>(
                4
            );
            machine.AttachDiagnostics(diagnostics);

            machine.TransitionExecuted += context =>
            {
                if (ReferenceEquals(context.CurrentState, active))
                {
                    machine.ForceTransition(
                        standby,
                        new TransitionContext(TransitionCause.Forced, TransitionFlags.Forced)
                    );
                }
            };

            machine.Update();
            machine.Update();

            Assert.AreEqual(2, diagnostics.TransitionCount);
            Assert.AreEqual(1, diagnostics.DeferredTransitionCount);
            Assert.AreEqual(2, diagnostics.GetTransitionCauseCount(TransitionCause.Forced));

            IReadOnlyCollection<StateMachineDiagnosticEvent<TestState>> events =
                diagnostics.RecentTransitions;
            Assert.AreEqual(3, events.Count);
            Assert.IsTrue(events.Any(evt => evt.EventType == StateMachineDiagnosticEventType.TransitionDeferred));
        }

        [Test]
        public void RegistryTracksAttachedDiagnostics()
        {
            TestState idle = new TestState("Idle");
            TestState active = new TestState("Active");
            Transition<TestState> transition = new Transition<TestState>(
                idle,
                active,
                new ToggleRule(false),
                new TransitionContext(TransitionCause.RuleSatisfied)
            );

            StateMachine<TestState> machine = new StateMachine<TestState>(
                new[] { transition },
                idle
            );

            StateMachineDiagnostics<TestState> diagnostics = new StateMachineDiagnostics<TestState>(
                2
            );
            machine.AttachDiagnostics(diagnostics);

            IReadOnlyList<StateMachineDiagnosticsEntry> entries =
                StateMachineDiagnosticsRegistry.GetEntries();
            Assert.IsTrue(
                entries.Any(
                    entry =>
                        entry.StateType == typeof(TestState)
                        && ReferenceEquals(entry.Diagnostics, diagnostics)
                )
            );
        }

        private sealed class TestState : IStateContext<TestState>
        {
            private readonly string _name;

            public TestState(string name)
            {
                _name = name;
            }

            public StateMachine<TestState> StateMachine { get; set; }

            public bool IsActive { get; private set; }

            public void Enter()
            {
                IsActive = true;
            }

            public void Exit()
            {
                IsActive = false;
            }

            public void Log(FormattableString message) { }
        }

        private readonly struct ToggleRule : ITransitionRule
        {
            private readonly bool _value;

            public ToggleRule(bool value)
            {
                _value = value;
            }

            public bool Evaluate()
            {
                return _value;
            }
        }
    }
}
