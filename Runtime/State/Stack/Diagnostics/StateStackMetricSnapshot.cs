namespace WallstopStudios.DxState.State.Stack.Diagnostics
{
    public readonly struct StateStackMetricSnapshot
    {
        public StateStackMetricSnapshot(int transitionCount, float averageDuration, float longestDuration)
        {
            TransitionCount = transitionCount;
            AverageTransitionDurationSeconds = averageDuration;
            LongestTransitionDurationSeconds = longestDuration;
        }

        public int TransitionCount { get; }

        public float AverageTransitionDurationSeconds { get; }

        public float LongestTransitionDurationSeconds { get; }
    }
}
