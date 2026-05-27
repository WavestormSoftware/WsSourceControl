using FlaxEditor.GUI.Tree;
using FlaxEngine;
using FlaxEngine.GUI;

namespace WsSourceControlEditor.UI
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

        public BranchTreeNode(string branchName, bool isCurrent, bool isRemote)
        {
            BranchName = branchName;
            IsCurrent = isCurrent;
            IsRemote = isRemote;
            Text = isCurrent ? $"\u25CF {branchName}" : $"  {branchName}";
            TextColor = isCurrent ? Style.Current.BorderSelected : Style.Current.Foreground;
        }
    }
}
