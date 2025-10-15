namespace WallstopStudios.DxState.State.Stack.States.Systems
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using UnityEngine;
#if ENABLE_INPUT_SYSTEM
    using UnityEngine.InputSystem;
#endif

    [Serializable]
    public sealed class InputModeState : IState
    {
        [SerializeField]
        private string _name;

        [SerializeField]
        private bool _tickWhenInactive;

#if ENABLE_INPUT_SYSTEM
        [SerializeField]
        private InputActionAsset _actionAsset;

        [SerializeField]
        private string _actionMapId;
#endif

        public InputModeState(string name)
        {
            _name = name ?? throw new ArgumentNullException(nameof(name));
        }

        public string Name => _name;

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
#if ENABLE_INPUT_SYSTEM
            EnableActionMap();
#endif
            progress.Report(1f);
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
#if ENABLE_INPUT_SYSTEM
            DisableActionMap();
#endif
            progress.Report(1f);
            return default;
        }

        public ValueTask Remove<TProgress>(
            IReadOnlyList<IState> previousStatesInStack,
            IReadOnlyList<IState> nextStatesInStack,
            TProgress progress
        )
            where TProgress : IProgress<float>
        {
#if ENABLE_INPUT_SYSTEM
            DisableActionMap();
#endif
            progress.Report(1f);
            return default;
        }

#if ENABLE_INPUT_SYSTEM
        private void EnableActionMap()
        {
            if (_actionAsset == null || string.IsNullOrEmpty(_actionMapId))
            {
                return;
            }

            InputActionMap map = _actionAsset.FindActionMap(_actionMapId, throwIfNotFound: false);
            if (map == null)
            {
                Debug.LogWarning($"InputModeState: action map '{_actionMapId}' not found.");
                return;
            }

            map.Enable();
        }

        private void DisableActionMap()
        {
            if (_actionAsset == null || string.IsNullOrEmpty(_actionMapId))
            {
                return;
            }

            InputActionMap map = _actionAsset.FindActionMap(_actionMapId, throwIfNotFound: false);
            if (map == null)
            {
                return;
            }

            map.Disable();
        }
#endif
    }
}
