namespace WallstopStudios.DxState.State.Machine
{
    using System;

    public interface IStateContext<T>
    {
        public StateMachine<T> StateMachine { get; set; }
        public bool IsActive { get; }

        public void Enter();
        public void Exit();

        public void Log(FormattableString message);
    }
}
