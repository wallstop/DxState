namespace WallstopStudios.DxState.Pooling
{
    using System;

    internal sealed class PooledQueue<T> : IDisposable
    {
        private const int DefaultCapacity = 8;

        private T[] _buffer;
        private int _head;
        private int _count;
        private bool _disposed;

        public PooledQueue(int initialCapacity = DefaultCapacity)
        {
            int capacity = Math.Max(1, initialCapacity);
            _buffer = WallstopArrayPool<T>.Rent(capacity, clear: false);
            if (_buffer.Length == 0)
            {
                _buffer = new T[capacity];
            }
        }

        public int Count => _count;

        public void Enqueue(T item)
        {
            EnsureNotDisposed();
            EnsureCapacity(_count + 1);
            int tail = (_head + _count) % _buffer.Length;
            _buffer[tail] = item;
            _count++;
        }

        public T Dequeue()
        {
            EnsureNotDisposed();
            if (_count == 0)
            {
                throw new InvalidOperationException("Queue is empty.");
            }

            T item = _buffer[_head];
            _buffer[_head] = default;
            _head = (_head + 1) % _buffer.Length;
            _count--;
            return item;
        }

        public bool TryDequeue(out T item)
        {
            EnsureNotDisposed();
            if (_count == 0)
            {
                item = default;
                return false;
            }

            item = Dequeue();
            return true;
        }

        public void Clear()
        {
            EnsureNotDisposed();
            if (_count > 0)
            {
                Array.Clear(_buffer, 0, _buffer.Length);
            }
            _head = 0;
            _count = 0;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            if (_buffer != null)
            {
                WallstopArrayPool<T>.Return(_buffer, clear: true);
                _buffer = Array.Empty<T>();
            }
            _head = 0;
            _count = 0;
        }

        ~PooledQueue()
        {
            Dispose(false);
        }

        private void EnsureCapacity(int required)
        {
            if (required <= _buffer.Length)
            {
                return;
            }

            int newCapacity = Math.Max(required, _buffer.Length * 2);
            T[] newBuffer = WallstopArrayPool<T>.Rent(newCapacity, clear: false);
            if (newBuffer.Length == 0)
            {
                newBuffer = new T[newCapacity];
            }

            for (int i = 0; i < _count; i++)
            {
                newBuffer[i] = _buffer[(_head + i) % _buffer.Length];
            }

            WallstopArrayPool<T>.Return(_buffer, clear: true);
            _buffer = newBuffer;
            _head = 0;
        }

        private void EnsureNotDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(PooledQueue<T>));
            }
        }
    }
}
