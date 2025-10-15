namespace WallstopStudios.DxState.Tests.EditMode.State.Stack.Systems
{
    using System;
    using NUnit.Framework;
    using UnityEngine;
    using WallstopStudios.DxState.State.Stack;
    using WallstopStudios.DxState.State.Stack.States.Systems;

    public sealed class PhysicsIsolationStateTests
    {
        [Test]
        public void EnterSetsIgnoreCollisionAndExitRestores()
        {
            bool original = Physics.GetIgnoreLayerCollision(0, 1);
            try
            {
                PhysicsIsolationState.LayerPair pair = new PhysicsIsolationState.LayerPair
                {
                    LayerA = 0,
                    LayerB = 1,
                    IgnoreCollisions = true,
                };

                PhysicsIsolationState state = new PhysicsIsolationState(
                    "PhysicsIsolation",
                    new[] { pair }
                );
                ProgressCollector progress = new ProgressCollector();

                _ = state.Enter(null, progress, StateDirection.Forward);
                Assert.IsTrue(Physics.GetIgnoreLayerCollision(0, 1));

                _ = state.Exit(null, progress, StateDirection.Backward);
                Assert.AreEqual(original, Physics.GetIgnoreLayerCollision(0, 1));
            }
            finally
            {
                Physics.IgnoreLayerCollision(0, 1, original);
            }
        }

        private sealed class ProgressCollector : IProgress<float>
        {
            public void Report(float value) { }
        }
    }
}
