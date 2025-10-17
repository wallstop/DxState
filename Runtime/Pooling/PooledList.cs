namespace WallstopStudios.DxState.Pooling
{
    using System;
    using System.Collections;
    using System.Collections.Generic;

    public sealed class PooledList<T> : IReadOnlyList<T>, IDisposable
    {
        private const int DefaultCapacity = 8;

        private T[] _buffer;
        private int _count;
        private bool _disposed;

        private PooledList(T[] buffer)
        {
            _buffer = buffer;
            _count = 0;
            _disposed = false;
        }

        public static PooledList<T> Rent(int minimumCapacity = DefaultCapacity, bool clear = false)
        {
            int capacity = minimumCapacity > 0 ? minimumCapacity : DefaultCapacity;
            T[] buffer = WallstopArrayPool<T>.Rent(capacity, clear);
            if (buffer.Length < capacity)
            {
                WallstopArrayPool<T>.Return(buffer, clear: false);
                buffer = new T[capacity];
            }

            return new PooledList<T>(buffer);
        }

        public int Count => _count;

        public T this[int index]
        {
            get
            {
                EnsureNotDisposed();
                if (index < 0 || index >= _count)
                {
                    throw new ArgumentOutOfRangeException(nameof(index));
                }

                return _buffer[index];
            }
        }

        public void Add(T item)
        {
            EnsureNotDisposed();
            EnsureCapacity(_count + 1);
            _buffer[_count] = item;
            _count++;
        }

        public void Clear()
        {
            EnsureNotDisposed();
            if (_count > 0)
            {
                Array.Clear(_buffer, 0, _count);
            }
            _count = 0;
        }

        public Enumerator GetEnumerator()
        {
            EnsureNotDisposed();
            return new Enumerator(this);
        }

        IEnumerator<T> IEnumerable<T>.GetEnumerator()
        {
            return GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            T[] buffer = _buffer;
            _buffer = Array.Empty<T>();
            _count = 0;
            if (buffer.Length > 0)
            {
                WallstopArrayPool<T>.Return(buffer, clear: true);
            }
        }

        private void EnsureCapacity(int required)
        {
            if (required <= _buffer.Length)
            {
                return;
            }

            int newCapacity = Math.Max(required, _buffer.Length * 2);
            T[] newBuffer = WallstopArrayPool<T>.Rent(newCapacity, clear: false);
            if (newBuffer.Length < newCapacity)
            {
                newBuffer = new T[newCapacity];
            }

            Array.Copy(_buffer, newBuffer, _count);
            WallstopArrayPool<T>.Return(_buffer, clear: false);
            _buffer = newBuffer;
        }

        private void EnsureNotDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(PooledList<T>));
            }
        }

        public struct Enumerator : IEnumerator<T>
        {
            private readonly PooledList<T> _owner;
            private readonly int _initialCount;
            private int _index;

            internal Enumerator(PooledList<T> owner)
            {
                _owner = owner;
                _initialCount = owner._count;
                _index = -1;
            }

            public T Current
            {
                get
                {
                    if (_index < 0 || _index >= _initialCount)
                    {
                        throw new InvalidOperationException();
                    }

                    return _owner._buffer[_index];
                }
            }

            object IEnumerator.Current => Current;

            public bool MoveNext()
            {
                _index++;
                return _index < _initialCount;
            }

            public void Reset()
            {
                _index = -1;
            }

            public void Dispose()
            {
            }
        }
    }
}
