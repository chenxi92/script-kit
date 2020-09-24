using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor.Build.Reporting;
using UnityEditor.Callbacks;
using UnityEditor;
using UnityEditor.iOS.Xcode;
using System.IO;


public static class BuildIOS
{
    private static Dictionary<string, string> InfoPlistStringFields = new Dictionary<string, string>
    {
        { "GADApplicationIdentifier", "ca-app-pub-3940256099942544~1458002511" }
    };

    public static void BuildForiOS()
    {
        string projDir = GetCommandLineArgument("-xcodeProject");
        if (projDir == null)
        {
            Debug.LogError("xcode build path can't be null.");
            return;
        }

        BuildOptions option = BuildOptions.None;
        BuildReport report = BuildPipeline.BuildPlayer(GetLevelsFromBuildSettings(), projDir, BuildTarget.iOS, option);
        BuildSummary summary = report.summary;
        if (summary.result == BuildResult.Succeeded)
        {
            Debug.Log("Build success, cost " + summary.totalTime.Seconds + " seconds.");
        }
        else if (summary.result == BuildResult.Failed)
        {
            Debug.LogError("Build fail");
            return;
        }
    }

    [PostProcessBuild(2000)]
    public static void OnPostProcessBuild(BuildTarget target, string pathToBuildProject)
    {
        if (target != BuildTarget.iOS)
        {
            return;
        }

        string projectPath = PBXProject.GetPBXProjectPath(pathToBuildProject);
        PBXProject project = new PBXProject();
        project.ReadFromFile(projectPath);

        HandleInfoPlist(pathToBuildProject);

        CopyResources(project, pathToBuildProject);

        File.WriteAllText(projectPath, project.WriteToString());
        project.WriteToFile(projectPath);
    }

    private static void HandleInfoPlist(string pathToBuildProject)
    {
        string plistFilePath = Path.Combine(pathToBuildProject, "Info.plist");
        PlistDocument plist = new PlistDocument();
        plist.ReadFromFile(plistFilePath);
        PlistElementDict rootDic = plist.root;

        foreach (KeyValuePair<string, string> item in InfoPlistStringFields)
        {
            if (rootDic.values.ContainsKey(item.Key) == false)
            {
                Debug.Log("Info.plist add key-value " + item.Key + " : " + item.Value);
                rootDic.SetString(item.Key, item.Value);
            }
        }

        File.WriteAllText(plistFilePath, plist.WriteToString());
    }

    private static void CopyResources(PBXProject project, string pathToBuildProject)
    {
        // create folder to place resource files.
        string sdkResourceName = "sdk_res";
        Directory.CreateDirectory(Path.Combine(pathToBuildProject, sdkResourceName));

        string targetGuid = project.GetUnityMainTargetGuid();
        string projectRelativePath = "Plugins/IOS/res";
        string source = "Assets/" + projectRelativePath;
        foreach (string filePath in Directory.EnumerateFileSystemEntries(source))
        {
            // 1. filter invalid file.
            bool validFileType = filePath.EndsWith(".plist") || filePath.EndsWith(".bundle");
            if (!validFileType)
            {
                continue;
            }
            string fileName = Path.GetFileName(filePath);

            // 2. delete file from UnityFramework Target.
            string deleteFilePath = Path.Combine("Frameworks", projectRelativePath, fileName);
            string deleteFileGuid = project.FindFileGuidByRealPath(deleteFilePath);
            if (deleteFileGuid != null)
            {
                Debug.Log("delete file from UnityFramework target: " + deleteFilePath);
                project.RemoveFileFromBuild(project.GetUnityFrameworkTargetGuid(), deleteFileGuid);
            }

            // 3. copy file from Unity project to Xcode project.
            string destPath = Path.Combine(pathToBuildProject, sdkResourceName, fileName);
            FileUtil.CopyFileOrDirectory(filePath, destPath);

            // 4. add file to Unity-iPhone target.

            // path: The physical path to the file on the filesystem.
            // projectPath: The project path to the file as viewed in Xcode.
            string fileGuid = project.AddFile(sdkResourceName + "/" + fileName, sdkResourceName + "/" + fileName, PBXSourceTree.Source);
            project.AddFileToBuild(targetGuid, fileGuid);

            Debug.Log("add file to Unity-iPhone target: " + filePath);
        }
    }

    private static string GetCommandLineArgument(string prefix)
    {
        foreach (string arg in System.Environment.GetCommandLineArgs())
        {
            if (arg.StartsWith(prefix))
            {
                return arg.Substring(prefix.Length);
            }
        }
        return null;
    }

    private static string[] GetLevelsFromBuildSettings()
    {
        List<string> levels = new List<string>();
        for (int i = 0; i < EditorBuildSettings.scenes.Length; ++i)
        {
            if (EditorBuildSettings.scenes[i].enabled)
            {
                levels.Add(EditorBuildSettings.scenes[i].path);
            }
        }

        return levels.ToArray();
    }
}
