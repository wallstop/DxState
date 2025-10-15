namespace WallstopStudios.DxState.State.Machine.Trigger
{
    public readonly struct TriggerStateTransition<TState, TTrigger>
    {
        public TriggerStateTransition(TState from, TTrigger trigger, TState to)
        {
            From = from;
            Trigger = trigger;
            To = to;
        }

        public TState From { get; }

        public TTrigger Trigger { get; }

        public TState To { get; }
    }
}
