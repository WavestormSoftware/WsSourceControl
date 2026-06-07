using LibGit2Sharp;
using WsSourceControl.Git;

var tests = new List<(string Name, Action Body)>
{
    ("init creates repository and Flax gitignore once", TestInitAndGitIgnore),
    ("status maps staged and unstaged files", TestStatusMapping),
    ("commit and amend validate staged changes", TestCommitAndAmend),
    ("branch create checkout delete guards current branch", TestBranchOperations),
    ("stash save apply pop drop", TestStashOperations),
    ("ahead behind with local bare remote", TestAheadBehind),
    ("lfs patterns and pointer files are detected", TestLfsDetection),
};

var failed = 0;
foreach (var test in tests)
{
    try
    {
        test.Body();
        Console.WriteLine($"PASS {test.Name}");
    }
    catch (Exception ex)
    {
        failed++;
        Console.Error.WriteLine($"FAIL {test.Name}");
        Console.Error.WriteLine(ex);
    }
}

if (failed > 0)
    Environment.Exit(1);

static void TestInitAndGitIgnore()
{
    using var temp = TempRepo(createRepo: false);
    var client = new LibGit2SharpGitClient(temp.Path);
    Assert(!client.IsRepository(), "repo should not exist before init");
    Assert(client.InitializeRepository().Success, "init should succeed");
    Assert(client.IsRepository(), "repo should exist after init");
    var ignorePath = Path.Combine(temp.Path, ".gitignore");
    var first = File.ReadAllText(ignorePath);
    client.InitializeRepository();
    var second = File.ReadAllText(ignorePath);
    Assert(first == second, "gitignore block should not duplicate");
    Assert(second.Contains("/Cache/"), "Flax cache ignore should be present");
}

static void TestStatusMapping()
{
    using var temp = TempRepo();
    var client = new LibGit2SharpGitClient(temp.Path);
    File.WriteAllText(Path.Combine(temp.Path, "tracked.txt"), "one\n");
    client.Stage(new[] { "tracked.txt" });
    client.Commit("initial", false);

    File.WriteAllText(Path.Combine(temp.Path, "tracked.txt"), "two\n");
    File.WriteAllText(Path.Combine(temp.Path, "new.txt"), "new\n");
    var status = client.GetStatus();
    Assert(status.Any(x => x.FilePath == "tracked.txt" && x.Type == GitChangeType.Modified && !x.Staged), "modified tracked file missing");
    Assert(status.Any(x => x.FilePath == "new.txt" && x.Type == GitChangeType.Untracked && !x.Staged), "untracked file missing");

    client.Stage(new[] { "new.txt" });
    status = client.GetStatus();
    Assert(status.Any(x => x.FilePath == "new.txt" && x.Type == GitChangeType.Added && x.Staged), "staged added file missing");
    client.Unstage(new[] { "new.txt" });
    status = client.GetStatus();
    Assert(status.Any(x => x.FilePath == "new.txt" && !x.Staged), "unstaged file missing after unstage");
}

static void TestCommitAndAmend()
{
    using var temp = TempRepo();
    var client = new LibGit2SharpGitClient(temp.Path);
    Assert(!client.Commit("", false).Success, "empty message should fail");
    Assert(!client.Commit("nothing", false).Success, "commit without staged changes should fail");

    File.WriteAllText(Path.Combine(temp.Path, "a.txt"), "a\n");
    client.Stage(new[] { "a.txt" });
    Assert(client.Commit("first", false).Success, "commit should succeed");
    File.WriteAllText(Path.Combine(temp.Path, "b.txt"), "b\n");
    client.Stage(new[] { "b.txt" });
    Assert(client.Commit("first amended", true).Success, "amend should succeed");
    Assert(client.GetLog(10).Count == 1, "amend should keep a single commit");
}

static void TestBranchOperations()
{
    using var temp = TempRepoWithCommit();
    var client = new LibGit2SharpGitClient(temp.Path);
    var originalBranch = client.GetSnapshot().BranchName;
    Assert(client.CreateBranch("feature").Success, "branch create should succeed");
    Assert(client.GetSnapshot().BranchName == "feature", "new branch should be checked out");
    Assert(!client.DeleteBranch("feature").Success, "current branch delete should fail");
    Assert(client.CheckoutBranch(originalBranch).Success, "checkout original branch should succeed");
    Assert(client.DeleteBranch("feature").Success, "non-current branch delete should succeed");
}

static void TestStashOperations()
{
    using var temp = TempRepoWithCommit();
    var client = new LibGit2SharpGitClient(temp.Path);
    File.WriteAllText(Path.Combine(temp.Path, "tracked.txt"), "changed\n");
    Assert(client.Stash("test stash").Success, "stash should succeed");
    Assert(client.GetStashes().Count == 1, "stash should be listed");
    Assert(client.StashApply(0).Success, "stash apply should succeed");
    Assert(client.GetStashes().Count == 1, "apply should keep stash");
    client.Discard(new[] { "tracked.txt" });
    Assert(client.StashPop(0).Success, "stash pop should succeed");
    Assert(client.GetStashes().Count == 0, "pop should remove stash");

    client.Stash("second");
    Assert(client.StashDrop(0).Success, "stash drop should succeed");
    Assert(client.GetStashes().Count == 0, "drop should remove stash");
}

static void TestAheadBehind()
{
    using var remote = TempRepo(createRepo: false);
    Repository.Init(remote.Path, isBare: true);
    using var work = TempRepo(createRepo: false);
    Repository.Clone(remote.Path, work.Path);
    ConfigureIdentity(work.Path);
    var client = new LibGit2SharpGitClient(work.Path);
    File.WriteAllText(Path.Combine(work.Path, "a.txt"), "a\n");
    client.Stage(new[] { "a.txt" });
    client.Commit("initial", false);
    client.Push();
    Assert(client.GetSnapshot().Ahead == 0, "branch should not be ahead after push");
    File.WriteAllText(Path.Combine(work.Path, "b.txt"), "b\n");
    client.Stage(new[] { "b.txt" });
    client.Commit("second", false);
    Assert(client.GetSnapshot().Ahead == 1, "branch should be ahead by one");
}

static void TestLfsDetection()
{
    using var temp = TempRepo();
    var client = new LibGit2SharpGitClient(temp.Path);
    File.WriteAllText(Path.Combine(temp.Path, ".gitattributes"), "*.png filter=lfs diff=lfs merge=lfs -text\n");
    File.WriteAllText(Path.Combine(temp.Path, "asset.png"), "version https://git-lfs.github.com/spec/v1\noid sha256:abc\nsize 1\n");
    var snapshot = client.GetSnapshot();
    Assert(snapshot.HasLfsConfigured, "LFS pattern should be detected");
    Assert(snapshot.LfsPatterns.Contains("*.png"), "LFS pattern should be listed");
    Assert(snapshot.Changes.Any(x => x.FilePath == "asset.png" && x.IsLfsPointer), "LFS pointer should be detected");
}

static TempDirectory TempRepo(bool createRepo = true)
{
    var temp = new TempDirectory();
    if (createRepo)
    {
        Repository.Init(temp.Path);
        ConfigureIdentity(temp.Path);
    }
    return temp;
}

static TempDirectory TempRepoWithCommit()
{
    var temp = TempRepo();
    var client = new LibGit2SharpGitClient(temp.Path);
    File.WriteAllText(Path.Combine(temp.Path, "tracked.txt"), "initial\n");
    client.Stage(new[] { "tracked.txt" });
    client.Commit("initial", false);
    return temp;
}

static void ConfigureIdentity(string path)
{
    using var repo = new Repository(path);
    repo.Config.Set("user.name", "WsSourceControl Tests");
    repo.Config.Set("user.email", "tests@example.invalid");
    if (repo.Branches["master"] != null && repo.Branches["main"] == null)
        repo.Branches.Rename("master", "main");
}

static void Assert(bool condition, string message)
{
    if (!condition)
        throw new InvalidOperationException(message);
}

sealed class TempDirectory : IDisposable
{
    public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "wsc-tests-" + Guid.NewGuid().ToString("N"));

    public TempDirectory()
    {
        Directory.CreateDirectory(Path);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(Path, true);
        }
        catch
        {
        }
    }
}
