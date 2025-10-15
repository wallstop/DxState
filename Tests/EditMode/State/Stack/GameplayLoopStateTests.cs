namespace WallstopStudios.DxState.Tests.EditMode.State.Stack
{
    using System;
    using System.Collections;
    using NUnit.Framework;
    using UnityEngine.TestTools;
    using WallstopStudios.DxState.State.Stack;
    using WallstopStudios.DxState.State.Stack.States.Scenarios;
    using WallstopStudios.DxState.Tests.EditMode.TestSupport;

    public sealed class GameplayLoopStateTests
    {
        [UnityTest]
        public IEnumerator EnterTriggersStartAndPauseResumeDelegates()
        {
            bool startCalled = false;
            bool pauseCalled = false;
            bool resumeCalled = false;
            bool returnCalled = false;

            GameplayLoopState state = new GameplayLoopState(
                "GameplayLoop",
                () =>
                {
                    startCalled = true;
                    return default;
                },
                () =>
                {
                    pauseCalled = true;
                    return default;
                },
                () =>
                {
                    resumeCalled = true;
                    return default;
                },
                () =>
                {
                    returnCalled = true;
                    return default;
                }
            );

            yield return ValueTaskTestHelpers.WaitForValueTask(
                state.Enter(null, new Progress<float>(_ => { }), StateDirection.Forward)
            );
            Assert.IsTrue(startCalled);

            yield return ValueTaskTestHelpers.WaitForValueTask(state.TriggerPauseAsync());
            Assert.IsTrue(pauseCalled);

            yield return ValueTaskTestHelpers.WaitForValueTask(state.TriggerResumeAsync());
            Assert.IsTrue(resumeCalled);

            yield return ValueTaskTestHelpers.WaitForValueTask(
                state.Exit(null, new Progress<float>(_ => { }), StateDirection.Forward)
            );
            Assert.IsTrue(returnCalled);
        }
    }
}
