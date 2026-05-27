using FlaxEditor.GUI.Tree;
using FlaxEngine;
using FlaxEngine.GUI;
using WsSourceControlEditor.Git;

namespace WsSourceControlEditor.UI
{
    /// <summary>
    /// Tree node representing a single commit in the log history.
    /// Shows short hash, relative date, and author.
    /// </summary>
    public class CommitTreeNode : TreeNode
    {
        public readonly GitLogEntry Entry;

        public CommitTreeNode(GitLogEntry entry)
        {
            Entry = entry;
            Text = $"{entry.Hash}  {entry.Date}  {entry.Author}";
        }

        /// <summary>
        /// First line of the commit message for quick preview.
        /// </summary>
        public string ShortMessage => Entry.Message;
    }
}
