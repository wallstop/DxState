namespace WallstopStudios.DxState.State.Stack.Builder.Templates
{
    using System;
    using System.Collections.Generic;
    using UnityEngine;
    using WallstopStudios.DxState.State.Stack;
    using WallstopStudios.DxState.State.Machine;

    [CreateAssetMenu(
        fileName = "StateGraphTemplate",
        menuName = "Wallstop Studios/DxState/State Graph/Template",
        order = 1
    )]
    public sealed class StateGraphTemplate : ScriptableObject
    {
        public string DisplayName => string.IsNullOrWhiteSpace(_displayName) ? name : _displayName;

        public TemplateCategory Category => _category;

        public string Description => _description;

        public StackDefinitionData StackDefinition => _stackDefinition;

        [SerializeField]
        private string _displayName = "Template";

        [SerializeField]
        [TextArea]
        private string _description;

        [SerializeField]
        private TemplateCategory _category = TemplateCategory.None;

        [SerializeField]
        private StackDefinitionData _stackDefinition = new StackDefinitionData();

        public enum TemplateCategory
        {
            None = 0,
            Hierarchical = 1,
            Trigger = 2,
        }

        public enum StateNodeKind
        {
            Default = 0,
            Hierarchical = 1,
            Trigger = 2,
        }

        [Serializable]
        public sealed class StackDefinitionData
        {
            public string Name => _name;

            public IReadOnlyList<StateDefinition> States => _states;

            public IReadOnlyList<TransitionDefinition> Transitions => _transitions;

            [SerializeField]
            private string _name = "New Stack";

            [SerializeField]
            private List<StateDefinition> _states = new List<StateDefinition>();

            [SerializeField]
            private List<TransitionDefinition> _transitions = new List<TransitionDefinition>();

#if UNITY_EDITOR
            public void SetName(string name)
            {
                _name = name;
            }

            public StateDefinition AddState(
                string name,
                StateNodeKind kind,
                bool setAsInitial,
                TickMode tickMode,
                bool tickWhenInactive,
                string notes
            )
            {
                StateDefinition definition = new StateDefinition();
                definition.SetName(name);
                definition.SetKind(kind);
                definition.SetIsInitial(setAsInitial);
                definition.SetTickMode(tickMode);
                definition.SetTickWhenInactive(tickWhenInactive);
                definition.SetNotes(notes);
                _states.Add(definition);
                return definition;
            }

            public TransitionDefinition AddTransition(
                int fromIndex,
                int toIndex,
                string label,
                string tooltip,
                TransitionCause cause,
                TransitionFlags flags
            )
            {
                TransitionDefinition definition = new TransitionDefinition();
                definition.SetSource(fromIndex);
                definition.SetDestination(toIndex);
                definition.SetLabel(label);
                definition.SetTooltip(tooltip);
                definition.SetCause(cause);
                definition.SetFlags(flags);
                _transitions.Add(definition);
                return definition;
            }

            public void Clear()
            {
                _states.Clear();
                _transitions.Clear();
            }
#endif
        }

        [Serializable]
        public sealed class StateDefinition
        {
            public string Name => _name;

            public StateNodeKind Kind => _kind;

            public bool SetAsInitial => _setAsInitial;

            public TickMode TickMode => _tickMode;

            public bool TickWhenInactive => _tickWhenInactive;

            public string Notes => _notes;

            [SerializeField]
            private string _name = "State";

            [SerializeField]
            private StateNodeKind _kind = StateNodeKind.Default;

            [SerializeField]
            private bool _setAsInitial;

            [SerializeField]
            private TickMode _tickMode = TickMode.None;

            [SerializeField]
            private bool _tickWhenInactive;

            [SerializeField]
            [TextArea]
            private string _notes;

#if UNITY_EDITOR
            public void SetName(string value)
            {
                _name = value;
            }

            public void SetKind(StateNodeKind value)
            {
                _kind = value;
            }

            public void SetIsInitial(bool value)
            {
                _setAsInitial = value;
            }

            public void SetTickMode(TickMode value)
            {
                _tickMode = value;
            }

            public void SetTickWhenInactive(bool value)
            {
                _tickWhenInactive = value;
            }

            public void SetNotes(string value)
            {
                _notes = value;
            }
#endif
        }

        [Serializable]
        public sealed class TransitionDefinition
        {
            public int FromIndex => _fromIndex;

            public int ToIndex => _toIndex;

            public string Label => _label;

            public string Tooltip => _tooltip;

            public TransitionCause Cause => _cause;

            public TransitionFlags Flags => _flags;

            [SerializeField]
            private int _fromIndex;

            [SerializeField]
            private int _toIndex;

            [SerializeField]
            private string _label;

            [SerializeField]
            private string _tooltip;

            [SerializeField]
            private TransitionCause _cause = TransitionCause.RuleSatisfied;

            [SerializeField]
            private TransitionFlags _flags = TransitionFlags.None;

#if UNITY_EDITOR
            public void SetSource(int index)
            {
                _fromIndex = Mathf.Max(0, index);
            }

            public void SetDestination(int index)
            {
                _toIndex = Mathf.Max(0, index);
            }

            public void SetLabel(string value)
            {
                _label = value;
            }

            public void SetTooltip(string value)
            {
                _tooltip = value;
            }

            public void SetCause(TransitionCause cause)
            {
                _cause = cause;
            }

            public void SetFlags(TransitionFlags flags)
            {
                _flags = flags;
            }
#endif
        }

#if UNITY_EDITOR
        public void SetDisplayName(string name)
        {
            _displayName = name;
        }

        public void SetDescription(string description)
        {
            _description = description;
        }

        public void SetCategory(TemplateCategory category)
        {
            _category = category;
        }
#endif
    }
}
