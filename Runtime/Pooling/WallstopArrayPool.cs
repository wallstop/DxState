namespace WallstopStudios.DxState.Pooling
{
    using System;
    using System.Buffers;

    public static class WallstopArrayPool<T>
    {
        public static T[] Rent(int minimumLength, bool clear = true)
        {
            if (minimumLength < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(minimumLength));
            }

            if (minimumLength == 0)
            {
                return Array.Empty<T>();
            }

            T[] buffer = ArrayPool<T>.Shared.Rent(minimumLength);
            if (clear)
            {
                Array.Clear(buffer, 0, buffer.Length);
            }

            return buffer;
        }

        public static void Return(T[] array, bool clear = false)
        {
            if (array == null)
            {
                return;
            }

            if (array.Length == 0)
            {
                return;
            }

            ArrayPool<T>.Shared.Return(array, clear);
        }
    }
}
