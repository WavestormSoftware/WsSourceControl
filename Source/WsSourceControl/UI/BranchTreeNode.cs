using FlaxEditor.GUI.Tree;
using FlaxEngine;
using FlaxEngine.GUI;

namespace WsSourceControl.UI
{
    /// <summary>
    /// Tree node representing a branch (local or remote).
    /// Shows a bullet indicator for the current branch.
    /// </summary>
    public class BranchTreeNode : TreeNode
    {
        public readonly string BranchName;
        public readonly bool IsCurrent;
        public readonly bool IsRemote;

        public BranchTreeNode(string branchName, bool isCurrent, bool isRemote, string upstream = null, int ahead = 0, int behind = 0)
        {
            BranchName = branchName;
            IsCurrent = isCurrent;
            IsRemote = isRemote;
            var marker = isCurrent ? "* " : "  ";
            var scope = isRemote ? "[remote]" : "[local]";
            var tracking = string.IsNullOrEmpty(upstream) ? string.Empty : $"  -> {upstream}";
            var sync = ahead != 0 || behind != 0 ? $"  Up {ahead} Down {behind}" : string.Empty;
            Text = $"{marker}{scope} {branchName}{tracking}{sync}";
            TextColor = isCurrent ? Style.Current.BorderSelected : isRemote ? Style.Current.ForegroundGrey : Style.Current.Foreground;
        }
    }
}
