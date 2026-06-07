using FlaxEngine;

namespace WsSourceControl.Git
{
    public enum FileGroupingMode
    {
        Directory,
        Flat
    }

    public sealed class WsSourceControlSettings
    {
        [Tooltip("Maximum number of commits loaded into the History tab.")]
        public int HistoryCount = 100;

        [Tooltip("Automatic refresh interval in seconds. Set to 0 to disable.")]
        public float AutoRefreshIntervalSeconds = 15.0f;

        [Tooltip("Number of context lines shown in generated diffs.")]
        public int DiffContextLines = 3;

        [Tooltip("How changed files are grouped in the Changes tab.")]
        public FileGroupingMode GroupingMode = FileGroupingMode.Directory;

        [Tooltip("Require confirmation for discard, delete branch, amend, stash drop, and other risky operations.")]
        public bool ConfirmRiskyActions = true;
    }
}
