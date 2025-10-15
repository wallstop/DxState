namespace WallstopStudios.DxState.Tests.EditMode.State.Stack
{
    using System;
    using System.Collections;
    using NUnit.Framework;
    using UnityEngine.TestTools;
    using WallstopStudios.DxState.State.Stack;
    using WallstopStudios.DxState.State.Stack.States.Scenarios;
    using WallstopStudios.DxState.Tests.EditMode.TestSupport;

    public sealed class VirtualCameraStateTests
    {
        [UnityTest]
        public IEnumerator EnterActivatesAndExitDeactivates()
        {
            TestController controller = new TestController();
            VirtualCameraState state = new VirtualCameraState("Camera", controller);

            yield return ValueTaskTestHelpers.WaitForValueTask(
                state.Enter(null, new Progress<float>(_ => { }), StateDirection.Forward)
            );
            Assert.IsTrue(controller.IsActive);

            yield return ValueTaskTestHelpers.WaitForValueTask(
                state.Exit(null, new Progress<float>(_ => { }), StateDirection.Forward)
            );
            Assert.IsFalse(controller.IsActive);
        }

        private sealed class TestController : IVirtualCameraController
        {
            public bool IsActive { get; private set; }

            public void Activate()
            {
                IsActive = true;
            }

            public void Deactivate()
            {
                IsActive = false;
            }
        }
    }
}
