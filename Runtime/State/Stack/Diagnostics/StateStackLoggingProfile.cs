namespace WallstopStudios.DxState.State.Stack.Diagnostics
{
    using UnityEngine;

    [CreateAssetMenu(
        fileName = "StateStackLoggingProfile",
        menuName = "Wallstop Studios/DxState/State Stack Logging Profile"
    )]
    public sealed class StateStackLoggingProfile : ScriptableObject
    {
        [SerializeField]
        private bool _logTransitions = true;

        [SerializeField]
        private bool _logProgress;

        [SerializeField]
        private string _logCategory = "DxState";

        public bool LogTransitions => _logTransitions;

        public bool LogProgress => _logProgress;

        public string LogCategory => _logCategory;
    }
}
