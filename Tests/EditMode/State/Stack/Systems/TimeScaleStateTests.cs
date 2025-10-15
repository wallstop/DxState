namespace WallstopStudios.DxState.Tests.EditMode.State.Stack.Systems
{
    using System;
    using System.Collections.Generic;
    using NUnit.Framework;
    using UnityEngine;
    using WallstopStudios.DxState.State.Stack;
    using WallstopStudios.DxState.State.Stack.States.Systems;

    public sealed class TimeScaleStateTests
    {
        [Test]
        public void EnterSetsTimeScaleAndExitRestores()
        {
            float original = Time.timeScale;
            try
            {
                TimeScaleState state = new TimeScaleState("SlowMo", 0.5f);
                ProgressCollector progress = new ProgressCollector();
                _ = state.Enter(null, progress, StateDirection.Forward);
                Assert.AreEqual(0.5f, Time.timeScale, 0.001f);

                _ = state.Exit(null, progress, StateDirection.Backward);
                Assert.AreEqual(original, Time.timeScale, 0.001f);
                Assert.AreEqual(2, progress.Reported.Count);
            }
            finally
            {
                Time.timeScale = original;
            }
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
