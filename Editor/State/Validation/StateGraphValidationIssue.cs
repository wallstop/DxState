#if UNITY_EDITOR
namespace WallstopStudios.DxState.Editor.State.Validation
{
    internal enum StateGraphValidationSeverity
    {
        Info = 0,
        Warning = 1,
        Error = 2,
    }

    internal readonly struct StateGraphValidationIssue
    {
        public StateGraphValidationIssue(
            StateGraphValidationSeverity severity,
            string message,
            string stackName,
            int stackIndex,
            int stateIndex,
            int transitionIndex
        )
        {
            Severity = severity;
            Message = message;
            StackName = stackName;
            StackIndex = stackIndex;
            StateIndex = stateIndex;
            TransitionIndex = transitionIndex;
        }

        public StateGraphValidationSeverity Severity { get; }

        public string Message { get; }

        public string StackName { get; }

        public int StackIndex { get; }

        public int StateIndex { get; }

        public int TransitionIndex { get; }

        public bool TargetsState => 0 <= StateIndex;

        public bool TargetsTransition => 0 <= TransitionIndex;
    }
}
#endif
