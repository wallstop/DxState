namespace WallstopStudios.DxState.State.Stack.States
{
    using System;
    using System.Threading.Tasks;
    using UnityEngine;
    using UnityEngine.SceneManagement;
    using UnityHelpers.Core.Extension;

    public class SceneState : IState
    {
        public virtual string Name { get; set; }
        public virtual float? TimeInState => 0 <= _timeEntered ? Time.time - _timeEntered : null;

        protected float _timeEntered = -1;

        public virtual async ValueTask Enter(IState previousState)
        {
            if (string.IsNullOrEmpty(Name))
            {
                throw new ArgumentException($"Scene name cannot be null/empty", nameof(Name));
            }

            _timeEntered = Time.time;
            await SceneManager.LoadSceneAsync(Name);
        }

        public virtual void Tick(TickMode mode, float delta) { }

        public virtual ValueTask Exit(IState nextState)
        {
            _timeEntered = -1;
            return new ValueTask();
        }
    }
}
