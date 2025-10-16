namespace WallstopStudios.DxState.State.Machine.Component
{
    using System;
    using WallstopStudios.DxState.State.Stack.Diagnostics;

    public static class StateMachineDiagnosticsExtensions
    {
        public static StateMachineDiagnostics<TState> AttachDiagnostics<TState>(
            this StateMachine<TState> stateMachine,
            StateMachineDiagnostics<TState> diagnostics
        )
        {
            if (stateMachine == null)
            {
                throw new ArgumentNullException(nameof(stateMachine));
            }

            if (diagnostics == null)
            {
                throw new ArgumentNullException(nameof(diagnostics));
            }

            stateMachine.TransitionExecuted += diagnostics.RecordTransition;
            stateMachine.TransitionDeferred += diagnostics.RecordDeferredTransition;
            return diagnostics;
        }
    }
}
