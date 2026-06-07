using FlaxEditor.GUI.Tree;
using FlaxEngine;
using FlaxEngine.GUI;
using WsSourceControl.Git;

namespace WsSourceControl.UI
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
            var shortHash = string.IsNullOrEmpty(entry.Hash) ? string.Empty : entry.Hash.Substring(0, System.Math.Min(8, entry.Hash.Length));
            Text = $"{shortHash}  {ShortMessage}  -  {entry.Author}  {entry.Date}";
            TextColor = Style.Current.Foreground;
        }

        /// <summary>
        /// First line of the commit message for quick preview.
        /// </summary>
        public string ShortMessage => Entry.Message;
    }
}
