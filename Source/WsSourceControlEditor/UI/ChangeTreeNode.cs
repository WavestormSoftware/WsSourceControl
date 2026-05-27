using FlaxEditor.GUI.Tree;
using FlaxEngine;
using FlaxEngine.GUI;
using WsSourceControlEditor.Git;

namespace WsSourceControlEditor.UI
{
    /// <summary>
    /// Tree node representing a single file change (staged or unstaged).
    /// Shows change type prefix and color-coded file path.
    /// </summary>
    public class ChangeTreeNode : TreeNode
    {
        public readonly GitChange Change;
        public readonly bool IsStaged;

        public ChangeTreeNode(GitChange change)
        {
            Change = change;
            IsStaged = change.Staged;
            Text = $"{GitWrapper.GetChangeTypePrefix(change.Type)}  {change.FilePath}";
            TextColor = GitWrapper.GetChangeColor(change.Type);
        }

        /// <summary>
        /// Returns the full path relative to project root.
        /// </summary>
        public string FilePath => Change.FilePath;
    }
}
