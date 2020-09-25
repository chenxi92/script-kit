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
    #region 参数

    #region Info.plist 文件设置
    private static readonly Dictionary<string, string> InfoPlistStringFields = new Dictionary<string, string>
    {
        { "GADApplicationIdentifier", "ca-app-pub-3940256099942544~1458002511" },
        {"FacebookAppID",             "西部SLG"},
        {"FacebookDisplayName",       "406381166977364"},

        // 隐私
        {"NSPhotoLibraryUsageDescription",      "APP need your approval to access PhotoLibrary"},
        {"NSPhotoLibraryAddUsageDescription",   "APP need your approval to access PhotoLibrary"},
        {"NSCalendarsUsageDescription",         "APP need your approval to access Calendars"},
        {"NSLocationWhenInUseUsageDescription", "Show featured content based on region"}
    };

    private static readonly Dictionary<string, bool> InfoPlistBoolFields = new Dictionary<string, bool>
    {
        { "UIStatusBarHidden", true },
        { "UIViewControllerBasedStatusBarAppearance", true }
    };

    private static readonly string[] WhiteListArray = {
        "fbapi",
        "fb-messenger-share-api",
        "fbauth2",
        "fbshareextension",
        "fb-messenger-share-api",
        "line",
        "lineauth",
        "twitterauth",
        "twitter"
    };

    private static readonly string[] URLSchemeArray = {
        "fb406381166977364",
        "twitterkit-Atm0nw12whLHIf8hW9JAxCDYn",
        "globalwestlink"
    };
    #endregion

    #region 打包参数
    private static readonly Dictionary<string, string> DebugSignInfo = new Dictionary<string, string>()
    {
        {"CODE_SIGN_IDENTITY", "iPhone Developer: 石学谦 石 (6Q4D6F9RGA)"},
        {"PROVISIONING_PROFILE", "927f0b6a-51e8-4537-9883-42deca474c91"},
        {"PROVISIONING_PROFILE_SPECIFIER", "west_dev_20200813"},
        {"DEVELOPMENT_TEAM", "YG56WXQ497"}
    };

    private static readonly Dictionary<string, string> ReleaseSignInfo = new Dictionary<string, string>()
    {
        {"CODE_SIGN_IDENTITY", "iPhone Distribution: G-MEI NETWORK TECHNOLOGY CO LIMITED (YG56WXQ497)"},
        {"PROVISIONING_PROFILE", "b397109f-83d8-4573-8033-e8c3721de84c"},
        {"PROVISIONING_PROFILE_SPECIFIER", "west_appstore_20200508"},
        {"DEVELOPMENT_TEAM", "YG56WXQ497"}
    };
    #endregion

    private static readonly string[] ValidResourceTypes = { ".plist", ".bundle" };
    private static readonly string   ProductName = "mythpuzzlerpgios";
    #endregion

    #region - Public
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

        CopyResources(project, pathToBuildProject);

        HandleInfoPlist(pathToBuildProject);

        AddCapabilities(project, pathToBuildProject);

        AddSign(project);

        AddBuildSettings(project);

        File.WriteAllText(projectPath, project.WriteToString());
        project.WriteToFile(projectPath);
    }
    #endregion

    #region Private
    private static void AddBuildSettings(PBXProject project)
    {
        string targetGuid = project.GetUnityMainTargetGuid();
        project.AddBuildProperty(targetGuid, "OTHER_LDFLAGS", "-ObjC");
        project.AddBuildProperty(targetGuid, "OTHER_LDFLAGS", "-lxml2");
        project.SetBuildProperty(targetGuid, "DEBUG_INFORMATION_FORMAT", "dwarf-with-dsym");

        project.SetBuildProperty(project.GetUnityFrameworkTargetGuid(), "ENABLE_BITCODE", "NO");
    }

    private static void AddSign(PBXProject project)
    {
        string targetGuid = project.GetUnityMainTargetGuid();
        foreach (string configName in project.BuildConfigNames())
        {
            string configGuid = project.BuildConfigByName(targetGuid, configName);
            if (configName.Contains("Release"))
            {
                foreach (KeyValuePair<string, string> pair in ReleaseSignInfo)
                {
                    project.SetBuildPropertyForConfig(configGuid, pair.Key, pair.Value);
                    project.SetBuildPropertyForConfig(configGuid, "CODE_SIGN_IDENTITY[sdk=iphoneos*]", "iPhone Distribution");
                }
            }
            else
            {
                foreach (KeyValuePair<string, string> pair in DebugSignInfo)
                {
                    project.SetBuildPropertyForConfig(configGuid, pair.Key, pair.Value);
                    project.SetBuildPropertyForConfig(configGuid, "CODE_SIGN_IDENTITY[sdk=iphoneos*]", "iPhone Developer");
                }
            }
        }
    }

    private static void AddCapabilities(PBXProject project, string pathToBuildProject)
    {
        string targetGuid = project.GetUnityMainTargetGuid();
        string relativeEntitlementsFilePath = ProductName + "." + "entitlements";

        ProjectCapabilityManager manager = new ProjectCapabilityManager(pathToBuildProject + "/Unity-iPhone.xcodeproj/project.pbxproj", relativeEntitlementsFilePath, "Unity-iPhone");

        // 1. game center
        manager.AddGameCenter();
        if (project.ContainsFramework(targetGuid, "GameKit.framework") == false)
        {
            Debug.Log("Add GameKit.framework");
            project.AddFrameworkToProject(targetGuid, "GameKit.framework", true);
        }

        // 2. purchase
        manager.AddInAppPurchase();
        if (project.ContainsFramework(targetGuid, "StoreKit.framework") == false)
        {
            Debug.Log("Add StoreKit.framework");
            project.AddFrameworkToProject(targetGuid, "StoreKit.framework", true);
        }

        // 3. remote push
        manager.AddPushNotifications(true);
        project.AddFile(relativeEntitlementsFilePath, relativeEntitlementsFilePath);
        project.AddBuildProperty(targetGuid, "CODE_SIGN_ENTITLEMENTS", relativeEntitlementsFilePath);

        // 4. background
        manager.AddBackgroundModes(BackgroundModesOptions.RemoteNotifications);

        // 4. domains
        //string[] domains = { "xxx"};
        //manager.AddAssociatedDomains(domains);

        manager.WriteToFile();
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

        foreach (KeyValuePair<string, bool> item in InfoPlistBoolFields)
        {
            if (rootDic.values.ContainsKey(item.Key) == false)
            {
                Debug.Log("Info.plist add key-value " + item.Key + " : " + item.Value);
                rootDic.SetBoolean(item.Key, item.Value);
            }
        }

        // URL Scheme
        PlistElementArray urlArray;
        if (!rootDic.values.ContainsKey("CFBundleURLTypes"))
        {
            urlArray = rootDic.CreateArray("CFBundleURLTypes");
        }
        else
        {
            urlArray = rootDic.values["CFBundleURLTypes"].AsArray();
        }
        foreach (string scheme in URLSchemeArray)
        {
            var urlTypeDict = urlArray.AddDict();
            urlTypeDict.SetString("CFBundleTypeRole", "Editor");
            urlTypeDict.CreateArray("CFBundleURLSchemes").AddString(scheme);
        }

        // White List
        PlistElementArray schemesArray;
        if (!rootDic.values.ContainsKey("LSApplicationQueriesSchemes"))
        {
            schemesArray = rootDic.CreateArray("LSApplicationQueriesSchemes");
        }
        else
        {
            schemesArray = rootDic.values["LSApplicationQueriesSchemes"].AsArray();
        }
        foreach (string whiteList in WhiteListArray)
        {
            schemesArray.AddString(whiteList);
        }

        File.WriteAllText(plistFilePath, plist.WriteToString());
    }

    private static void CopyResources(PBXProject project, string pathToBuildProject)
    {
        // create folder to place resource files.
        string sdkResourceName = "sdk_res";
        Directory.CreateDirectory(Path.Combine(pathToBuildProject, sdkResourceName));

        string targetGuid = project.GetUnityMainTargetGuid();
        string subFolderPath = "Plugins/IOS/res";
        string unityProjectSourcePath = "Assets/" + subFolderPath;
        foreach (string filePath in Directory.EnumerateFileSystemEntries(unityProjectSourcePath))
        {
            // 1. filter invalid file.
            if (IsValidCopyResourceType(filePath) == false)
            {
                continue;
            }
            
            string fileName = Path.GetFileName(filePath);

            // 2. delete file from UnityFramework Target.
            string deleteFilePath = Path.Combine("Frameworks", subFolderPath, fileName);
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

    private static bool IsValidCopyResourceType(string filePath)
    {
        bool isValidType = false;
        foreach (string t in ValidResourceTypes)
        {
            if (filePath.EndsWith(t))
            {
                isValidType = true;
                break;
            }
        }
        return isValidType;
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
    #endregion

    #region Deprecated
    private static void AddCapabilitiesOld(PBXProject project, string pathToBuildProject)
    {
        string targetGuid = project.GetUnityMainTargetGuid();

        string relativeEntitlementsFilePath = ProductName + "." + "entitlements";
        string absoluteEntitlementsFilePath = pathToBuildProject + "/" + relativeEntitlementsFilePath;

        bool saveEntitlementFile = false;
        PlistDocument entitlements = new PlistDocument();

        if (project.AddCapability(targetGuid, PBXCapabilityType.PushNotifications, relativeEntitlementsFilePath))
        {
            entitlements.root["aps-environment"] = new PlistElementString("development");
            saveEntitlementFile = true;
            Debug.Log("add push capabiliity success");
        }

        if (project.AddCapability(targetGuid, PBXCapabilityType.InAppPurchase, addOptionalFramework: true))
        {
            if (project.ContainsFramework(targetGuid, "StoreKit.framework") == false)
            {
                Debug.Log("Add StoreKit.framework");
                project.AddFrameworkToProject(targetGuid, "StoreKit.framework", true);
            }
            Debug.Log("add purchase capability success");
        }

        if (project.AddCapability(targetGuid, PBXCapabilityType.GameCenter, addOptionalFramework: true))
        {
            if (project.ContainsFramework(targetGuid, "GameKit.framework") == false)
            {
                Debug.Log("Add GameKit.framework");
                project.AddFrameworkToProject(targetGuid, "GameKit.framework", true);
            }
            AddCapabilitiesFieldToInfoPlist(pathToBuildProject, "UIRequiredDeviceCapabilities", "gamekit", new PlistElementArray());
            Debug.Log("add game center capability success");
        }

        if (saveEntitlementFile)
        {
            entitlements.WriteToFile(absoluteEntitlementsFilePath);
        }
    }

    private static void AddCapabilitiesFieldToInfoPlist(string pathToBuildProject, string key, string value, PlistElement type)
    {
        string plistFilePath = Path.Combine(pathToBuildProject, "Info.plist");
        if (File.Exists(plistFilePath) == false)
        {
            return;
        }
        if (key.Length < 1 || value.Length < 1)
        {
            return;
        }

        PlistDocument plist = new PlistDocument();
        plist.ReadFromFile(plistFilePath);
        PlistElementDict rootDic = plist.root;

        if (rootDic.values.ContainsKey(key) == false)
        {
            if (type is PlistElementString)
            {
                rootDic.SetString(key, value);
            }
            else if (type is PlistElementArray)
            {
                rootDic.CreateArray(key).AddString(value);
            }
        }
        else
        {
            if (type is PlistElementString)
            {
                rootDic.SetString(key, value);
            }
            else if (type is PlistElementArray)
            {
                PlistElementArray array = rootDic.values[key].AsArray();
                foreach (PlistElement item in array.values)
                {
                    if (item.AsString() == value)
                    {
                        Debug.LogWarningFormat("alread exist {0}:{1} in Info.plist", key, value);
                        return;
                    }
                }
                array.AddString(value);
            }
        }
        File.WriteAllText(plistFilePath, plist.WriteToString());
    }
    #endregion
}
