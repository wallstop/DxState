namespace WallstopStudios.DxState.Tests.EditMode.State.Stack
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using NUnit.Framework;
    using UnityEngine;
    using WallstopStudios.DxState.State.Stack;
    using WallstopStudios.DxState.State.Stack.Builder;

    public sealed class StateGraphAssetTests
    {
        [Test]
        public void BuildGraphCreatesStateStackConfigurations()
        {
            StateGraphAsset asset = ScriptableObject.CreateInstance<StateGraphAsset>();
            DummyStateAsset firstState = ScriptableObject.CreateInstance<DummyStateAsset>();
            DummyStateAsset secondState = ScriptableObject.CreateInstance<DummyStateAsset>();

            try
            {
                firstState.Initialize("FirstState");
                secondState.Initialize("SecondState");

                StateGraphAsset.StackDefinition stackDefinition = new StateGraphAsset.StackDefinition();
                stackDefinition.SetName("Gameplay");
                stackDefinition.AddState(firstState, true);
                stackDefinition.AddState(secondState, false);

                asset.SetStacks(new List<StateGraphAsset.StackDefinition> { stackDefinition });

                StateGraph graph = asset.BuildGraph();
                Assert.IsTrue(
                    graph.TryGetStack("Gameplay", out StateStackConfiguration configuration)
                );
                Assert.AreEqual(2, configuration.States.Count);
                Assert.AreSame(firstState, configuration.InitialState);
                Assert.AreSame(firstState, configuration.States[0]);
                Assert.AreSame(secondState, configuration.States[1]);
            }
            finally
            {
                if (asset != null)
                {
                    UnityEngine.Object.DestroyImmediate(asset);
                }

                if (firstState != null)
                {
                    UnityEngine.Object.DestroyImmediate(firstState);
                }

                if (secondState != null)
                {
                    UnityEngine.Object.DestroyImmediate(secondState);
                }
            }
        }

        private sealed class DummyStateAsset : ScriptableObject, IState
        {
            private string _stateName;

            public void Initialize(string stateName)
            {
                if (string.IsNullOrWhiteSpace(stateName))
                {
                    throw new ArgumentException("State name must be provided.", nameof(stateName));
                }

                _stateName = stateName;
                name = stateName;
            }

            public string Name => _stateName;

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
                progress.Report(1f);
                return new ValueTask();
            }

            public void Tick(TickMode mode, float delta) { }

            public ValueTask Exit<TProgress>(
                IState nextState,
                TProgress progress,
                StateDirection direction
            )
                where TProgress : IProgress<float>
            {
                progress.Report(1f);
                return new ValueTask();
            }

            public ValueTask Remove<TProgress>(
                IReadOnlyList<IState> previousStatesInStack,
                IReadOnlyList<IState> nextStatesInStack,
                TProgress progress
            )
                where TProgress : IProgress<float>
            {
                progress.Report(1f);
                return new ValueTask();
            }
        }
    }
}
