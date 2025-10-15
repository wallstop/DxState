namespace WallstopStudios.DxState.Tests.EditMode.State.Machine.Component
{
    using NUnit.Framework;
    using UnityEngine;
    using WallstopStudios.DxState.State.Machine.Component;

    public sealed class StateComponentTests
    {
        [Test]
        public void ShouldEnterReturnsFalseWhenTagBlocksEntry()
        {
            GameObject owner = new GameObject("TaggedStateComponent");
            try
            {
                TestStateComponent component = owner.AddComponent<TestStateComponent>();
                component.SetEnterBlocked(true);

                bool shouldEnter = component.ShouldEnter();

                Assert.IsFalse(shouldEnter);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(owner);
            }
        }

        [Test]
        public void ShouldEnterReturnsFalseWhenAlreadyActive()
        {
            GameObject owner = new GameObject("ActiveStateComponent");
            try
            {
                TestStateComponent component = owner.AddComponent<TestStateComponent>();
                component.Enter();

                bool shouldEnter = component.ShouldEnter();

                Assert.IsFalse(shouldEnter);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(owner);
            }
        }

        private sealed class TestStateComponent : StateComponent
        {
            private bool _blockEntry;

            public void SetEnterBlocked(bool blocked)
            {
                _blockEntry = blocked;
            }

            protected override bool IsEnterBlockedByTag()
            {
                return _blockEntry;
            }

            protected override void OnEnter()
            {
            }

            protected override void OnExit()
            {
            }
        }
    }
}
