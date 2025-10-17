namespace WallstopStudios.DxState.Tests.EditMode.Pooling
{
    using System;
    using NUnit.Framework;
    using WallstopStudios.DxState.Pooling;
    using WallstopStudios.UnityHelpers.Utils;

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
            using (PooledResource<string[]> initialLease = WallstopArrayPool<string>.Get(
                       3,
                       out string[] buffer
                   ))
            {
                buffer[0] = "CachedValue";
            }

            using PooledResource<string[]> reusedLease = WallstopArrayPool<string>.Get(
                3,
                out string[] reused
            );

            Assert.IsNull(reused[0]);
        }
    }
}
