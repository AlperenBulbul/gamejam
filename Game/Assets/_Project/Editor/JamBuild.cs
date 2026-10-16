// One-click jam builds: menu "Jam > Build Windows (zip)" / "Jam > Build WebGL (zip)".
// Output: <repo>/Builds/<Product>_<Target>_<ddHHmm>_<commit>.zip  (Builds/ is git-ignored)
// Writes BUILD_INFO.txt with the git commit so the ZIP can be matched to the public repo (rule 3.7 / 5.6).
#if UNITY_EDITOR
using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
using Debug = UnityEngine.Debug;

public static class JamBuild
{
    static string RepoRoot => Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));
    static string BuildsDir => Path.Combine(RepoRoot, "Builds");

    [MenuItem("Jam/Build Windows (zip)")]
    public static void BuildWindows() =>
        Build(BuildTarget.StandaloneWindows64, "Windows", Sanitize(PlayerSettings.productName) + ".exe");

    [MenuItem("Jam/Build WebGL (zip)")]
    public static void BuildWebGL() => Build(BuildTarget.WebGL, "WebGL", null);

    [MenuItem("Jam/Open Builds Folder")]
    public static void OpenBuilds()
    {
        Directory.CreateDirectory(BuildsDir);
        EditorUtility.RevealInFinder(BuildsDir + Path.DirectorySeparatorChar);
    }

    static void Build(BuildTarget target, string label, string exeName)
    {
        var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
        if (scenes.Length == 0)
        {
            EditorUtility.DisplayDialog("Jam Build", "No scenes are enabled in File > Build Settings.\nAdd Main.unity first.", "OK");
            return;
        }

        string hash = Git("rev-parse --short HEAD") ?? "nogit";
        bool dirty = !string.IsNullOrEmpty(Git("status --porcelain"));

        string outDir = Path.Combine(BuildsDir, label);
        if (Directory.Exists(outDir)) Directory.Delete(outDir, true);
        Directory.CreateDirectory(outDir);

        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = exeName == null ? outDir : Path.Combine(outDir, exeName),
            target = target,
            options = BuildOptions.None
        });

        if (report.summary.result != BuildResult.Succeeded)
        {
            Debug.LogError($"[JamBuild] {label} build failed: {report.summary.result}, errors: {report.summary.totalErrors}");
            return;
        }

        // Unity drops debug folders next to the exe that must not ship.
        foreach (var d in Directory.GetDirectories(outDir, "*DoNotShip*"))
            Directory.Delete(d, true);

        File.WriteAllText(Path.Combine(outDir, "BUILD_INFO.txt"),
            $"{PlayerSettings.productName}\n" +
            $"Commit: {hash}{(dirty ? " (+ uncommitted changes)" : "")}\n" +
            $"Built: {DateTime.Now:yyyy-MM-dd HH:mm} (UTC+3)\n" +
            $"Unity: {Application.unityVersion}\n" +
            $"Target: {label}\n");

        string zip = Path.Combine(BuildsDir,
            $"{Sanitize(PlayerSettings.productName)}_{label}_{DateTime.Now:ddHHmm}_{hash}.zip");
        if (File.Exists(zip)) File.Delete(zip);
        ZipFile.CreateFromDirectory(outDir, zip);

        long mb = new FileInfo(zip).Length / (1024 * 1024);
        Debug.Log($"[JamBuild] OK: {zip} ({mb} MB)");
        if (dirty)
            Debug.LogWarning("[JamBuild] Built with UNCOMMITTED changes. Commit + push so the public repo matches this build (rule 3.7).");
        EditorUtility.RevealInFinder(zip);
    }

    static string Git(string args)
    {
        try
        {
            var p = Process.Start(new ProcessStartInfo("git", args)
            {
                WorkingDirectory = RepoRoot,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            });
            string output = p.StandardOutput.ReadToEnd().Trim();
            p.WaitForExit();
            return p.ExitCode == 0 ? output : null;
        }
        catch { return null; }
    }

    static string Sanitize(string s)
    {
        foreach (char c in Path.GetInvalidFileNameChars()) s = s.Replace(c, '_');
        return s.Replace(' ', '_');
    }
}
#endif
