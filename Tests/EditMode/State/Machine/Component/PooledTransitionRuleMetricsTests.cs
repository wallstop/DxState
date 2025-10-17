namespace WallstopStudios.DxState.Tests.EditMode.State.Machine.Component
{
    using NUnit.Framework;
    using WallstopStudios.DxState.State.Machine.Component;

    public sealed class PooledTransitionRuleMetricsTests
    {
        [Test]
        public void MetricsTrackActiveAndPeakCounts()
        {
            PooledTransitionRule.ResetMetrics();

            PooledTransitionRule first = PooledTransitionRule.Rent(() => true);
            PooledTransitionRule second = PooledTransitionRule.Rent(() => false);

            PooledTransitionRuleMetrics metricsWhileActive = PooledTransitionRule.GetMetrics();
            Assert.AreEqual(2, metricsWhileActive.ActiveCount);
            Assert.AreEqual(2, metricsWhileActive.PeakActiveCount);
            Assert.AreEqual(2, metricsWhileActive.TotalRentals);
            Assert.AreEqual(0, metricsWhileActive.TotalReleases);

            ReleaseRule(first);
            ReleaseRule(second);

            PooledTransitionRuleMetrics finalMetrics = PooledTransitionRule.GetMetrics();
            Assert.AreEqual(0, finalMetrics.ActiveCount);
            Assert.AreEqual(2, finalMetrics.PeakActiveCount);
            Assert.AreEqual(2, finalMetrics.TotalRentals);
            Assert.AreEqual(2, finalMetrics.TotalReleases);
        }

        [Test]
        public void ResetMetricsClearsCounters()
        {
            PooledTransitionRule.ResetMetrics();
            PooledTransitionRule rule = PooledTransitionRule.Rent(() => true);
            ReleaseRule(rule);

            PooledTransitionRule.ResetMetrics();
            PooledTransitionRuleMetrics metrics = PooledTransitionRule.GetMetrics();
            Assert.AreEqual(0, metrics.ActiveCount);
            Assert.AreEqual(0, metrics.PeakActiveCount);
            Assert.AreEqual(0, metrics.TotalRentals);
            Assert.AreEqual(0, metrics.TotalReleases);
        }

        private static void ReleaseRule(PooledTransitionRule rule)
        {
            rule.ReleaseForTesting();
        }
    }
}
