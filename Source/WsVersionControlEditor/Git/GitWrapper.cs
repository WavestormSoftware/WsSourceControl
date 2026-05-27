using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using FlaxEngine;
using FlaxEngine.GUI;

namespace WsVersionControlEditor.Git
{
    public enum GitChangeType
    {
        Modified,
        Added,
        Deleted,
        Untracked,
        Renamed,
        Unknown
    }

    public class GitChange
    {
        public GitChangeType Type;
        public string FilePath;
        public bool Staged;

        public override string ToString() => $"{(Staged ? "[S]" : "[U]")} {Type}: {FilePath}";
    }

    public class GitLogEntry
    {
        public string Hash;
        public string Author;
        public string Date;
        public string Message;
    }

    public class GitStashEntry
    {
        public int Index;
        public string Message;
        public string BranchName;

        public override string ToString() => $"stash@{{{Index}}}: {Message}";
    }

    public class GitResult
    {
        public bool Success;
        public string Output;
        public string Error;
        public int ExitCode;

        public static GitResult Ok(string output, string error, int exitCode) => new GitResult
        {
            Success = exitCode == 0,
            Output = output,
            Error = error,
            ExitCode = exitCode
        };

        public static GitResult Fail(string error) => new GitResult
        {
            Success = false,
            Output = string.Empty,
            Error = error,
            ExitCode = -1
        };
    }

    public static class GitWrapper
    {
        public static string ProjectPath => Globals.ProjectFolder;

        public static GitResult RunGitCommand(string args)
        {
            string output = string.Empty;
            string error = string.Empty;

            try
            {
                var processInfo = new ProcessStartInfo
                {
                    FileName = "git",
                    Arguments = args,
                    WorkingDirectory = ProjectPath,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using (var process = Process.Start(processInfo))
                {
                    if (process == null)
                        return GitResult.Fail("Failed to start git process. Is git installed and in PATH?");

                    output = process.StandardOutput.ReadToEnd();
                    error = process.StandardError.ReadToEnd();
                    process.WaitForExit();
                    return GitResult.Ok(output, error, process.ExitCode);
                }
            }
            catch (Exception ex)
            {
                return GitResult.Fail(ex.Message);
            }
        }

        public static int RunGitCommand(string args, out string output, out string error)
        {
            var result = RunGitCommand(args);
            output = result.Output;
            error = result.Error;
            return result.ExitCode;
        }

        public static bool IsGitRepo()
        {
            var r = RunGitCommand("rev-parse --is-inside-work-tree");
            return r.Success && r.Output.Trim().Equals("true", StringComparison.OrdinalIgnoreCase);
        }

        public static string GetCurrentBranch()
        {
            var r = RunGitCommand("branch --show-current");
            if (r.Success)
            {
                string branch = r.Output.Trim();
                if (!string.IsNullOrEmpty(branch))
                    return branch;
            }
            var r2 = RunGitCommand("rev-parse --short HEAD");
            if (r2.Success && !string.IsNullOrWhiteSpace(r2.Output))
                return $"(detached) {r2.Output.Trim()}";
            return "Unknown";
        }

        public static bool IsDetachedHead()
        {
            var r = RunGitCommand("symbolic-ref -q HEAD");
            return !r.Success;
        }

        public static string GetRemoteUrl()
        {
            var r = RunGitCommand("remote get-url origin");
            return r.Success ? r.Output.Trim() : string.Empty;
        }

        public static void GetAheadBehind(out int ahead, out int behind)
        {
            ahead = 0;
            behind = 0;

            var r = RunGitCommand("rev-list --left-right --count HEAD...@{upstream}");
            if (!r.Success)
                return;

            string trimmed = r.Output.Trim();
            string[] parts = trimmed.Split(new[] { '\t', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2)
            {
                int.TryParse(parts[0], out ahead);
                int.TryParse(parts[1], out behind);
            }
        }

        public static List<GitChange> GetStatus()
        {
            var changes = new List<GitChange>();
            var r = RunGitCommand("status --porcelain");

            if (!r.Success || string.IsNullOrWhiteSpace(r.Output))
                return changes;

            var lines = r.Output.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                if (line.Length < 4) continue;

                char x = line[0];
                char y = line[1];
                string path = line.Substring(3).Trim();

                if (path.StartsWith("\"") && path.EndsWith("\""))
                    path = path.Substring(1, path.Length - 2);

                // Handle rename/copy: path will contain " -> newpath"
                string originalPath = path;
                int arrowIdx = path.IndexOf(" -> ", StringComparison.Ordinal);
                if (arrowIdx >= 0)
                {
                    originalPath = path.Substring(0, arrowIdx);
                    path = path.Substring(arrowIdx + 4);
                }

                // Staged changes (index column)
                if (x != ' ' && x != '?')
                    changes.Add(new GitChange { Type = ParseChangeType(x), FilePath = path, Staged = true });

                // Unstaged changes (worktree column) — includes untracked '?'
                if (y != ' ')
                    changes.Add(new GitChange { Type = ParseChangeType(y), FilePath = path, Staged = false });
            }

            return changes;
        }

        public static string GetDiff(string filePath)
        {
            string safePath = filePath.Replace("\"", "\\\"");
            var r = RunGitCommand($"diff -- \"{safePath}\"");
            return r.Success ? r.Output : string.Empty;
        }

        public static string GetDiffStaged(string filePath)
        {
            string safePath = filePath.Replace("\"", "\\\"");
            var r = RunGitCommand($"diff --cached -- \"{safePath}\"");
            return r.Success ? r.Output : string.Empty;
        }

        public static List<GitLogEntry> GetLog(int count)
        {
            var entries = new List<GitLogEntry>();
            var r = RunGitCommand($"log --pretty=format:\"%h|%an|%ar|%s\" -n {count}");

            if (!r.Success || string.IsNullOrWhiteSpace(r.Output))
                return entries;

            var lines = r.Output.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                string clean = line.Trim();
                if (clean.StartsWith("\"") && clean.EndsWith("\""))
                    clean = clean.Substring(1, clean.Length - 2);

                string[] parts = clean.Split(new[] { '|' }, 4);
                if (parts.Length < 4) continue;

                entries.Add(new GitLogEntry
                {
                    Hash = parts[0],
                    Author = parts[1],
                    Date = parts[2],
                    Message = parts[3]
                });
            }

            return entries;
        }

        public static string GetCommitDetail(string hash)
        {
            var r = RunGitCommand($"show --stat --pretty=format:\"%H%n%an <%ae>%n%ai%n%n%s%n%n%b\" {hash}");
            return r.Success ? r.Output : string.Empty;
        }

        public static List<string> GetCommitChangedFiles(string hash)
        {
            var files = new List<string>();
            var r = RunGitCommand($"diff-tree --no-commit-id --name-status -r {hash}");
            if (!r.Success || string.IsNullOrWhiteSpace(r.Output))
                return files;

            var lines = r.Output.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                string trimmed = line.Trim();
                if (trimmed.Length < 3) continue;
                files.Add(trimmed);
            }
            return files;
        }

        public static bool Add(string[] files)
        {
            if (files == null || files.Length == 0) return true;

            string args = "add";
            foreach (var f in files)
                args += $" \"{f}\"";

            var r = RunGitCommand(args);
            if (!r.Success) FlaxEngine.Debug.LogError($"Git Add Error: {r.Error}");
            return r.Success;
        }

        public static bool AddAll()
        {
            var r = RunGitCommand("add .");
            if (!r.Success) FlaxEngine.Debug.LogError($"Git Add All Error: {r.Error}");
            return r.Success;
        }

        public static bool Unstage(string[] files)
        {
            if (files == null || files.Length == 0) return true;

            string args = "restore --staged";
            foreach (var f in files)
                args += $" \"{f}\"";

            var r = RunGitCommand(args);
            if (!r.Success) FlaxEngine.Debug.LogError($"Git Unstage Error: {r.Error}");
            return r.Success;
        }

        public static bool Commit(string message)
        {
            string safeMsg = message.Replace("\"", "\\\"");
            var r = RunGitCommand($"commit -m \"{safeMsg}\"");
            if (!r.Success) FlaxEngine.Debug.LogError($"Git Commit Error: {r.Error}");
            return r.Success;
        }

        public static bool CommitAmend(string message)
        {
            string safeMsg = message.Replace("\"", "\\\"");
            var r = RunGitCommand($"commit --amend -m \"{safeMsg}\"");
            if (!r.Success) FlaxEngine.Debug.LogError($"Git Commit Amend Error: {r.Error}");
            return r.Success;
        }

        public static GitResult Push()
        {
            var r = RunGitCommand("push");
            if (!r.Success) FlaxEngine.Debug.LogError($"Git Push Error: {r.Error}");
            return r;
        }

        public static GitResult Pull()
        {
            var r = RunGitCommand("pull");
            if (!r.Success) FlaxEngine.Debug.LogError($"Git Pull Error: {r.Error}");
            return r;
        }

        public static GitResult Fetch()
        {
            var r = RunGitCommand("fetch");
            if (!r.Success) FlaxEngine.Debug.LogError($"Git Fetch Error: {r.Error}");
            return r;
        }

        public static List<string> GetBranches()
        {
            var branches = new List<string>();
            var r = RunGitCommand("branch");
            if (!r.Success || string.IsNullOrWhiteSpace(r.Output))
                return branches;

            var lines = r.Output.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                string name = line.Trim().TrimStart('*').Trim();
                if (!string.IsNullOrEmpty(name))
                    branches.Add(name);
            }
            return branches;
        }

        public static List<string> GetRemoteBranches()
        {
            var branches = new List<string>();
            var r = RunGitCommand("branch -r");
            if (!r.Success || string.IsNullOrWhiteSpace(r.Output))
                return branches;

            var lines = r.Output.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                string name = line.Trim();
                if (!string.IsNullOrEmpty(name) && !name.Contains("->"))
                    branches.Add(name);
            }
            return branches;
        }

        public static bool CheckoutBranch(string branch)
        {
            var r = RunGitCommand($"checkout \"{branch}\"");
            if (!r.Success) FlaxEngine.Debug.LogError($"Git Checkout Error: {r.Error}");
            return r.Success;
        }

        public static bool CreateBranch(string branch)
        {
            var r = RunGitCommand($"checkout -b \"{branch}\"");
            if (!r.Success) FlaxEngine.Debug.LogError($"Git Create Branch Error: {r.Error}");
            return r.Success;
        }

        public static bool DeleteBranch(string branch)
        {
            var r = RunGitCommand($"branch -d \"{branch}\"");
            if (!r.Success) FlaxEngine.Debug.LogError($"Git Delete Branch Error: {r.Error}");
            return r.Success;
        }

        public static bool Stash()
        {
            var r = RunGitCommand("stash");
            if (!r.Success) FlaxEngine.Debug.LogError($"Git Stash Error: {r.Error}");
            return r.Success;
        }

        public static bool StashPop()
        {
            var r = RunGitCommand("stash pop");
            if (!r.Success) FlaxEngine.Debug.LogError($"Git Stash Pop Error: {r.Error}");
            return r.Success;
        }

        public static bool StashApply(int index)
        {
            var r = RunGitCommand($"stash apply stash@{{{index}}}");
            if (!r.Success) FlaxEngine.Debug.LogError($"Git Stash Apply Error: {r.Error}");
            return r.Success;
        }

        public static bool StashDrop(int index)
        {
            var r = RunGitCommand($"stash drop stash@{{{index}}}");
            if (!r.Success) FlaxEngine.Debug.LogError($"Git Stash Drop Error: {r.Error}");
            return r.Success;
        }

        public static List<GitStashEntry> GetStashList()
        {
            var stashes = new List<GitStashEntry>();
            var r = RunGitCommand("stash list");
            if (!r.Success || string.IsNullOrWhiteSpace(r.Output))
                return stashes;

            var lines = r.Output.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                string entry = line.Trim();
                int colonIdx = entry.IndexOf(':');
                if (colonIdx < 0) continue;

                string header = entry.Substring(0, colonIdx).Trim();
                string message = entry.Substring(colonIdx + 1).Trim();

                int braceOpen = header.IndexOf('{');
                int braceClose = header.IndexOf('}');
                int idx = -1;
                if (braceOpen >= 0 && braceClose > braceOpen)
                    int.TryParse(header.Substring(braceOpen + 1, braceClose - braceOpen - 1), out idx);

                string branchName = string.Empty;
                int onIdx = message.IndexOf("On ", StringComparison.Ordinal);
                if (onIdx == 0)
                {
                    int spaceIdx = message.IndexOf(' ', 3);
                    if (spaceIdx > 0)
                        branchName = message.Substring(3, spaceIdx - 3);
                    else
                        branchName = message.Substring(3);
                }

                stashes.Add(new GitStashEntry
                {
                    Index = idx,
                    Message = message,
                    BranchName = branchName
                });
            }
            return stashes;
        }

        public static bool Reset(string filePath)
        {
            string safePath = filePath.Replace("\"", "\\\"");

            // Check if the file is untracked — git checkout doesn't work for untracked files
            var statusResult = RunGitCommand($"status --porcelain -- \"{safePath}\"");
            if (statusResult.Success && !string.IsNullOrWhiteSpace(statusResult.Output))
            {
                string statusLine = statusResult.Output.Trim();
                if (statusLine.Length >= 2 && statusLine[0] == '?' && statusLine[1] == '?')
                {
                    // Untracked file: remove it with git clean
                    var r = RunGitCommand($"clean -f -- \"{safePath}\"");
                    if (!r.Success) FlaxEngine.Debug.LogError($"Git Clean Error: {r.Error}");
                    return r.Success;
                }
            }

            var result = RunGitCommand($"checkout -- \"{safePath}\"");
            if (!result.Success) FlaxEngine.Debug.LogError($"Git Reset Error: {result.Error}");
            return result.Success;
        }

        public static bool ResetHard()
        {
            var r = RunGitCommand("reset --hard HEAD");
            if (!r.Success)
            {
                FlaxEngine.Debug.LogError($"Git Reset Hard Error: {r.Error}");
                return false;
            }

            // Also clean untracked files and directories
            var clean = RunGitCommand("clean -fd");
            if (!clean.Success) FlaxEngine.Debug.LogWarning($"Git Clean Warning: {clean.Error}");
            return true;
        }

        public static bool HasConflicts()
        {
            var r = RunGitCommand("ls-files --unmerged");
            return r.Success && !string.IsNullOrWhiteSpace(r.Output);
        }

        public static List<GitChange> GetConflictFiles()
        {
            var conflicts = new List<GitChange>();
            var r = RunGitCommand("diff --name-only --diff-filter=U");
            if (!r.Success || string.IsNullOrWhiteSpace(r.Output))
                return conflicts;

            var lines = r.Output.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                string path = line.Trim();
                if (!string.IsNullOrEmpty(path))
                    conflicts.Add(new GitChange { Type = GitChangeType.Modified, FilePath = path, Staged = false });
            }
            return conflicts;
        }

        public static bool InitRepo()
        {
            var r = RunGitCommand("init");
            if (!r.Success) FlaxEngine.Debug.LogError($"Git Init Error: {r.Error}");
            return r.Success;
        }

        public static string GetStatusShort()
        {
            var r = RunGitCommand("status --porcelain");
            return r.Success ? r.Output.Trim() : string.Empty;
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
                case GitChangeType.Untracked: return Style.Current.ForegroundGrey;
                default: return Style.Current.Foreground;
            }
        }

        private static GitChangeType ParseChangeType(char c)
        {
            switch (c)
            {
                case 'M': return GitChangeType.Modified;
                case 'A': return GitChangeType.Added;
                case 'D': return GitChangeType.Deleted;
                case 'R': return GitChangeType.Renamed;
                case 'C': return GitChangeType.Added; // Copy shows as added
                case '?': return GitChangeType.Untracked;
                default: return GitChangeType.Unknown;
            }
        }
    }
}
