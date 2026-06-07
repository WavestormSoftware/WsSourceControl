using System.Collections.Generic;

namespace WsSourceControl.Git
{
    public interface IGitClient
    {
        string ProjectPath { get; }
        bool IsRepository();
        RepositorySnapshot GetSnapshot();
        IReadOnlyList<GitFileChange> GetStatus();
        IReadOnlyList<GitCommitInfo> GetLog(int count);
        string GetCommitDetail(string hash);
        IReadOnlyList<string> GetCommitChangedFiles(string hash);
        string GetDiff(string filePath, bool staged, int contextLines);
        GitOperationResult InitializeRepository();
        GitOperationResult Stage(IEnumerable<string> paths);
        GitOperationResult StageAll();
        GitOperationResult Unstage(IEnumerable<string> paths);
        GitOperationResult Discard(IEnumerable<string> paths);
        GitOperationResult ResetHardAndClean();
        GitOperationResult Commit(string message, bool amend);
        GitOperationResult Fetch(bool prune);
        GitOperationResult Pull();
        GitOperationResult Push();
        IReadOnlyList<GitBranchInfo> GetBranches();
        GitOperationResult CheckoutBranch(string branch);
        GitOperationResult CreateBranch(string branch, string fromCommit = null, bool checkout = true);
        GitOperationResult DeleteBranch(string branch);
        IReadOnlyList<GitStashInfo> GetStashes();
        GitOperationResult Stash(string message);
        GitOperationResult StashApply(int index);
        GitOperationResult StashPop(int index);
        GitOperationResult StashDrop(int index);
    }
}
