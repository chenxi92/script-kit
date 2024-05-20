#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// Build apk/aab files or export android gradle project.
/// </summary>
public class BuildAndroid
{
    private static readonly string TAG = "<BuildAndroid>";
    private const string RootMenuName = "Build/";

    private static string ApplicationIdentifier = "<com.xxx.abc>";
    private static string BundleVersion = "0.0.1";
    private static int BundleVersionCode = 1;
    private static string KeystoreName = Path.GetFullPath(Application.dataPath + "/../keystore/<path-to.keystore>");
    private static string KeystorePass = "xx";
    private static string KeyaliasName = "xx";
    private static string KeyaliasPass = "xx";


    private enum BuildType
    {
        AndrodGradleProject,
        APK,
        AAB
    }

    [MenuItem(RootMenuName + "Build Android APK")]
    public static void BuildAPK()
    {
        ParseCommandLines();
        ConfigPlayerSettings();
        ConfigEditorUserBuildSettings(BuildType.APK);

        string apkFilePath = GetOutputFilePath(".apk");
        DeleteIfExist(apkFilePath);

        BuildOptions options = BuildOptions.None;
        BuildReport report = BuildPipeline.BuildPlayer(GetScenePaths, apkFilePath, BuildTarget.Android, options);
        ProcessBuildReport(report);
    }

    [MenuItem(RootMenuName + "Build Android AAB")]
    public static void BuildAAB()
    {
        ParseCommandLines();
        ConfigPlayerSettings();
        ConfigEditorUserBuildSettings(BuildType.AAB);

        string aabFilePath = GetOutputFilePath(".aab");
        DeleteIfExist(aabFilePath);

        BuildOptions options = BuildOptions.None;
        BuildPlayerOptions buildPlayerOptions = new BuildPlayerOptions
        {
            scenes = GetScenePaths,
            locationPathName = aabFilePath,
            target = BuildTarget.Android,
            options = options
        };
        BuildReport report = BuildPipeline.BuildPlayer(buildPlayerOptions);
        ProcessBuildReport(report);
    }

    [MenuItem(RootMenuName + "Build Android Gradle Project")]
    public static void BuildAndroidGradleProject()
    {
        ParseCommandLines();
        ConfigPlayerSettings();
        ConfigEditorUserBuildSettings(BuildType.AndrodGradleProject);

        string projectDirectory = Path.GetFullPath(Application.dataPath + "/../Build/Android/ExportProject/<Example-Name>");
        DeleteIfExist(projectDirectory, false);
        BuildOptions options = BuildOptions.None;
        BuildReport report = BuildPipeline.BuildPlayer(GetScenePaths, projectDirectory, BuildTarget.Android, options);
        ProcessBuildReport(report);
    }

    private static void ProcessBuildReport(BuildReport report)
    {
        if (report.summary.result == BuildResult.Succeeded)
        {
            Debug.Log($"{TAG} build success! cost {report.summary.totalTime.Seconds} seconds, output path: {report.summary.outputPath}");
            string outputPath = report.summary.outputPath;
            EditorUtility.RevealInFinder(Path.GetDirectoryName(outputPath));
        }
        else
        {
            Debug.LogError($"{TAG} build failed: {report.summary}");
        }
    }

    private static string[] GetScenePaths
    {
        get
        {
            return EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
        }
    }

    private static void DeleteIfExist(string path, bool isFile = true)
    {
        if (isFile)
        {
            if (File.Exists(path))
            {
                Debug.Log($"{TAG} delete exist file: {path}");
                File.Delete(path);
            }
        }
        else
        {
            if (Directory.Exists(path))
            {
                Debug.LogWarning($"{TAG} delete existed directory: {path}");
                Directory.Delete(path, true);
            }
        }
    }

    private static string GetOutputFilePath(string suffix)
    {
        if (suffix.StartsWith("."))
        {
            suffix = suffix.Substring(1);
        }
        // string formattedVersion = $"{bundleVersion}({bundleVersionCode})";
        // string timestamp = System.DateTime.Now.ToString("yyyy_MM_dd_HH_mm_ss");
        string fileName = $"<Name-Of-Game>.{suffix}";
        return Path.GetFullPath(Application.dataPath + $"/../Build/Android/" + fileName);
    }

    private static void ConfigEditorUserBuildSettings(BuildType buildType)
    {
        SwitchPlatform(BuildTarget.Android);

        switch (buildType)
        {
            case BuildType.AndrodGradleProject:
                EditorUserBuildSettings.exportAsGoogleAndroidProject = true;
                EditorUserBuildSettings.buildAppBundle = false;
                break;
            case BuildType.APK:
                EditorUserBuildSettings.exportAsGoogleAndroidProject = false;
                EditorUserBuildSettings.buildAppBundle = false;
                break;
            case BuildType.AAB:
                EditorUserBuildSettings.exportAsGoogleAndroidProject = false;
                EditorUserBuildSettings.buildAppBundle = true;
                break;
        }
    }

    private static void SwitchPlatform(BuildTarget platform)
    {
        if (EditorUserBuildSettings.activeBuildTarget != platform)
        {
            Debug.LogWarning($"Switch platform: ${EditorUserBuildSettings.activeBuildTarget} -> ${platform}");
            BuildTargetGroup buildTargetGroup = BuildPipeline.GetBuildTargetGroup(platform);
            EditorUserBuildSettings.SwitchActiveBuildTarget(buildTargetGroup, platform);
        }
    }

    private static void ConfigPlayerSettings()
    {
        AddSymbol("<example-symbol>");

        PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android, ApplicationIdentifier);
        PlayerSettings.bundleVersion = BundleVersion;
        PlayerSettings.Android.bundleVersionCode = BundleVersionCode;

        if (File.Exists(KeystoreName) == false)
        {
            Debug.LogError($"{TAG} not exist keystore file: {KeystoreName}");
            return;
        }
        PlayerSettings.Android.useCustomKeystore = true;
        PlayerSettings.Android.keystoreName = KeystoreName;
        PlayerSettings.Android.keystorePass = KeystorePass;
        PlayerSettings.Android.keyaliasName = KeyaliasName;
        PlayerSettings.Android.keyaliasPass = KeyaliasPass;
    }


    private static void AddSymbol(string symbol)
    {
        var target = EditorUserBuildSettings.selectedBuildTargetGroup;
        string defines = PlayerSettings.GetScriptingDefineSymbolsForGroup(target);
        Debug.Log($"{TAG} current defines: {defines}");
        if (defines.Contains(symbol))
        {
            Debug.Log($"{TAG} already exist: {symbol}");
        }
        else
        {
            HashSet<string> newDefinesSet = new HashSet<string>(defines.Split(';')) {
                symbol
            };
            string newDefines = string.Join(";", newDefinesSet);
            Debug.Log($"{TAG} new defines: {newDefines}");
            PlayerSettings.SetScriptingDefineSymbolsForGroup(target, newDefines);
        }
    }

    private static void ParseCommandLines()
    {
        ParseAndAssign("-identifier", value => ApplicationIdentifier = value);
        ParseAndAssign("-bundleVersion", value => BundleVersion = value);
        ParseAndAssign("-bundleVersionCode", value =>
        {
            if (int.TryParse(value, out int intValue))
            {
                BundleVersionCode = intValue;
            }
        });

        ParseAndAssign("-keystoreName", value => KeystoreName = value);
        ParseAndAssign("-keystorePass", value => KeystorePass = value);
        ParseAndAssign("-keyaliasName", value => KeyaliasName = value);
        ParseAndAssign("-keyaliasPass", value => KeyaliasPass = value);
    }

    private static void ParseAndAssign(string argName, Action<string> assignAction)
    {
        string argValue = GetArg(argName);
        if (string.IsNullOrEmpty(argValue))
        {
            return;
        }
        Debug.Log($"{TAG} parse command argument: {argName} = {argValue}");
        assignAction(argValue);
    }

    private static string GetArg(string argName)
    {
        string[] args = System.Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == argName && args.Length > i + 1)
            {
                return args[i + 1];
            }
        }
        return null;
    }

}

#endif