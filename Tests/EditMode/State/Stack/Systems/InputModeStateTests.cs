namespace WallstopStudios.DxState.Tests.EditMode.State.Stack.Systems
{
    using System;
    using System.Collections.Generic;
    using NUnit.Framework;
    using WallstopStudios.DxState.State.Stack;
    using WallstopStudios.DxState.State.Stack.States.Systems;

    public sealed class InputModeStateTests
    {
        [Test]
        public void EnterAndExitReportProgress()
        {
            InputModeState state = new InputModeState("InputMode");
            ProgressCollector progress = new ProgressCollector();
            _ = state.Enter(null, progress, StateDirection.Forward);
            _ = state.Exit(null, progress, StateDirection.Backward);

            Assert.AreEqual(2, progress.Reported.Count);
        }

        private sealed class ProgressCollector : IProgress<float>
        {
            public List<float> Reported { get; } = new List<float>();

            public void Report(float value)
            {
                Reported.Add(value);
            }
        }
    }
}
