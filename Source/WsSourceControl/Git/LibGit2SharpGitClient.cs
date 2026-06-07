using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using LibGit2Sharp;

namespace WsSourceControl.Git
{
    public sealed class LibGit2SharpGitClient : IGitClient
    {
        private const long LargeAssetWarningBytes = 50L * 1024L * 1024L;
        private const string FlaxIgnoreStart = "# WsSourceControl Flax defaults - begin";
        private const string FlaxIgnoreEnd = "# WsSourceControl Flax defaults - end";

        public string ProjectPath { get; }

        public LibGit2SharpGitClient(string projectPath)
        {
            ProjectPath = Path.GetFullPath(projectPath);
        }

        public bool IsRepository() => Repository.Discover(ProjectPath) != null;

        public RepositorySnapshot GetSnapshot()
        {
            if (!IsRepository())
                return new RepositorySnapshot { IsRepository = false };

            return WithRepository(repo =>
            {
                var changes = GetStatus(repo).ToList();
                var conflicts = changes.Where(x => x.IsConflict).ToList();
                var head = repo.Head;
                GetAheadBehind(head, out var ahead, out var behind);
                var lfsPatterns = GetLfsPatterns();

                return new RepositorySnapshot
                {
                    IsRepository = true,
                    IsDetachedHead = repo.Info.IsHeadDetached,
                    BranchName = GetBranchLabel(repo),
                    UpstreamName = head?.TrackedBranch?.FriendlyName ?? string.Empty,
                    RemoteUrl = GetRemoteUrl(repo),
                    Ahead = ahead,
                    Behind = behind,
                    HasConflicts = conflicts.Count > 0,
                    HasLfsConfigured = lfsPatterns.Count > 0,
                    StashCount = repo.Stashes.Count(),
                    Changes = changes,
                    Conflicts = conflicts,
                    LfsPatterns = lfsPatterns,
                    LargeUntrackedFiles = GetLargeUntrackedFiles(changes)
                };
            });
        }

        public IReadOnlyList<GitFileChange> GetStatus()
        {
            if (!IsRepository())
                return Array.Empty<GitFileChange>();
            return WithRepository(repo => GetStatus(repo).ToList());
        }

        public IReadOnlyList<GitCommitInfo> GetLog(int count)
        {
            if (!IsRepository())
                return Array.Empty<GitCommitInfo>();

            return WithRepository(repo => repo.Commits.Take(Math.Max(1, count)).Select(commit => new GitCommitInfo
            {
                Hash = commit.Sha,
                ShortHash = commit.Sha.Substring(0, Math.Min(8, commit.Sha.Length)),
                Author = commit.Author?.Name ?? string.Empty,
                Date = ToRelativeDate(commit.Author.When),
                Message = commit.MessageShort ?? string.Empty,
                ChangedFiles = GetCommitChangedFiles(repo, commit.Sha).ToList()
            }).ToList());
        }

        public string GetCommitDetail(string hash)
        {
            if (!IsRepository())
                return string.Empty;

            return WithRepository(repo =>
            {
                var commit = repo.Lookup<Commit>(hash);
                if (commit == null)
                    return string.Empty;

                var sb = new StringBuilder();
                sb.AppendLine(commit.Sha);
                sb.AppendLine($"{commit.Author.Name} <{commit.Author.Email}>");
                sb.AppendLine(commit.Author.When.ToString("yyyy-MM-dd HH:mm:ss zzz"));
                sb.AppendLine();
                sb.AppendLine(commit.Message?.TrimEnd() ?? string.Empty);
                return sb.ToString();
            });
        }

        public IReadOnlyList<string> GetCommitChangedFiles(string hash)
        {
            if (!IsRepository())
                return Array.Empty<string>();
            return WithRepository(repo => GetCommitChangedFiles(repo, hash).ToList());
        }

        public string GetDiff(string filePath, bool staged, int contextLines)
        {
            if (!IsRepository() || string.IsNullOrWhiteSpace(filePath))
                return string.Empty;

            return WithRepository(repo =>
            {
                if (IsBinaryFile(Path.Combine(ProjectPath, filePath)))
                    return "(Binary file)";

                var paths = new[] { NormalizePath(filePath) };
                var options = new CompareOptions { ContextLines = Math.Max(0, contextLines) };
                Patch patch;
                if (staged)
                {
                    var oldTree = repo.Head.Tip?.Tree;
                    patch = repo.Diff.Compare<Patch>(oldTree, DiffTargets.Index, paths, null, options);
                }
                else
                {
                    patch = repo.Diff.Compare<Patch>(paths, true, null, options);
                }
                return patch?.Content ?? string.Empty;
            });
        }

        public GitOperationResult InitializeRepository()
        {
            try
            {
                if (!IsRepository())
                    Repository.Init(ProjectPath);
                EnsureFlaxGitIgnore(ProjectPath);
                return GitOperationResult.Ok("Repository initialized.");
            }
            catch (Exception ex)
            {
                return GitOperationResult.Fail("Failed to initialize repository.", ex);
            }
        }

        public GitOperationResult Stage(IEnumerable<string> paths)
        {
            return Run(repo =>
            {
                var list = NormalizePaths(paths).ToList();
                if (list.Count == 0)
                    return GitOperationResult.Ok("Nothing to stage.");
                Commands.Stage(repo, list);
                return GitOperationResult.Ok($"Staged {list.Count} file(s).");
            });
        }

        public GitOperationResult StageAll()
        {
            return Run(repo =>
            {
                Commands.Stage(repo, "*");
                return GitOperationResult.Ok("Staged all changes.");
            });
        }

        public GitOperationResult Unstage(IEnumerable<string> paths)
        {
            return Run(repo =>
            {
                var list = NormalizePaths(paths).ToList();
                if (list.Count == 0)
                    return GitOperationResult.Ok("Nothing to unstage.");
                Commands.Unstage(repo, list);
                return GitOperationResult.Ok($"Unstaged {list.Count} file(s).");
            });
        }

        public GitOperationResult Discard(IEnumerable<string> paths)
        {
            return Run(repo =>
            {
                var list = NormalizePaths(paths).Distinct().ToList();
                if (list.Count == 0)
                    return GitOperationResult.Ok("Nothing to discard.");

                foreach (var path in list)
                {
                    var fullPath = Path.Combine(ProjectPath, path);
                    var state = repo.RetrieveStatus(path);
                    if ((state & FileStatus.NewInWorkdir) != 0 && File.Exists(fullPath))
                    {
                        File.Delete(fullPath);
                        continue;
                    }
                    if ((state & FileStatus.NewInWorkdir) != 0 && Directory.Exists(fullPath))
                    {
                        Directory.Delete(fullPath, true);
                        continue;
                    }
                    repo.CheckoutPaths("HEAD", new[] { path }, new CheckoutOptions { CheckoutModifiers = CheckoutModifiers.Force });
                    Commands.Unstage(repo, path);
                }
                return GitOperationResult.Ok($"Discarded {list.Count} file(s).");
            });
        }

        public GitOperationResult ResetHardAndClean()
        {
            return Run(repo =>
            {
                if (repo.Head.Tip != null)
                    repo.Reset(ResetMode.Hard, repo.Head.Tip, new CheckoutOptions { CheckoutModifiers = CheckoutModifiers.Force });

                foreach (var change in GetStatus(repo).Where(x => x.Type == GitChangeType.Untracked))
                {
                    var fullPath = Path.Combine(ProjectPath, change.FilePath);
                    if (File.Exists(fullPath))
                        File.Delete(fullPath);
                    else if (Directory.Exists(fullPath))
                        Directory.Delete(fullPath, true);
                }

                return GitOperationResult.Ok("Reset working tree.");
            });
        }

        public GitOperationResult Commit(string message, bool amend)
        {
            return Run(repo =>
            {
                if (string.IsNullOrWhiteSpace(message))
                    return GitOperationResult.Fail("Commit message cannot be empty.");
                if (!GetStatus(repo).Any(x => x.Staged))
                    return GitOperationResult.Fail("No staged changes to commit.");
                if (GetStatus(repo).Any(x => x.IsConflict))
                    return GitOperationResult.Fail("Resolve merge conflicts before committing.");

                var signature = BuildSignature(repo);
                var options = new CommitOptions { AmendPreviousCommit = amend };
                var commit = repo.Commit(message.Trim(), signature, signature, options);
                return GitOperationResult.Ok($"Committed {commit.Sha.Substring(0, 8)}.");
            });
        }

        public GitOperationResult Fetch(bool prune)
        {
            return Run(repo =>
            {
                var remote = repo.Network.Remotes["origin"] ?? repo.Network.Remotes.FirstOrDefault();
                if (remote == null)
                    return GitOperationResult.Fail("No remote configured.");

                var options = new FetchOptions
                {
                    CredentialsProvider = CredentialsProvider,
                    Prune = prune
                };
                Commands.Fetch(repo, remote.Name, remote.FetchRefSpecs.Select(x => x.Specification), options, "WsSourceControl fetch");
                return GitOperationResult.Ok($"Fetched {remote.Name}.");
            });
        }

        public GitOperationResult Pull()
        {
            return Run(repo =>
            {
                if (repo.Head?.TrackedBranch == null)
                    return GitOperationResult.Fail("Current branch has no upstream.");

                var signature = BuildSignature(repo);
                var result = Commands.Pull(repo, signature, new PullOptions
                {
                    FetchOptions = new FetchOptions { CredentialsProvider = CredentialsProvider }
                });
                return GitOperationResult.Ok($"Pull finished: {result.Status}.");
            });
        }

        public GitOperationResult Push()
        {
            return Run(repo =>
            {
                if (repo.Head?.TrackedBranch == null)
                    return GitOperationResult.Fail("Current branch has no upstream.");
                repo.Network.Push(repo.Head, new PushOptions { CredentialsProvider = CredentialsProvider });
                return GitOperationResult.Ok("Pushed current branch.");
            });
        }

        public IReadOnlyList<GitBranchInfo> GetBranches()
        {
            if (!IsRepository())
                return Array.Empty<GitBranchInfo>();

            return WithRepository(repo => repo.Branches.Select(branch =>
            {
                GetAheadBehind(branch, out var ahead, out var behind);
                return new GitBranchInfo
                {
                    Name = branch.FriendlyName,
                    FriendlyName = branch.FriendlyName,
                    UpstreamName = branch.TrackedBranch?.FriendlyName ?? string.Empty,
                    IsCurrent = branch.IsCurrentRepositoryHead,
                    IsRemote = branch.IsRemote,
                    Ahead = ahead,
                    Behind = behind
                };
            }).OrderBy(x => x.IsRemote).ThenByDescending(x => x.IsCurrent).ThenBy(x => x.FriendlyName).ToList());
        }

        public GitOperationResult CheckoutBranch(string branch)
        {
            return Run(repo =>
            {
                var target = repo.Branches[branch];
                if (target == null && branch.StartsWith("origin/", StringComparison.Ordinal))
                {
                    var localName = branch.Substring(branch.IndexOf('/') + 1);
                    var remoteBranch = repo.Branches[branch];
                    if (remoteBranch == null)
                        return GitOperationResult.Fail($"Branch not found: {branch}");
                    target = repo.CreateBranch(localName, remoteBranch.Tip);
                    repo.Branches.Update(target, b => b.TrackedBranch = remoteBranch.CanonicalName);
                }
                if (target == null)
                    return GitOperationResult.Fail($"Branch not found: {branch}");
                Commands.Checkout(repo, target);
                return GitOperationResult.Ok($"Checked out {target.FriendlyName}.");
            });
        }

        public GitOperationResult CreateBranch(string branch, string fromCommit = null, bool checkout = true)
        {
            return Run(repo =>
            {
                if (string.IsNullOrWhiteSpace(branch))
                    return GitOperationResult.Fail("Branch name cannot be empty.");
                if (repo.Branches[branch] != null)
                    return GitOperationResult.Fail($"Branch already exists: {branch}");

                Commit commit = null;
                if (!string.IsNullOrWhiteSpace(fromCommit))
                    commit = repo.Lookup<Commit>(fromCommit);
                commit ??= repo.Head.Tip;
                if (commit == null)
                    return GitOperationResult.Fail("Cannot create a branch before the first commit.");

                var newBranch = repo.CreateBranch(branch.Trim(), commit);
                if (checkout)
                    Commands.Checkout(repo, newBranch);
                return GitOperationResult.Ok($"Created branch {newBranch.FriendlyName}.");
            });
        }

        public GitOperationResult DeleteBranch(string branch)
        {
            return Run(repo =>
            {
                var target = repo.Branches[branch];
                if (target == null)
                    return GitOperationResult.Fail($"Branch not found: {branch}");
                if (target.IsCurrentRepositoryHead)
                    return GitOperationResult.Fail("Cannot delete the current branch.");
                if (target.IsRemote)
                    return GitOperationResult.Fail("Remote branch deletion is not supported in this pass.");
                repo.Branches.Remove(target);
                return GitOperationResult.Ok($"Deleted branch {branch}.");
            });
        }

        public IReadOnlyList<GitStashInfo> GetStashes()
        {
            if (!IsRepository())
                return Array.Empty<GitStashInfo>();

            return WithRepository(repo => repo.Stashes.Select((stash, index) => new GitStashInfo
            {
                Index = index,
                Message = stash.Message ?? string.Empty,
                BranchName = ParseStashBranch(stash.Message)
            }).ToList());
        }

        public GitOperationResult Stash(string message)
        {
            return Run(repo =>
            {
                if (!GetStatus(repo).Any())
                    return GitOperationResult.Fail("No changes to stash.");
                repo.Stashes.Add(BuildSignature(repo), string.IsNullOrWhiteSpace(message) ? "WsSourceControl stash" : message, StashModifiers.IncludeUntracked);
                return GitOperationResult.Ok("Stashed changes.");
            });
        }

        public GitOperationResult StashApply(int index)
        {
            return Run(repo =>
            {
                repo.Stashes.Apply(index);
                return GitOperationResult.Ok($"Applied stash@{{{index}}}.");
            });
        }

        public GitOperationResult StashPop(int index)
        {
            return Run(repo =>
            {
                repo.Stashes.Pop(index);
                return GitOperationResult.Ok($"Popped stash@{{{index}}}.");
            });
        }

        public GitOperationResult StashDrop(int index)
        {
            return Run(repo =>
            {
                repo.Stashes.Remove(index);
                return GitOperationResult.Ok($"Dropped stash@{{{index}}}.");
            });
        }

        public static void EnsureFlaxGitIgnore(string projectPath)
        {
            var ignorePath = Path.Combine(projectPath, ".gitignore");
            var existing = File.Exists(ignorePath) ? File.ReadAllText(ignorePath) : string.Empty;
            if (existing.Contains(FlaxIgnoreStart))
                return;

            var block = string.Join(Environment.NewLine, new[]
            {
                FlaxIgnoreStart,
                "/Binaries/",
                "/Cache/",
                "/Logs/",
                "/Output/",
                "/Screenshots/",
                "*.HotReload.*",
                "Source/*.Gen.*",
                "imgui.ini",
                "*.csproj",
                "*.sln",
                "*.code-workspace",
                ".vscode/",
                ".idea/",
                "*.user",
                "*.obj",
                "*.pdb",
                "*.cache",
                FlaxIgnoreEnd,
                string.Empty
            });

            if (!string.IsNullOrWhiteSpace(existing) && !existing.EndsWith(Environment.NewLine, StringComparison.Ordinal))
                existing += Environment.NewLine;
            File.WriteAllText(ignorePath, existing + block);
        }

        private GitOperationResult Run(Func<Repository, GitOperationResult> operation)
        {
            try
            {
                if (!IsRepository())
                    return GitOperationResult.Fail("This project is not inside a Git repository.");
                return WithRepository(operation);
            }
            catch (Exception ex)
            {
                return GitOperationResult.Fail(GetGitErrorMessage(ex), ex);
            }
        }

        private static string GetGitErrorMessage(Exception ex)
        {
            var message = ex?.Message ?? "Git operation failed.";
            if (message.IndexOf("credentials", StringComparison.OrdinalIgnoreCase) >= 0 ||
                message.IndexOf("authentication", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "Git authentication failed. LibGit2Sharp cannot show an interactive credential prompt inside Flax. Configure an SSH agent or Git credential helper, or set WS_GIT_USERNAME and WS_GIT_PASSWORD/WS_GIT_TOKEN before starting the editor.";
            }

            return string.IsNullOrWhiteSpace(message) ? "Git operation failed." : message;
        }

        private T WithRepository<T>(Func<Repository, T> func)
        {
            var repoPath = Repository.Discover(ProjectPath);
            if (repoPath == null)
                throw new RepositoryNotFoundException(ProjectPath);
            using var repo = new Repository(repoPath);
            return func(repo);
        }

        private IEnumerable<GitFileChange> GetStatus(Repository repo)
        {
            var status = repo.RetrieveStatus(new StatusOptions
            {
                Show = StatusShowOption.IndexAndWorkDir,
                IncludeUntracked = true,
                RecurseUntrackedDirs = true,
                DetectRenamesInIndex = true,
                DetectRenamesInWorkDir = true,
                ExcludeSubmodules = false
            });

            foreach (var entry in status)
            {
                foreach (var change in ConvertStatusEntry(entry))
                    yield return change;
            }
        }

        private IEnumerable<GitFileChange> ConvertStatusEntry(StatusEntry entry)
        {
            var state = entry.State;
            if ((state & FileStatus.Conflicted) != 0)
            {
                yield return BuildChange(entry, GitChangeType.Conflicted, false, true);
                yield break;
            }

            if ((state & FileStatus.NewInIndex) != 0) yield return BuildChange(entry, GitChangeType.Added, true);
            if ((state & FileStatus.ModifiedInIndex) != 0) yield return BuildChange(entry, GitChangeType.Modified, true);
            if ((state & FileStatus.DeletedFromIndex) != 0) yield return BuildChange(entry, GitChangeType.Deleted, true);
            if ((state & FileStatus.RenamedInIndex) != 0) yield return BuildChange(entry, GitChangeType.Renamed, true);
            if ((state & FileStatus.TypeChangeInIndex) != 0) yield return BuildChange(entry, GitChangeType.TypeChanged, true);

            if ((state & FileStatus.NewInWorkdir) != 0) yield return BuildChange(entry, GitChangeType.Untracked, false);
            if ((state & FileStatus.ModifiedInWorkdir) != 0) yield return BuildChange(entry, GitChangeType.Modified, false);
            if ((state & FileStatus.DeletedFromWorkdir) != 0) yield return BuildChange(entry, GitChangeType.Deleted, false);
            if ((state & FileStatus.RenamedInWorkdir) != 0) yield return BuildChange(entry, GitChangeType.Renamed, false);
            if ((state & FileStatus.TypeChangeInWorkdir) != 0) yield return BuildChange(entry, GitChangeType.TypeChanged, false);
        }

        private GitFileChange BuildChange(StatusEntry entry, GitChangeType type, bool staged, bool conflict = false)
        {
            var path = NormalizePath(entry.FilePath);
            var fullPath = Path.Combine(ProjectPath, path);
            var rename = staged ? entry.HeadToIndexRenameDetails : entry.IndexToWorkDirRenameDetails;
            return new GitFileChange
            {
                Type = type,
                FilePath = path,
                OldFilePath = rename?.OldFilePath,
                Staged = staged,
                IsConflict = conflict,
                IsBinary = IsBinaryFile(fullPath),
                IsLfsPointer = IsLfsPointerFile(fullPath)
            };
        }

        private IEnumerable<string> GetCommitChangedFiles(Repository repo, string hash)
        {
            var commit = repo.Lookup<Commit>(hash);
            if (commit == null)
                yield break;

            var parentTree = commit.Parents.FirstOrDefault()?.Tree;
            var changes = repo.Diff.Compare<TreeChanges>(parentTree, commit.Tree);
            foreach (var change in changes)
                yield return $"{change.Status} {change.Path}";
        }

        private Signature BuildSignature(Repository repo)
        {
            return repo.Config.BuildSignature(DateTimeOffset.Now)
                   ?? new Signature("WsSourceControl", "unknown@example.invalid", DateTimeOffset.Now);
        }

        private Credentials CredentialsProvider(string url, string usernameFromUrl, SupportedCredentialTypes types)
        {
            if ((types & SupportedCredentialTypes.UsernamePassword) != 0)
            {
                var username = Environment.GetEnvironmentVariable("WS_GIT_USERNAME");
                var password = Environment.GetEnvironmentVariable("WS_GIT_PASSWORD") ?? Environment.GetEnvironmentVariable("WS_GIT_TOKEN");
                if (!string.IsNullOrWhiteSpace(username) && !string.IsNullOrWhiteSpace(password))
                {
                    return new UsernamePasswordCredentials
                    {
                        Username = username,
                        Password = password
                    };
                }

                var helperCredentials = GetCredentialsFromGitCredentialHelper(url, usernameFromUrl);
                if (helperCredentials != null)
                    return helperCredentials;
            }

            if ((types & SupportedCredentialTypes.Default) != 0)
                return new DefaultCredentials();

            return null;
        }

        private Credentials GetCredentialsFromGitCredentialHelper(string url, string usernameFromUrl)
        {
            if (string.IsNullOrWhiteSpace(url) || !Uri.TryCreate(url, UriKind.Absolute, out var uri))
                return null;

            if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
                return null;

            try
            {
                var input = new StringBuilder();
                input.AppendLine($"protocol={uri.Scheme}");
                input.AppendLine($"host={uri.Host}");
                if (!string.IsNullOrEmpty(uri.AbsolutePath) && uri.AbsolutePath != "/")
                    input.AppendLine($"path={uri.AbsolutePath.TrimStart('/')}");
                if (!string.IsNullOrWhiteSpace(usernameFromUrl))
                    input.AppendLine($"username={usernameFromUrl}");
                input.AppendLine();

                var startInfo = new ProcessStartInfo
                {
                    FileName = "git",
                    Arguments = "credential fill",
                    WorkingDirectory = ProjectPath,
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using (var process = Process.Start(startInfo))
                {
                    if (process == null)
                        return null;

                    process.StandardInput.Write(input.ToString());
                    process.StandardInput.Close();

                    if (!process.WaitForExit(3000))
                    {
                        try { process.Kill(); } catch { }
                        return null;
                    }

                    if (process.ExitCode != 0)
                        return null;

                    var output = process.StandardOutput.ReadToEnd();
                    var values = ParseCredentialOutput(output);
                    if (!values.TryGetValue("username", out var username) ||
                        !values.TryGetValue("password", out var password) ||
                        string.IsNullOrWhiteSpace(username) ||
                        string.IsNullOrWhiteSpace(password))
                    {
                        return null;
                    }

                    return new UsernamePasswordCredentials
                    {
                        Username = username,
                        Password = password
                    };
                }
            }
            catch
            {
                return null;
            }
        }

        private static Dictionary<string, string> ParseCredentialOutput(string output)
        {
            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrEmpty(output))
                return values;

            var lines = output.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
            foreach (var line in lines)
            {
                var separator = line.IndexOf('=');
                if (separator <= 0)
                    continue;

                values[line.Substring(0, separator)] = line.Substring(separator + 1);
            }

            return values;
        }

        private string GetBranchLabel(Repository repo)
        {
            if (repo.Info.IsHeadDetached)
                return repo.Head.Tip == null ? "(detached)" : $"(detached) {repo.Head.Tip.Sha.Substring(0, 8)}";
            return repo.Head?.FriendlyName ?? "Unknown";
        }

        private string GetRemoteUrl(Repository repo)
        {
            var remote = repo.Network.Remotes["origin"] ?? repo.Network.Remotes.FirstOrDefault();
            return remote?.Url ?? string.Empty;
        }

        private static void GetAheadBehind(Branch branch, out int ahead, out int behind)
        {
            ahead = branch?.TrackingDetails?.AheadBy ?? 0;
            behind = branch?.TrackingDetails?.BehindBy ?? 0;
        }

        private IReadOnlyList<string> GetLfsPatterns()
        {
            var attributesPath = Path.Combine(ProjectPath, ".gitattributes");
            if (!File.Exists(attributesPath))
                return Array.Empty<string>();

            return File.ReadAllLines(attributesPath)
                .Select(x => x.Trim())
                .Where(x => x.Length > 0 && !x.StartsWith("#", StringComparison.Ordinal) && x.Contains("filter=lfs"))
                .Select(x => x.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .ToList();
        }

        private IReadOnlyList<string> GetLargeUntrackedFiles(IEnumerable<GitFileChange> changes)
        {
            var result = new List<string>();
            foreach (var change in changes)
            {
                if (change.Type != GitChangeType.Untracked)
                    continue;
                var fullPath = Path.Combine(ProjectPath, change.FilePath);
                if (File.Exists(fullPath) && new FileInfo(fullPath).Length >= LargeAssetWarningBytes && !change.IsLfsPointer)
                    result.Add(change.FilePath);
            }
            return result;
        }

        private static IEnumerable<string> NormalizePaths(IEnumerable<string> paths)
        {
            return (paths ?? Array.Empty<string>())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(NormalizePath);
        }

        private static string NormalizePath(string path) => path?.Replace('\\', '/').Trim() ?? string.Empty;

        private static bool IsBinaryFile(string path)
        {
            if (!File.Exists(path))
                return false;
            try
            {
                using var stream = File.OpenRead(path);
                var length = Math.Min(8192, stream.Length);
                for (var i = 0; i < length; i++)
                {
                    if (stream.ReadByte() == 0)
                        return true;
                }
            }
            catch
            {
                return false;
            }
            return false;
        }

        private static bool IsLfsPointerFile(string path)
        {
            if (!File.Exists(path))
                return false;
            try
            {
                var info = new FileInfo(path);
                if (info.Length > 1024)
                    return false;
                var text = File.ReadAllText(path);
                return text.StartsWith("version https://git-lfs.github.com/spec/v1", StringComparison.Ordinal);
            }
            catch
            {
                return false;
            }
        }

        private static string ParseStashBranch(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
                return string.Empty;
            const string prefix = "On ";
            if (!message.StartsWith(prefix, StringComparison.Ordinal))
                return string.Empty;
            var rest = message.Substring(prefix.Length);
            var colon = rest.IndexOf(':');
            return colon > 0 ? rest.Substring(0, colon) : rest;
        }

        private static string ToRelativeDate(DateTimeOffset date)
        {
            var span = DateTimeOffset.Now - date;
            if (span.TotalSeconds < 60) return "just now";
            if (span.TotalMinutes < 60) return $"{(int)span.TotalMinutes}m ago";
            if (span.TotalHours < 24) return $"{(int)span.TotalHours}h ago";
            if (span.TotalDays < 30) return $"{(int)span.TotalDays}d ago";
            if (span.TotalDays < 365) return $"{(int)(span.TotalDays / 30)}mo ago";
            return $"{(int)(span.TotalDays / 365)}y ago";
        }
    }
}
