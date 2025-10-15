namespace WallstopStudios.DxState.Pooling
{
    using System;

    public static class WallstopFastArrayPool<T>
    {
        public static T[] Rent(int minimumLength)
        {
            return WallstopArrayPool<T>.Rent(minimumLength, clear: false);
        }

        public static void Return(T[] array)
        {
            WallstopArrayPool<T>.Return(array, clear: false);
        }
    }
}
