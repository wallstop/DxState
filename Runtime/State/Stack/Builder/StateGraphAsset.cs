namespace WallstopStudios.DxState.State.Stack.Builder
{
    using System;
    using System.Collections.Generic;
    using UnityEngine;
    using WallstopStudios.DxState.State.Machine;

    [CreateAssetMenu(
        fileName = "StateGraphAsset",
        menuName = "Wallstop Studios/DxState/State Graph",
        order = 10
    )]
    public sealed class StateGraphAsset : ScriptableObject
    {
        [SerializeField]
        private List<StackDefinition> _stacks = new List<StackDefinition>();

        public IReadOnlyList<StackDefinition> Stacks => _stacks;

        internal void SetStacks(IEnumerable<StackDefinition> stacks)
        {
            _stacks.Clear();
            if (stacks == null)
            {
                return;
            }

            foreach (StackDefinition definition in stacks)
            {
                if (definition != null)
                {
                    _stacks.Add(definition);
                }
            }
        }

        public StateGraph BuildGraph()
        {
            Dictionary<string, StateStackConfiguration> configurations = new Dictionary<
                string,
                StateStackConfiguration
            >(StringComparer.Ordinal);

            for (int i = 0; i < _stacks.Count; i++)
            {
                StackDefinition definition = _stacks[i];
                if (string.IsNullOrWhiteSpace(definition.Name))
                {
                    continue;
                }

                if (configurations.ContainsKey(definition.Name))
                {
                    throw new InvalidOperationException(
                        $"Duplicate stack definition detected for name '{definition.Name}'."
                    );
                }

                StateStackConfiguration configuration = definition.BuildConfiguration();
                configurations[definition.Name] = configuration;
            }

            return new StateGraph(configurations);
        }

        [Serializable]
        public sealed class StackDefinition
        {
            [SerializeField]
            private string _name;

            [SerializeField]
            private List<StateReference> _states = new List<StateReference>();

            [SerializeField]
            private List<StateTransitionMetadata> _transitions = new List<StateTransitionMetadata>();

            public string Name => _name;

            public IReadOnlyList<StateReference> States => _states;

            public IReadOnlyList<StateTransitionMetadata> Transitions => _transitions;

            internal void SetName(string name)
            {
                _name = name;
            }

            internal void AddState(UnityEngine.Object state, bool setAsInitial)
            {
                if (_states == null)
                {
                    _states = new List<StateReference>();
                }

                StateReference reference = new StateReference();
                reference.SetState(state, setAsInitial);
                _states.Add(reference);
            }

            public StateStackConfiguration BuildConfiguration()
            {
                if (_states.Count == 0)
                {
                    throw new InvalidOperationException(
                        $"Stack '{_name}' must contain at least one state reference."
                    );
                }

                List<IState> resolvedStates = new List<IState>(_states.Count);
                IState initialState = null;

                for (int i = 0; i < _states.Count; i++)
                {
                    StateReference stateReference = _states[i];
                    IState resolved = stateReference.Resolve();
                    resolvedStates.Add(resolved);
                    if (stateReference.SetAsInitial || initialState == null)
                    {
                        initialState = resolved;
                    }
                }

                if (initialState == null)
                {
                    throw new InvalidOperationException(
                        $"Stack '{_name}' did not identify an initial state."
                    );
                }

                return new StateStackConfiguration(resolvedStates, initialState);
            }
        }

        [Serializable]
        public sealed class StateReference
        {
            [SerializeField]
            private UnityEngine.Object _state;

            [SerializeField]
            private bool _setAsInitial;

            public UnityEngine.Object RawState => _state;

            public bool SetAsInitial => _setAsInitial;

            internal void SetState(UnityEngine.Object state, bool setAsInitial)
            {
                _state = state;
                _setAsInitial = setAsInitial;
            }

            public IState Resolve()
            {
                if (_state == null)
                {
                    throw new InvalidOperationException("State reference cannot be null.");
                }

                if (_state is IState state)
                {
                    return state;
                }

                throw new InvalidOperationException(
                    $"Serialized object '{_state.name}' does not implement IState."
                );
            }
        }

        [Serializable]
        public sealed class StateTransitionMetadata
        {
            [SerializeField]
            private int _fromIndex;

            [SerializeField]
            private int _toIndex;

            [SerializeField]
            private string _label;

            [SerializeField]
            private string _tooltip;

            [SerializeField]
            private TransitionCause _cause;

            [SerializeField]
            private TransitionFlags _flags;

            public int FromIndex => _fromIndex;

            public int ToIndex => _toIndex;

            public string Label => _label;

            public string Tooltip => _tooltip;

            public TransitionCause Cause => _cause;

            public TransitionFlags Flags => _flags;
        }
    }
}
