namespace WallstopStudios.DxState.State.Stack.States.Animator
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using UnityEngine;

    [Serializable]
    public sealed class AnimatorParameterState : IState
    {
        [SerializeField]
        private string _name;

        [SerializeField]
        private Animator _animator;

        [SerializeField]
        private AnimatorControllerParameterType _parameterType = AnimatorControllerParameterType.Trigger;

        [SerializeField]
        private string _parameterName;

        [SerializeField]
        private float _floatValue;

        [SerializeField]
        private int _intValue;

        [SerializeField]
        private bool _boolValue = true;

        [SerializeField]
        private bool _resetOnExit;

        [SerializeField]
        private bool _tickWhenInactive;

        public string Name => string.IsNullOrWhiteSpace(_name) ? nameof(AnimatorParameterState) : _name;

        public TickMode TickMode => TickMode.None;

        public bool TickWhenInactive => _tickWhenInactive;

        public float? TimeInState => null;

        public ValueTask Enter<TProgress>(
            IState previousState,
            TProgress progress,
            StateDirection direction
        )
            where TProgress : IProgress<float>
        {
            SetParameter(_boolValue);
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
            if (_resetOnExit)
            {
                ResetParameter();
            }

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
            if (_resetOnExit)
            {
                ResetParameter();
            }

            progress?.Report(1f);
            return default;
        }

        private void SetParameter(bool triggerValue)
        {
            if (_animator == null || string.IsNullOrEmpty(_parameterName))
            {
                return;
            }

            switch (_parameterType)
            {
                case AnimatorControllerParameterType.Trigger:
                    if (triggerValue)
                    {
                        _animator.SetTrigger(_parameterName);
                    }
                    break;
                case AnimatorControllerParameterType.Bool:
                    _animator.SetBool(_parameterName, _boolValue);
                    break;
                case AnimatorControllerParameterType.Float:
                    _animator.SetFloat(_parameterName, _floatValue);
                    break;
                case AnimatorControllerParameterType.Int:
                    _animator.SetInteger(_parameterName, _intValue);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private void ResetParameter()
        {
            if (_animator == null || string.IsNullOrEmpty(_parameterName))
            {
                return;
            }

            switch (_parameterType)
            {
                case AnimatorControllerParameterType.Trigger:
                    _animator.ResetTrigger(_parameterName);
                    break;
                case AnimatorControllerParameterType.Bool:
                    _animator.SetBool(_parameterName, false);
                    break;
                case AnimatorControllerParameterType.Float:
                    _animator.SetFloat(_parameterName, 0f);
                    break;
                case AnimatorControllerParameterType.Int:
                    _animator.SetInteger(_parameterName, 0);
                    break;
            }
        }
    }
}
