namespace WallstopStudios.DxState.Pooling
{
    using System;
    using WallstopStudios.UnityHelpers.Utils;

    public readonly struct PooledArray<T> : IDisposable
    {
        private readonly T[] _buffer;
        private readonly PooledResource<T[]> _lease;
        private readonly bool _hasLease;

        private PooledArray(T[] buffer, PooledResource<T[]> lease, bool hasLease)
        {
            _buffer = buffer;
            _lease = lease;
            _hasLease = hasLease;
        }

        public T[] Array => _buffer;

        public static PooledArray<T> Rent(int minimumLength, bool clear = false)
        {
            if (minimumLength <= 0)
            {
                return new PooledArray<T>(System.Array.Empty<T>(), default, false);
            }

            if (clear)
            {
                PooledResource<T[]> lease = WallstopArrayPool<T>.Get(minimumLength, out T[] buffer);
                return new PooledArray<T>(buffer, lease, true);
            }

            PooledResource<T[]> fastLease = WallstopFastArrayPool<T>.Get(minimumLength, out T[] fastBuffer);
            return new PooledArray<T>(fastBuffer, fastLease, true);
        }

        public void Dispose()
        {
            if (!_hasLease)
            {
                return;
            }

            _lease.Dispose();
        }
    }
}
