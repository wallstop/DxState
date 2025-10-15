namespace WallstopStudios.DxState.Tests.EditMode.State.Stack
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using NUnit.Framework;
    using UnityEngine.SceneManagement;
    using WallstopStudios.DxState.State.Stack;
    using WallstopStudios.DxState.State.Stack.Builder;
    using WallstopStudios.DxState.State.Stack.States;

    public sealed class StateGraphBuilderTests
    {
        [Test]
        public void BuildCreatesStackConfigurationWithInitialState()
        {
            TestState first = new TestState("First");
            TestState second = new TestState("Second");

            StateGraphBuilder builder = new StateGraphBuilder().Stack(
                "Primary",
                stack => stack.State(first, setAsInitial: true).State(second)
            );

            StateGraph graph = builder.Build();
            Assert.IsTrue(graph.TryGetStack("Primary", out StateStackConfiguration configuration));
            CollectionAssert.Contains(configuration.States, first);
            CollectionAssert.Contains(configuration.States, second);
            Assert.AreSame(first, configuration.InitialState);
        }

        [Test]
        public void SceneHelperAddsSceneState()
        {
            StateGraphBuilder builder = new StateGraphBuilder().Stack(
                "Scenes",
                stack =>
                    stack.Scene(
                        "Gameplay",
                        SceneTransitionMode.Addition,
                        loadParameters: new LoadSceneParameters(LoadSceneMode.Additive),
                        setAsInitial: true
                    )
            );

            StateGraph graph = builder.Build();
            Assert.IsTrue(graph.TryGetStack("Scenes", out StateStackConfiguration configuration));

            Assert.AreEqual(1, configuration.States.Count);
            SceneState sceneState = configuration.States[0] as SceneState;
            Assert.IsNotNull(sceneState);
            Assert.AreEqual("Gameplay", sceneState.Name);
            Assert.AreEqual(SceneTransitionMode.Addition, sceneState.TransitionMode);
        }

        [Test]
        public void GroupHelperCreatesStateGroup()
        {
            TestState childA = new TestState("ChildA");
            TestState childB = new TestState("ChildB");

            StateGraphBuilder builder = new StateGraphBuilder().Stack(
                "Grouped",
                stack =>
                    stack.Group(
                        "Group",
                        new[] { childA, childB },
                        StateGroupMode.Sequential,
                        setAsInitial: true
                    )
            );

            StateGraph graph = builder.Build();
            Assert.IsTrue(graph.TryGetStack("Grouped", out StateStackConfiguration configuration));
            Assert.AreEqual(1, configuration.States.Count);
            StateGroup stateGroup = configuration.States[0] as StateGroup;
            Assert.IsNotNull(stateGroup);
            Assert.AreEqual("Group", stateGroup.Name);
        }

        private sealed class TestState : IState
        {
            private readonly string _name;

            public TestState(string name)
            {
                _name = name ?? throw new ArgumentNullException(nameof(name));
            }

            public string Name => _name;

            public TickMode TickMode => TickMode.None;

            public bool TickWhenInactive => false;

            public float? TimeInState => null;

            public ValueTask Enter<TProgress>(
                IState previousState,
                TProgress progress,
                StateDirection direction
            )
                where TProgress : IProgress<float>
            {
                progress?.Report(1f);
                return default;
            }

            public void Tick(TickMode mode, float delta) { }

            public ValueTask Exit<TProgress>(
                IState nextState,
                TProgress progress,
                StateDirection direction
            )
                where TProgress : IProgress<float>
            {
                progress?.Report(1f);
                return default;
            }

            public ValueTask Remove<TProgress>(
                IReadOnlyList<IState> previousStatesInStack,
                IReadOnlyList<IState> nextStatesInStack,
                TProgress progress
            )
                where TProgress : IProgress<float>
            {
                progress?.Report(1f);
                return default;
            }
        }
    }
}
