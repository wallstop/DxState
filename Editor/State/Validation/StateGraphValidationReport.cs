#if UNITY_EDITOR
namespace WallstopStudios.DxState.Editor.State.Validation
{
    using System.Collections.Generic;
    using System.Linq;

    internal sealed class StateGraphValidationReport
    {
        private readonly List<StateGraphValidationIssue> _issues;

        public StateGraphValidationReport(List<StateGraphValidationIssue> issues)
        {
            _issues = issues ?? new List<StateGraphValidationIssue>();
        }

        public IReadOnlyList<StateGraphValidationIssue> Issues => _issues;

        public bool HasErrors => _issues.Any(issue => issue.Severity == StateGraphValidationSeverity.Error);

        public bool HasWarnings => _issues.Any(issue => issue.Severity == StateGraphValidationSeverity.Warning);

        public int ErrorCount => _issues.Count(issue => issue.Severity == StateGraphValidationSeverity.Error);

        public int WarningCount => _issues.Count(issue => issue.Severity == StateGraphValidationSeverity.Warning);

        public int InfoCount => _issues.Count(issue => issue.Severity == StateGraphValidationSeverity.Info);
    }
}
#endif
