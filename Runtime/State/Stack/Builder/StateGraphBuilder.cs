namespace WallstopStudios.DxState.State.Stack.Builder
{
    using System;
    using System.Collections.Generic;
    using UnityEngine.SceneManagement;
    using WallstopStudios.DxState.State.Stack.States;

    public sealed class StateGraphBuilder
    {
        private readonly Dictionary<string, StackBuilder> _stacks;

        public StateGraphBuilder()
        {
            _stacks = new Dictionary<string, StackBuilder>(StringComparer.Ordinal);
        }

        public StateGraphBuilder Stack(string name, Action<StackBuilder> configure)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("Stack name must be provided.", nameof(name));
            }

            if (configure == null)
            {
                throw new ArgumentNullException(nameof(configure));
            }

            StackBuilder builder = new StackBuilder(name);
            configure(builder);
            if (!builder.HasStates)
            {
                throw new InvalidOperationException(
                    $"Stack '{name}' must register at least one state before building."
                );
            }

            _stacks[name] = builder;
            return this;
        }

        public StateGraph Build()
        {
            Dictionary<string, StateStackConfiguration> configurations = new Dictionary<
                string,
                StateStackConfiguration
            >(StringComparer.Ordinal);
            foreach (KeyValuePair<string, StackBuilder> entry in _stacks)
            {
                configurations[entry.Key] = entry.Value.Build();
            }

            return new StateGraph(configurations);
        }

        public sealed class StackBuilder
        {
            private readonly List<IState> _states;
            private readonly HashSet<IState> _uniqueStates;

            private IState _initialState;

            internal StackBuilder(string name)
            {
                Name = name;
                _states = new List<IState>();
                _uniqueStates = new HashSet<IState>();
            }

            public string Name { get; }

            internal bool HasStates => _states.Count > 0;

            public StackBuilder State(IState state, bool setAsInitial = false)
            {
                if (state == null)
                {
                    throw new ArgumentNullException(nameof(state));
                }

                if (_uniqueStates.Add(state))
                {
                    _states.Add(state);
                }

                if (setAsInitial || _initialState == null)
                {
                    _initialState = state;
                }

                return this;
            }

            public StackBuilder Scene(
                string sceneName,
                SceneTransitionMode transitionMode = SceneTransitionMode.Addition,
                bool revertOnRemoval = true,
                LoadSceneParameters? loadParameters = null,
                UnloadSceneOptions unloadOptions = UnloadSceneOptions.None,
                bool setAsInitial = false
            )
            {
                if (string.IsNullOrWhiteSpace(sceneName))
                {
                    throw new ArgumentException("Scene name must be provided.", nameof(sceneName));
                }

                SceneState sceneState = new SceneState(
                    sceneName,
                    transitionMode,
                    loadParameters,
                    unloadOptions,
                    revertOnRemoval
                );

                return State(sceneState, setAsInitial);
            }

            public StackBuilder Group(
                string groupName,
                IEnumerable<IState> states,
                StateGroupMode mode,
                TickMode tickMode = TickMode.None,
                bool tickWhenInactive = false,
                bool setAsInitial = false
            )
            {
                if (string.IsNullOrWhiteSpace(groupName))
                {
                    throw new ArgumentException("Group name must be provided.", nameof(groupName));
                }

                StateGroup group = new StateGroup(
                    groupName,
                    states,
                    mode,
                    tickMode,
                    tickWhenInactive
                );

                return State(group, setAsInitial);
            }

            internal StateStackConfiguration Build()
            {
                if (_states.Count == 0)
                {
                    throw new InvalidOperationException(
                        $"Stack '{Name}' has no states configured and cannot be built."
                    );
                }

                IState[] snapshot = _states.ToArray();
                IState initial = _initialState ?? snapshot[0];
                return new StateStackConfiguration(snapshot, initial);
            }
        }
    }

    public sealed class StateGraph
    {
        private readonly IReadOnlyDictionary<string, StateStackConfiguration> _stacks;

        internal StateGraph(IDictionary<string, StateStackConfiguration> stacks)
        {
            if (stacks == null)
            {
                throw new ArgumentNullException(nameof(stacks));
            }

            _stacks = new Dictionary<string, StateStackConfiguration>(
                stacks,
                StringComparer.Ordinal
            );
        }

        public IReadOnlyDictionary<string, StateStackConfiguration> Stacks => _stacks;

        public bool TryGetStack(string name, out StateStackConfiguration configuration)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                configuration = default;
                return false;
            }

            return _stacks.TryGetValue(name, out configuration);
        }
    }
}
