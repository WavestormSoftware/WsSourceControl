using System;
using System.Collections.Generic;

namespace WsSourceControl.Git
{
    public enum GitChangeType
    {
        Modified,
        Added,
        Deleted,
        Untracked,
        Renamed,
        Conflicted,
        TypeChanged,
        Unknown
    }

    public class GitChange
    {
        public GitChangeType Type;
        public string FilePath;
        public string OldFilePath;
        public bool Staged;
        public bool IsBinary;
        public bool IsLfsPointer;
        public bool IsConflict;

        public override string ToString() => $"{(Staged ? "[S]" : "[U]")} {Type}: {FilePath}";
    }

    public sealed class GitFileChange : GitChange
    {
    }

    public class GitLogEntry
    {
        public string Hash;
        public string Author;
        public string Date;
        public string Message;
        public IReadOnlyList<string> ChangedFiles = Array.Empty<string>();
    }

    public class GitStashEntry
    {
        public int Index;
        public string Message;
        public string BranchName;

        public override string ToString() => $"stash@{{{Index}}}: {Message}";
    }

    public sealed class GitBranchInfo
    {
        public string Name;
        public string FriendlyName;
        public string UpstreamName;
        public bool IsCurrent;
        public bool IsRemote;
        public int Ahead;
        public int Behind;
    }

    public sealed class GitRemoteInfo
    {
        public string Name;
        public string Url;
    }

    public sealed class GitCommitInfo
    {
        public string Hash;
        public string ShortHash;
        public string Author;
        public string Date;
        public string Message;
        public IReadOnlyList<string> ChangedFiles = Array.Empty<string>();
    }

    public sealed class GitStashInfo
    {
        public int Index;
        public string Message;
        public string BranchName;
    }

    public sealed class RepositorySnapshot
    {
        public bool IsRepository;
        public bool IsDetachedHead;
        public string BranchName;
        public string UpstreamName;
        public string RemoteUrl;
        public int Ahead;
        public int Behind;
        public bool HasConflicts;
        public bool HasLfsConfigured;
        public int StashCount;
        public IReadOnlyList<GitFileChange> Changes = Array.Empty<GitFileChange>();
        public IReadOnlyList<GitFileChange> Conflicts = Array.Empty<GitFileChange>();
        public IReadOnlyList<string> LfsPatterns = Array.Empty<string>();
        public IReadOnlyList<string> LargeUntrackedFiles = Array.Empty<string>();
    }

    public sealed class GitOperationResult
    {
        public bool Success;
        public string Message;
        public string Output;
        public Exception Exception;
        public int ExitCode;
        public bool RefreshRequired;

        public string Error => Exception != null ? Exception.Message : Message;

        public static GitOperationResult Ok(string message = "", string output = "", bool refreshRequired = true)
        {
            return new GitOperationResult
            {
                Success = true,
                Message = message,
                Output = output,
                ExitCode = 0,
                RefreshRequired = refreshRequired
            };
        }

        public static GitOperationResult Fail(string message, Exception exception = null)
        {
            return new GitOperationResult
            {
                Success = false,
                Message = message,
                Exception = exception,
                ExitCode = -1,
                RefreshRequired = false
            };
        }
    }

    public sealed class GitResult
    {
        public bool Success;
        public string Output;
        public string Error;
        public int ExitCode;

        public static GitResult FromOperation(GitOperationResult result)
        {
            return new GitResult
            {
                Success = result.Success,
                Output = result.Output ?? string.Empty,
                Error = result.Success ? string.Empty : result.Error ?? result.Message ?? string.Empty,
                ExitCode = result.ExitCode
            };
        }

        public static GitResult Ok(string output = "") => new GitResult { Success = true, Output = output ?? string.Empty, Error = string.Empty, ExitCode = 0 };

        public static GitResult Fail(string error) => new GitResult { Success = false, Output = string.Empty, Error = error ?? string.Empty, ExitCode = -1 };
    }
}
