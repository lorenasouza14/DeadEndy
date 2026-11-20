using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;

[InitializeOnLoad]
public static class PraiaLightingBuild
{
    static PraiaLightingBuild() { EditorApplication.update += Poll; }
    static void Poll()
    {
        const string request = "Logs/praia-final-build-request.txt";
        if (!File.Exists(request) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
        File.Delete(request);
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray(),
            target = BuildTarget.StandaloneWindows64,
            locationPathName = "Builds/DeadEndy/DeadEndy.exe",
            options = BuildOptions.None
        });
        File.WriteAllText("Logs/praia-final-build-result.txt", report.summary.result + " errors=" + report.summary.totalErrors + " path=" + report.summary.outputPath);
    }
}
