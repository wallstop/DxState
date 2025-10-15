namespace WallstopStudios.DxState.Tests.EditMode.Pooling
{
    using System;
    using NUnit.Framework;
    using WallstopStudios.DxState.Pooling;

    public sealed class PooledArrayTests
    {
        [Test]
        public void RentZeroLengthReturnsEmptyArray()
        {
            using PooledArray<int> pooled = PooledArray<int>.Rent(0);
            Assert.AreSame(Array.Empty<int>(), pooled.Array);
        }

        [Test]
        public void ReturnClearsBufferWhenRequested()
        {
            string[] buffer = WallstopArrayPool<string>.Rent(3, clear: false);
            buffer[0] = "CachedValue";

            WallstopArrayPool<string>.Return(buffer, clear: true);

            Assert.IsNull(buffer[0]);
        }
    }
}
