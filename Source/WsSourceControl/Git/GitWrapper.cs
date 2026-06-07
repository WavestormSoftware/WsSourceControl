using System;
using System.Collections.Generic;
using System.Linq;
using FlaxEngine;
using FlaxEngine.GUI;

namespace WsSourceControl.Git
{
    public static class GitWrapper
    {
        private static IGitClient _client;

        public static string ProjectPath => Globals.ProjectFolder;

        public static IGitClient Client => _client ??= new LibGit2SharpGitClient(ProjectPath);

        public static void SetClientForTests(IGitClient client)
        {
            _client = client;
        }

        public static bool IsGitRepo() => Client.IsRepository();

        public static RepositorySnapshot GetSnapshot() => Client.GetSnapshot();

        public static GitResult RunGitCommand(string args)
        {
            return GitResult.Fail("Raw Git commands are no longer supported by WsSourceControl. Use IGitClient operations instead.");
        }

        public static int RunGitCommand(string args, out string output, out string error)
        {
            var result = RunGitCommand(args);
            output = result.Output;
            error = result.Error;
            return result.ExitCode;
        }

        public static string GetCurrentBranch() => Client.GetSnapshot().BranchName ?? "Unknown";

        public static bool IsDetachedHead() => Client.GetSnapshot().IsDetachedHead;

        public static string GetRemoteUrl() => Client.GetSnapshot().RemoteUrl ?? string.Empty;

        public static void GetAheadBehind(out int ahead, out int behind)
        {
            var snapshot = Client.GetSnapshot();
            ahead = snapshot.Ahead;
            behind = snapshot.Behind;
        }

        public static List<GitChange> GetStatus() => Client.GetStatus().Cast<GitChange>().ToList();

        public static string GetDiff(string filePath) => Client.GetDiff(filePath, false, 3);

        public static string GetDiffStaged(string filePath) => Client.GetDiff(filePath, true, 3);

        public static List<GitLogEntry> GetLog(int count)
        {
            return Client.GetLog(count).Select(x => new GitLogEntry
            {
                Hash = x.Hash,
                Author = x.Author,
                Date = x.Date,
                Message = x.Message,
                ChangedFiles = x.ChangedFiles
            }).ToList();
        }

        public static string GetCommitDetail(string hash) => Client.GetCommitDetail(hash);

        public static List<string> GetCommitChangedFiles(string hash) => Client.GetCommitChangedFiles(hash).ToList();

        public static bool Add(string[] files)
        {
            var result = Client.Stage(files);
            LogIfFailed("Stage", result);
            return result.Success;
        }

        public static bool AddAll()
        {
            var result = Client.StageAll();
            LogIfFailed("Stage All", result);
            return result.Success;
        }

        public static bool Unstage(string[] files)
        {
            var result = Client.Unstage(files);
            LogIfFailed("Unstage", result);
            return result.Success;
        }

        public static bool Commit(string message)
        {
            var result = Client.Commit(message, false);
            LogIfFailed("Commit", result);
            return result.Success;
        }

        public static bool CommitAmend(string message)
        {
            var result = Client.Commit(message, true);
            LogIfFailed("Commit Amend", result);
            return result.Success;
        }

        public static GitResult Push() => GitResult.FromOperation(Client.Push());

        public static GitResult Pull() => GitResult.FromOperation(Client.Pull());

        public static GitResult Fetch() => GitResult.FromOperation(Client.Fetch(false));

        public static GitResult FetchPrune() => GitResult.FromOperation(Client.Fetch(true));

        public static List<string> GetBranches() => Client.GetBranches().Where(x => !x.IsRemote).Select(x => x.FriendlyName).ToList();

        public static List<string> GetRemoteBranches() => Client.GetBranches().Where(x => x.IsRemote).Select(x => x.FriendlyName).ToList();

        public static List<GitBranchInfo> GetBranchInfos() => Client.GetBranches().ToList();

        public static bool CheckoutBranch(string branch)
        {
            var result = Client.CheckoutBranch(branch);
            LogIfFailed("Checkout", result);
            return result.Success;
        }

        public static bool CreateBranch(string branch)
        {
            var result = Client.CreateBranch(branch);
            LogIfFailed("Create Branch", result);
            return result.Success;
        }

        public static bool CreateBranch(string branch, string fromCommit)
        {
            var result = Client.CreateBranch(branch, fromCommit);
            LogIfFailed("Create Branch", result);
            return result.Success;
        }

        public static bool DeleteBranch(string branch)
        {
            var result = Client.DeleteBranch(branch);
            LogIfFailed("Delete Branch", result);
            return result.Success;
        }

        public static bool Stash()
        {
            var result = Client.Stash("WsSourceControl stash");
            LogIfFailed("Stash", result);
            return result.Success;
        }

        public static bool StashPop() => StashPop(0);

        public static bool StashPop(int index)
        {
            var result = Client.StashPop(index);
            LogIfFailed("Stash Pop", result);
            return result.Success;
        }

        public static bool StashApply(int index)
        {
            var result = Client.StashApply(index);
            LogIfFailed("Stash Apply", result);
            return result.Success;
        }

        public static bool StashDrop(int index)
        {
            var result = Client.StashDrop(index);
            LogIfFailed("Stash Drop", result);
            return result.Success;
        }

        public static List<GitStashEntry> GetStashList()
        {
            return Client.GetStashes().Select(x => new GitStashEntry
            {
                Index = x.Index,
                Message = x.Message,
                BranchName = x.BranchName
            }).ToList();
        }

        public static bool Reset(string filePath)
        {
            var result = Client.Discard(new[] { filePath });
            LogIfFailed("Discard", result);
            return result.Success;
        }

        public static bool ResetHard()
        {
            var result = Client.ResetHardAndClean();
            LogIfFailed("Reset Hard", result);
            return result.Success;
        }

        public static bool HasConflicts() => Client.GetSnapshot().HasConflicts;

        public static List<GitChange> GetConflictFiles() => Client.GetSnapshot().Conflicts.Cast<GitChange>().ToList();

        public static bool InitRepo()
        {
            var result = Client.InitializeRepository();
            LogIfFailed("Init", result);
            return result.Success;
        }

        public static string GetStatusShort()
        {
            return string.Join(Environment.NewLine, Client.GetStatus().Select(x => $"{GetChangeTypePrefix(x.Type)} {(x.Staged ? "S" : "U")} {x.FilePath}"));
        }

        public static string GetChangeTypePrefix(GitChangeType type)
        {
            switch (type)
            {
                case GitChangeType.Added: return "A";
                case GitChangeType.Modified: return "M";
                case GitChangeType.Deleted: return "D";
                case GitChangeType.Renamed: return "R";
                case GitChangeType.Untracked: return "?";
                case GitChangeType.Conflicted: return "!";
                case GitChangeType.TypeChanged: return "T";
                default: return "~";
            }
        }

        public static Color GetChangeColor(GitChangeType type)
        {
            switch (type)
            {
                case GitChangeType.Added: return new Color(0.35f, 0.85f, 0.35f);
                case GitChangeType.Modified: return new Color(0.85f, 0.75f, 0.2f);
                case GitChangeType.Deleted: return new Color(0.9f, 0.3f, 0.3f);
                case GitChangeType.Renamed: return new Color(0.3f, 0.8f, 0.85f);
                case GitChangeType.Conflicted: return new Color(1.0f, 0.25f, 0.2f);
                case GitChangeType.Untracked: return Style.Current.ForegroundGrey;
                default: return Style.Current.Foreground;
            }
        }

        private static void LogIfFailed(string operation, GitOperationResult result)
        {
            if (result.Success)
                return;
            Debug.LogError($"Git {operation} Error: {result.Error}");
        }
    }
}
