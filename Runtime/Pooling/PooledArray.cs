namespace WallstopStudios.DxState.Pooling
{
    using System;

    public readonly struct PooledArray<T> : IDisposable
    {
        private readonly T[] _buffer;
        private readonly bool _shouldClear;
        private readonly bool _shouldReturn;

        private PooledArray(T[] buffer, bool shouldClear, bool shouldReturn)
        {
            _buffer = buffer;
            _shouldClear = shouldClear;
            _shouldReturn = shouldReturn;
        }

        public T[] Array => _buffer;

        public static PooledArray<T> Rent(int minimumLength, bool clear = false)
        {
            if (minimumLength <= 0)
            {
                return new PooledArray<T>(System.Array.Empty<T>(), false, false);
            }

            T[] buffer = clear
                ? WallstopArrayPool<T>.Rent(minimumLength, clear: true)
                : WallstopFastArrayPool<T>.Rent(minimumLength);
            return new PooledArray<T>(buffer, clear, true);
        }

        public void Dispose()
        {
            if (!_shouldReturn)
            {
                return;
            }

            if (_shouldClear)
            {
                WallstopArrayPool<T>.Return(_buffer, clear: true);
                return;
            }

            WallstopFastArrayPool<T>.Return(_buffer);
        }
    }
}
