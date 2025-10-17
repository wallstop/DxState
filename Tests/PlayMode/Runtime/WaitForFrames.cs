namespace WallstopStudios.DxState.Tests.PlayMode.Runtime
{
    using System.Collections;

    public static class CoroutineTestUtilities
    {
        public static IEnumerator WaitForFrames(int count)
        {
            for (int i = 0; i < count; i++)
            {
                yield return null;
            }
        }
    }
}
