namespace WallstopStudios.DxState.State.Stack
{
    using System;

    [Flags]
    public enum TickMode
    {
        None = 0,
        Update = 1 << 0,
        FixedUpdate = 1 << 1,
        LateUpdate = 1 << 2,
    }
}
