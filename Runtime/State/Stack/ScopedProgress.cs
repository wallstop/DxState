namespace WallstopStudios.DxState.State.Stack
{
    using System;
    using UnityEngine;

    public readonly struct ScopedProgress : IProgress<float>
    {
        private readonly IProgress<float> _parent;
        private readonly float _offset;
        private readonly float _scale;

        public ScopedProgress(IProgress<float> parent, float offset, float scale)
        {
            _parent = parent;
            _offset = Mathf.Clamp01(offset);
            _scale = Mathf.Clamp01(scale);
        }

        public void Report(float value)
        {
            float clampedValue = Mathf.Clamp01(value);
            _parent?.Report(_offset + clampedValue * _scale);
        }
    }
}
