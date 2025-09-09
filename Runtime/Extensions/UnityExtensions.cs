namespace WallstopStudios.DxState.Extensions
{
    using System;
    using System.Threading.Tasks;

    public static class UnityExtensions
    {
        public static async ValueTask AwaitWithProgress<TProgress>(
            this UnityEngine.AsyncOperation operation,
            TProgress progressReporter,
            float total = 1.0f
        )
            where TProgress : IProgress<float>
        {
            if (operation == null)
            {
                progressReporter?.Report(1f);
                return;
            }

            progressReporter?.Report(operation.progress / total);
            while (!operation.isDone)
            {
                progressReporter?.Report(operation.progress / total);
                await Task.Yield();
            }
            progressReporter?.Report(operation.progress / total);
        }
    }
}
