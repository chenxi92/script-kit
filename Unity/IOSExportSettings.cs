using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.iOS.Xcode;
using UnityEditor.Callbacks;
using Path = System.IO.Path;
using System.Linq;
using System.Text.RegularExpressions;
using System.Diagnostics;

public class IOSExportSettings
{
    private static readonly string BundleIdentifier = "com.xx.xxx";

    #region Certificate Settings
    private static Dictionary<string, string> DeveloperSignInfo = new Dictionary<string, string>()
    {
        {"CODE_SIGN_IDENTITY", "iPhone Developer: xxx"},
        {"PROVISIONING_PROFILE", "xxxx-xxxx-xxxx-xxxx-xxxxxxx"},
        {"PROVISIONING_PROFILE_SPECIFIER", "xxxxx"},
        {"DEVELOPMENT_TEAM", "xxxx"}
    };
    private static Dictionary<string, string> DistributionSignInfo = new Dictionary<string, string>()
    {
        {"CODE_SIGN_IDENTITY", "iPhone Distribution: xxxx"},
        {"PROVISIONING_PROFILE", "xxxx-xxxx-xxxx-xxxx-xxxxxxx"},
        {"PROVISIONING_PROFILE_SPECIFIER", "xxxxx"},
        {"DEVELOPMENT_TEAM", "xxxx"}
    };
    #endregion

    #region Info.plist Settings
    private static readonly string[] WhiteListArray =
    {
        "fbapi",
        "fb-messenger-share-api",
        "fbauth2",
        "fbshareextension"
    };
    private static readonly Dictionary<string, string> InfoPlistStringSettings = new Dictionary<string, string>
    {
        { "FacebookAppID", "xxxx" },
        { "FacebookDisplayName", "xxx"},
        { "AppLovinSdkKey", "xxxx"},
        { "GADApplicationIdentifier", "ca-app-pub-xxxx" }
    };
    private static readonly Dictionary<string, bool> InfoPlistBoolSettings = new Dictionary<string, bool>
    {
    };
    private static readonly string[] URLSchemeArray =
    {
        "fbxxx"
    };
    private static readonly Dictionary<string, string> PrivacySettings = new Dictionary<string, string>
    {
        {"NSUserTrackingUsageDescription", "This identifier will be used to deliver personalized ads to you."}
    };
    private static readonly string[] SKAItems =
    {

    };
    #endregion

    #region SDK Opt-in Feature Settings

    // 待开启权限
    private static readonly PBXCapabilityType[] CapabilityArray =
    {
        PBXCapabilityType.GameCenter,
        PBXCapabilityType.BackgroundModes,
        PBXCapabilityType.PushNotifications,
        PBXCapabilityType.InAppPurchase,
    };
    // Optional Frameworks
    private static readonly string[] WeakSystemFrameworks =
    {
        "SystemConfiguration.framework",
        "Security.framework",
        "AppTrackingTransparency.framework"
    };
    // Required Frameworks -> add to UnityFramework Target
    private static readonly string[] StrongSystemFrameworks =
    {
        "Accelerate.framework"
    };
    #endregion

    private static readonly Dictionary<string, string> XcodeBuildSettings = new Dictionary<string, string>()
    {
        { "ENABLE_BITCODE", "NO"},
        { "ALWAYS_EMBED_SWIFT_STANDARD_LIBRARIES", "YES"},
        { "SWIFT_OBJC_BRIDGING_HEADER", "Libraries/Plugins/IOS/res/xxxx-Bridging-Header.h"},
        { "SWIFT_VERSION", "5.0"}
    };


    [PostProcessBuild(1)]
    public static void OnPostProcessBuild(BuildTarget buildTarget, string pathToBuiltProject)
    {
        // 只处理IOS工程， pathToBuildProject会传入导出的ios工程的根目录
        if (buildTarget != BuildTarget.iOS)
            return;

        InfoLog($"Starg Post Build: {pathToBuiltProject}");

        // 创建工程设置对象
        string projectPath = pathToBuiltProject + "/Unity-iPhone.xcodeproj/project.pbxproj";
        PBXProject project = new PBXProject();
        project.ReadFromFile(projectPath);

        HandleInfoPlistFile(pathToBuiltProject);
        CopyResources(project, pathToBuiltProject);

        AddSignInfo(project);
        HandleBuildSettings(project);
        AddSystemFramework(project);
        HandleBugly(project);

        HandleCapability(project, pathToBuiltProject);

        // 修改后的内容写回到配置文件
        project.WriteToFile(projectPath);
    }

    #region Logs
    private static void InfoLog(string msg, string tag = "")
    {
        if (tag != "")
        {
            tag = $"[{tag}] ";
        }
        UnityEngine.Debug.Log(String.Format("{0}{1}", tag, msg));
    }
    private static void WarningLog(string msg, string tag = "")
    {
        if (tag != "")
        {
            tag = $"[{tag}] ";
        }
        UnityEngine.Debug.LogWarning(String.Format("{0}{1}", tag, msg));
    }
    private static void ErrorLog(string msg, string tag = "")
    {
        if (tag != "")
        {
            tag = $"[{tag}] ";
        }
        UnityEngine.Debug.LogWarning(String.Format("{0}{1}", tag, msg));
    }
    #endregion

    #region 处理 Info.plist 文件
    private static void HandleInfoPlistFile(string pathToBuildProject)
    {
        string plistPath = pathToBuildProject + "/Info.plist";
        InfoLog($"start handle at: {plistPath}", "Info.plist");

        PlistDocument plist = new PlistDocument();
        plist.ReadFromString(File.ReadAllText(plistPath));

        PlistElementDict rootDict = plist.root;

        InfoPlistHandleURLSchemes(rootDict);
        InfoPlistHandleWhitList(rootDict);
        InfoPlistHandleSKAItems(rootDict);
        InfoPlistHandleStringSettings(rootDict);
        InfoPlistHandleBoolSettings(rootDict);

        // 允许使用 HTTP 请求
        if (rootDict.values.ContainsKey("NSAppTransportSecurity"))
        {
            rootDict.values.Remove("NSAppTransportSecurity");
        }
        PlistElementDict urlDict = rootDict.CreateDict("NSAppTransportSecurity");
        urlDict.SetBoolean("NSAllowsArbitraryLoads", true);

        // 保存 Info.plist 文件
        File.WriteAllText(plistPath, plist.WriteToString());

        InfoLog("finishe handle.", "Info.plist");
    }

    private static void InfoPlistHandleURLSchemes(PlistElementDict rootDict)
    {
        if (URLSchemeArray.Length == 0)
        {
            return;
        }

        PlistElementArray urlSchemeArray;
        if (!rootDict.values.ContainsKey("CFBundleURLTypes"))
        {
            urlSchemeArray = rootDict.CreateArray("CFBundleURLTypes");
        }
        else
        {
            urlSchemeArray = rootDict.values["CFBundleURLTypes"].AsArray();
        }
        foreach (string scheme in URLSchemeArray)
        {
            PlistElementDict urlTypeDict = urlSchemeArray.AddDict();
            urlTypeDict.SetString("CFBundleTypeRole", "Editor");

            PlistElementArray urlScheme = urlTypeDict.CreateArray("CFBundleURLSchemes");
            urlScheme.AddString(scheme);
            InfoLog($"set scheme: {scheme}", "Info.plist");
        }
    }

    private static void InfoPlistHandleWhitList(PlistElementDict rootDict)
    {
        if (WhiteListArray.Length == 0)
        {
            return;
        }
        PlistElementArray schemesArray;
        if (!rootDict.values.ContainsKey("LSApplicationQueriesSchemes"))
        {
            schemesArray = rootDict.CreateArray("LSApplicationQueriesSchemes");
        }
        else
        {
            schemesArray = rootDict.values["LSApplicationQueriesSchemes"].AsArray();
        }
        foreach (string whiteList in WhiteListArray)
        {
            schemesArray.AddString(whiteList);
            InfoLog($"set whiteList: {whiteList}", "Info.plist");
        }
    }

    private static void InfoPlistHandleSKAItems(PlistElementDict rootDict)
    {
        if (SKAItems.Length == 0)
        {
            return;
        }
        PlistElementArray skaItemArray;
        if (!rootDict.values.ContainsKey("SKAdNetworkItems"))
        {
            skaItemArray = rootDict.CreateArray("SKAdNetworkItems");
        }
        else
        {
            skaItemArray = rootDict.values["SKAdNetworkItems"].AsArray();
        }
        foreach (string item in SKAItems)
        {
            PlistElementDict dic = skaItemArray.AddDict();
            dic.SetString("SKAdNetworkIdentifier", item);
            InfoLog($"set SKItem: {item}", "Info.plist");
        }
    }

    private static void InfoPlistHandleStringSettings(PlistElementDict rootDict)
    {
        foreach (KeyValuePair<string, string> kvp in InfoPlistStringSettings)
        {
            if (rootDict.values.ContainsKey(kvp.Key) == false)
            {
                rootDict.SetString(kvp.Key, kvp.Value);
                InfoLog($"set {kvp.Key} = {kvp.Value}", "Info.plist");
            }
        }

        foreach (KeyValuePair<string, string> kvp in PrivacySettings)
        {
            if (rootDict.values.ContainsKey(kvp.Key) == false)
            {
                rootDict.SetString(kvp.Key, kvp.Value);
                InfoLog($"set string {kvp.Key} = {kvp.Value}", "Info.plist");
            }
        }
    }

    private static void InfoPlistHandleBoolSettings(PlistElementDict rootDict)
    {
        foreach (KeyValuePair<string, bool> kvp in InfoPlistBoolSettings)
        {
            if (rootDict.values.ContainsKey(kvp.Key) == false)
            {
                rootDict.SetBoolean(kvp.Key, kvp.Value);
                InfoLog($"set bool {kvp.Key} = {kvp.Value}", "Info.plist");
            }
        }
    }
    #endregion

    #region Copy SDK Resource to Xcode project
    private static void CopyResources(PBXProject project, string projectDir, string subFolderPath = "Plugins/IOS/res")
    {
        // create folder to place resource files.
        string sdkResourceName = "SDKResources";
        string folderPath = Path.Combine(projectDir, sdkResourceName);
        InfoLog($"create folder = {folderPath}", "Copy Resource");
        Directory.CreateDirectory(folderPath);

        string targetGuid = project.GetUnityMainTargetGuid();
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
            //if (deleteFileGuid == null)
            //{
            //    // If not find the file in Frameworks/ folder, try to find in the Libraries folder
            //    // Fix bug: the empty Dumy.swift was in Libraries folder
            //    InfoLog($"try to find file at: {deleteFilePath}", "Copy Resource");
            //    deleteFilePath = Path.Combine("Libraries", subFolderPath, fileName);
            //    deleteFileGuid = project.FindFileGuidByRealPath(deleteFilePath);
            //}
            if (deleteFileGuid != null)
            {
                InfoLog($"delete file from UnityFramework target: {deleteFilePath}", "Copy Resource");
                project.RemoveFileFromBuild(project.GetUnityFrameworkTargetGuid(), deleteFileGuid);
            }
            else
            {
                WarningLog($"delete file guid was empty at: {deleteFilePath}", "Copy Resource");
            }

            // 3. copy file from Unity project to Xcode project.
            string destPath = Path.Combine(projectDir, sdkResourceName, fileName);
            FileUtil.CopyFileOrDirectory(filePath, destPath);

            // 4. add file to Unity-iPhone target.

            // path: The physical path to the file on the filesystem.
            // projectPath: The project path to the file as viewed in Xcode.
            string fileGuid = project.AddFile(sdkResourceName + "/" + fileName, sdkResourceName + "/" + fileName, PBXSourceTree.Source);
            project.AddFileToBuild(targetGuid, fileGuid);

            InfoLog($"add file to Unity-iPhone target: {destPath}", "Copy Resource");
        }
    }

    private static readonly string[] ValidResourceTypes = { ".plist", ".bundle" };
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
    #endregion

    #region Certificate
    private static void DetectSignInfo()
    {
        ProvisionFileManager.ProvisionFileInfo devFileInfo = ProvisionFileManager.FindLastestProvisionFile(BundleIdentifier);
        DeveloperSignInfo["CODE_SIGN_IDENTITY"] = devFileInfo.CertificateName;
        DeveloperSignInfo["PROVISIONING_PROFILE"] = devFileInfo.UDID;
        DeveloperSignInfo["PROVISIONING_PROFILE_SPECIFIER"] = devFileInfo.Name;
        DeveloperSignInfo["DEVELOPMENT_TEAM"] = devFileInfo.TeamIdentifier;
        devFileInfo.Description();

        ProvisionFileManager.ProvisionFileInfo disFileInfo = ProvisionFileManager.FindLastestProvisionFile(BundleIdentifier, false);
        DistributionSignInfo["CODE_SIGN_IDENTITY"] = disFileInfo.CertificateName;
        DistributionSignInfo["PROVISIONING_PROFILE"] = disFileInfo.UDID;
        DistributionSignInfo["PROVISIONING_PROFILE_SPECIFIER"] = disFileInfo.Name;
        DistributionSignInfo["DEVELOPMENT_TEAM"] = disFileInfo.TeamIdentifier;
        disFileInfo.Description();
    }

    private static void AddSignInfo(PBXProject project)
    {
        DetectSignInfo();

        string targetGuid = project.GetUnityMainTargetGuid();

        project.SetBuildProperty(targetGuid, "CODE_SIGN_STYLE", "Manual");
        project.SetTeamId(targetGuid, DistributionSignInfo["DEVELOPMENT_TEAM"]);

        foreach (string configName in project.BuildConfigNames())
        {
            // configName
            // Release, ReleaseForProfiling, ReleaseForRunning, Debug
            string configGuid = project.BuildConfigByName(targetGuid, configName);
            if (configName != "Release")
            {
                foreach (KeyValuePair<string, string> pair in DeveloperSignInfo)
                {
                    InfoLog($"set {configName} with {pair.Key} = {pair.Value}", "SignInfo");
                    project.SetBuildPropertyForConfig(configGuid, pair.Key, pair.Value);
                    project.SetBuildPropertyForConfig(configGuid, "CODE_SIGN_IDENTITY[sdk=iphoneos*]", "iPhone Developer");
                }
            }
            else
            {
                foreach (KeyValuePair<string, string> pair in DistributionSignInfo)
                {
                    InfoLog($"set {configName} with {pair.Key} = {pair.Value}", "SignInfo");
                    project.SetBuildPropertyForConfig(configGuid, pair.Key, pair.Value);
                    project.SetBuildPropertyForConfig(configGuid, "CODE_SIGN_IDENTITY[sdk=iphoneos*]", "iPhone Distribution");
                }
            }
        }
    }
    #endregion

    #region Xcode Project Settings
    private static void HandleBuildSettings(PBXProject project)
    {
        string unityFrameworkGuid = project.GetUnityFrameworkTargetGuid();
        project.SetBuildProperty(unityFrameworkGuid, "ENABLE_BITCODE", "NO");
        project.AddBuildProperty(unityFrameworkGuid, "OTHER_LDFLAGS", "-ObjC");

        string targetGuid = project.GetUnityMainTargetGuid();
        project.AddBuildProperty(targetGuid, "OTHER_LDFLAGS", "-ObjC");

        foreach (KeyValuePair<string, string> pair in XcodeBuildSettings)
        {
            InfoLog($"set {pair.Key} = {pair.Value}", "Build Setting");
            project.SetBuildProperty(targetGuid, pair.Key, pair.Value);
        }
        project.SetBuildProperty(targetGuid, "PRODUCT_BUNDLE_IDENTIFIER", BundleIdentifier);
    }

    private static void AddSystemFramework(PBXProject project)
    {
        foreach (var framework in WeakSystemFrameworks)
        {
            AddSystemFramework(project, framework, true, false);
        }
        foreach (var framework in StrongSystemFrameworks)
        {
            AddSystemFramework(project, framework, false, true);
        }
    }

    private static void AddSystemFramework(PBXProject project, string framework, bool weak, bool addToUnityFrameworkTarget = false)
    {
        string targetGuid = project.GetUnityMainTargetGuid();
        if (addToUnityFrameworkTarget) {
            targetGuid = project.GetUnityFrameworkTargetGuid();
        }
        bool success = project.ContainsFramework(targetGuid, framework);
        if (success == false)
        {
            InfoLog($"add {framework}", "System Framework");
            project.AddFrameworkToProject(targetGuid, framework, weak);
        }
        else
        {
            InfoLog($"already exist {framework}", "System Framework");
        }
    }
    #endregion

    private static void HandleBugly(PBXProject project)
    {
        string targetGuid = project.GetUnityMainTargetGuid();

        project.AddBuildProperty(targetGuid, "FRAMEWORK_SEARCH_PATHS", "Frameworks/Plugins/IOS/Bugly");
        project.AddBuildProperty(targetGuid, "LIBRARY_SEARCH_PATHS", "Libraries/Plugins/IOS/Bugly/BuglyBridge");

        project.AddFileToBuild(targetGuid, project.AddFile("usr/lib/libsqlite3.0.tbd", "Frameworks/libsqlite3.0.tbd", PBXSourceTree.Sdk));
        project.AddFileToBuild(targetGuid, project.AddFile("usr/lib/libz.tbd", "Frameworks/libz.tbd", PBXSourceTree.Sdk));
        project.AddFileToBuild(targetGuid, project.AddFile("usr/lib/libc++.tbd", "Frameworks/libc++.tbd", PBXSourceTree.Sdk));
    }

    #region Capability

#if UNITY_2019_3_OR_NEWER

    private static void HandleCapability(PBXProject project, string pathToBuildProject)
    {
        string pbxProjectPath = pathToBuildProject + "/Unity-iPhone.xcodeproj/project.pbxproj";
        string targetName = "Unity-iPhone";
        string entitlementFilePath = "Unity-iPhone.entitlements";
        string targetGuid = project.GetUnityMainTargetGuid();
        ProjectCapabilityManager manager = new ProjectCapabilityManager(pbxProjectPath, entitlementFilePath, targetName, targetGuid);
        foreach (PBXCapabilityType type in CapabilityArray)
        {
            if (type == PBXCapabilityType.GameCenter)
            {
                manager.AddGameCenter();
            }
            else if (type == PBXCapabilityType.InAppPurchase)
            {
                manager.AddInAppPurchase();
            }
            else if (type == PBXCapabilityType.BackgroundModes)
            {
                manager.AddBackgroundModes(BackgroundModesOptions.RemoteNotifications);
            }
            else if (type == PBXCapabilityType.PushNotifications)
            {
                manager.AddPushNotifications(true);
            }
            else if (type == PBXCapabilityType.SignInWithApple)
            {
                manager.AddSignInWithApple();
            }
            else
            {
                WarningLog("unrecoginzed type.", "Capability");
            }
        }
        manager.WriteToFile();

        // Add Unity-iPhone.entitlements to the Xcode project
        // For Push capability need a file attach to the Xcode project
        project.AddFile(entitlementFilePath, entitlementFilePath);
        project.AddBuildProperty(targetGuid, "CODE_SIGN_ENTITLEMENTS", entitlementFilePath);
    }
    
#else

    private static void HandleCapability(PBXProject project, string pathToBuildProject)
    {
        string targetGuid = project.GetUnityMainTargetGuid();
        foreach (PBXCapabilityType type in CapabilityArray)
        {
            bool success;
            if (type == PBXCapabilityType.GameCenter)
            {
                success = project.AddCapability(targetGuid, type);
                if (success == false)
                {
                    ErrorLog("add GameCenter fail.", "Capability");
                }
                else
                {
                    AddValueOnEnableCapability(pathToBuildProject, "UIRequiredDeviceCapabilities", "gamekit");
                }
            }
            else if (type == PBXCapabilityType.InAppPurchase)
            {
                success = project.AddCapability(targetGuid, type);
                if (success == false)
                {
                    ErrorLog("add InAppPurchase fail.", "Capability");
                }
                else
                {
                    AddSystemFramework(project, "StoreKit.framework", false);
                }
            }
            else if (type == PBXCapabilityType.BackgroundModes)
            {
                success = project.AddCapability(targetGuid, type);
                if (success == false)
                {
                    ErrorLog("add BackgroundModes fail.", "Capability");
                }
                else
                {
                    AddValueOnEnableCapability(pathToBuildProject, "UIBackgroundModes", "remote-notification");
                }
            }
            else if (type == PBXCapabilityType.PushNotifications)
            {
                AddPushCapability(project, pathToBuildProject);
            }
            else
            {
                WarningLog("unrecoginzed type.", "Capability");
            }
        }
    }

    // Capabilities 开启的时候，会修改Info.plist 文件内容
    private static void AddValueOnEnableCapability(string pathToBuildProject, string key, string value)
    {
        // 远程推送, GameCenter 等, 需要修改 Info.Plist 文件
        string plistPath = pathToBuildProject + "/Info.plist";
        PlistDocument plist = new PlistDocument();
        plist.ReadFromString(File.ReadAllText(plistPath));

        PlistElementDict rootDict = plist.root;
        PlistElementArray modifyArray;
        if (!rootDict.values.ContainsKey(key))
        {
            modifyArray = rootDict.CreateArray(key);
        }
        else
        {
            modifyArray = rootDict.values[key].AsArray();
            foreach (PlistElement element in modifyArray.values)
            {
                if (element.AsString() == value)
                {
                    InfoLog($"Info.plist alread exist {key} = {value}", "Capability");
                    return;
                }
            }
        }

        modifyArray.AddString(value);

        File.WriteAllText(plistPath, plist.WriteToString());
    }

    // https://answers.unity.com/questions/1224123/enable-push-notification-in-xcode-project-by-defau.html?childToView=1364606
    // 添加推送功能
    private static bool AddPushCapability(PBXProject project, string pathToBuildProject)
    {
        string targetGuid = project.GetUnityMainTargetGuid();
        string targetName = "Unity-iPhone";
        var fileName = targetName + ".entitlements";
        var entitleFilePath = pathToBuildProject + "/" + targetName + "/" + fileName;
        if (File.Exists(entitleFilePath) == true)
        {
            ErrorLog("entitlements file exist.", "Capability");
            return true;
        }

        bool success = project.AddCapability(targetGuid, PBXCapabilityType.PushNotifications);
        if (success == false)
        {
            ErrorLog("open push cabability fail.", "Capability");
            return false;
        }

        string entitlements =
@"<?xml version=""1.0"" encoding=""UTF-8""?>
<!DOCTYPE plist PUBLIC ""-//Apple//DTD PLIST 1.0//EN"" ""http://www.apple.com/DTDs/PropertyList-1.0.dtd"">
<plist version=""1.0"">
    <dict>
        <key>aps-environment</key>
        <string>development</string>
    </dict>
</plist>";
        bool UseSignWithApple = CapabilityArray.Contains(PBXCapabilityType.SignInWithApple);
        if (UseSignWithApple)
        {
            entitlements =
@"<?xml version=""1.0"" encoding=""UTF-8""?>
<!DOCTYPE plist PUBLIC ""-//Apple//DTD PLIST 1.0//EN"" ""http://www.apple.com/DTDs/PropertyList-1.0.dtd"">
<plist version=""1.0"">
    <dict>
    <key>aps-environment</key>
    <string>development</string>
    <key>com.apple.developer.applesignin</key>
    <array>
        <string>Default</string>
    </array>
    </dict>
</plist>";
        }

        try
        {
            File.WriteAllText(entitleFilePath, entitlements);
            project.AddFile(targetName + "/" + fileName, fileName);
            project.AddBuildProperty(targetGuid, "CODE_SIGN_ENTITLEMENTS", targetName + "/" + fileName);
        }
        catch (IOException e)
        {
            ErrorLog($"Could not copy entitlements. Probably already exists. exception = {e}", "Capability");
        }

        InfoLog("add push capability success.", "Capability");
        return true;
    }
#endif

    #endregion

    /**
    解析 mobileprovision 文件内容

    Inspired by:
    https://github.com/Unity-Technologies/UnityCsReference/blob/master/Editor/Mono/PlatformSupport/ProvisioningProfile.cs
    https://github.com/wlgys8/UnityShellHelper
    http://maniak-dobrii.com/extracting-stuff-from-provisioning-profile/

    Author: Peak
     */
    public static class ProvisionFileManager
    {
        /// <summary>
        /// iOS 描述文件信息
        /// </summary>
        public class ProvisionFileInfo
        {
            /// <summary>
            /// 描述文件路径
            /// </summary>
            public string FilePath;

            /// <summary>
            /// 描述文件UDID
            /// </summary>
            public string UDID;

            /// <summary>
            /// 描述文件名称
            /// </summary>
            public string Name;

            /// <summary>
            /// 描述文件过期时间
            /// </summary>
            public string ExpirationDate;

            /// <summary>
            /// 描述文件过期时间戳
            /// </summary>
            public double ExpirationDateTimeStamp;

            /// <summary>
            /// 描述文件内TeamID
            /// </summary>
            public string TeamIdentifier;

            /// <summary>
            /// 描述文件内包名信息
            /// </summary>
            public string BundleIdentifier;

            /// <summary>
            /// 是否是开发证书的描述文件
            /// </summary>
            public bool Development;

            /// <summary>
            /// 证书名称
            /// </summary>
            public string CertificateName;

            public void Description()
            {
                string description = string.Format("\n=============={0}==============\n" +
                    "BundleIdentifier: {1}\nUDID: {2}\nTeamIdentifier: {3}\nCertificateName: {4}\nFilePath: {5}\n",
                    Name, BundleIdentifier, UDID, TeamIdentifier, CertificateName, FilePath);
                UnityEngine.Debug.Log(description);
            }
        }

        /// <summary>
        /// 获取最新证书信息
        /// </summary>
        /// <param name="bundleIdentifier">包名</param>
        /// <param name="isDevelopment">是否是开发证书，默认是开发证书</param>
        /// <returns></returns>
        public static ProvisionFileInfo FindLastestProvisionFile(string bundleIdentifier, bool isDevelopment = true)
        {
#if UNITY_EDITOR_OSX
            List<string> profilePaths = LoadLocalProfiles();
            List<ProvisionFileInfo> provisionFileInfos = new List<ProvisionFileInfo>();
            foreach (var filePath in profilePaths)
            {
                provisionFileInfos.Add(ParseFile(filePath));
            }

            List<ProvisionFileInfo> filterProvisionFileInfo = provisionFileInfos
                .Where(p => p.BundleIdentifier == bundleIdentifier).ToList()
                .Where(p => p.Development == isDevelopment).ToList()
                .OrderByDescending(p => p.ExpirationDateTimeStamp).ToList();

            if (filterProvisionFileInfo.Count > 0)
            {
                return filterProvisionFileInfo[0];
            }
            return null;
#else
            return null;
#endif
        }

        private static ProvisionFileInfo ParseFile(string filePath)
        {
            ProvisionFileInfo fileInfo = new ProvisionFileInfo
            {
                FilePath = filePath
            };

            var provisioningFileContents = File.ReadAllText(filePath);

            string patternUUID = "<key>UUID<\\/key>[\n\t]*<string>((\\w*\\-?){5})";
            Match matchUDID = Regex.Match(provisioningFileContents, patternUUID, RegexOptions.Singleline);
            if (matchUDID.Success)
            {
                string udid = matchUDID.Groups[1].Value;
                fileInfo.UDID = udid;
            }

            string patternName = "<key>Name<\\/key>[\n\t]*<string>((\\w*\\-?){5})";
            Match matchName = Regex.Match(provisioningFileContents, patternName, RegexOptions.Singleline);
            if (matchName.Success)
            {
                string name = matchName.Groups[1].Value;
                fileInfo.Name = name;
            }

            string patternTeamIdentifier = "<key>TeamIdentifier<\\/key>[\n\t]*<array>[\n\t]*<string>([\\w\\/+=]+)<\\/string>";
            Match matchTeamIdentifier = Regex.Match(provisioningFileContents, patternTeamIdentifier, RegexOptions.Singleline);
            if (matchTeamIdentifier.Success)
            {
                string teamIdentifier = matchTeamIdentifier.Groups[1].Value;
                fileInfo.TeamIdentifier = teamIdentifier;
            }

            string patternExpirationDate = "<key>ExpirationDate<\\/key>[\n\t]*<date>(.*?)</date>";
            Match matchExpirationDate = Regex.Match(provisioningFileContents, patternExpirationDate, RegexOptions.Singleline);
            if (matchExpirationDate.Success)
            {
                string expirationDate = matchExpirationDate.Groups[1].Value;
                System.DateTime dateTime = System.DateTime.Parse(expirationDate);

                TimeSpan span = (dateTime - new DateTime(1970, 1, 1, 0, 0, 0, 0).ToLocalTime());
                fileInfo.ExpirationDateTimeStamp = (double)span.TotalSeconds;

                expirationDate = dateTime.ToString("yyyy-MM-dd HH:mm:ss");
                fileInfo.ExpirationDate = expirationDate;
            }

            string patternBundleIdentifier = "<key>application-identifier<\\/key>[\n\t]*<string>(.*?)</string>";
            Match matchBundeIdentifier = Regex.Match(provisioningFileContents, patternBundleIdentifier, RegexOptions.Singleline);
            if (matchBundeIdentifier.Success)
            {
                string bundleIdentifier = matchBundeIdentifier.Groups[1].Value;
                string bundleId = string.Join(".", bundleIdentifier.Split('.').Skip(1).ToArray());
                fileInfo.BundleIdentifier = bundleId;
            }

            string plistCmd = "/usr/libexec/PlistBuddy -c 'Print :DeveloperCertificates:0' /dev/stdin";
            string opensslCmd = "openssl x509 -inform DER -noout -subject";
            string sedCmd = "sed -n '/^subject/s/^.*CN=\\(.*\\)\\/OU=.*/\\1/p'";
            string cmd = string.Format("{0} <<< $(security cms -D -i {1}) | {2} | {3}",
               plistCmd, filePath.Replace(" ", "\\ "), opensslCmd, sedCmd);
            string signInfo = RunCommand(cmd);
            if (signInfo != null)
            {
                signInfo = signInfo.Replace("\n", "");
                if (signInfo.Contains("/x"))
                {
                    // 中文: 转成UTF8格式， 添加%, 使用URLDecode来解码
                    signInfo = signInfo.Replace("/x", "%");
                    signInfo = Uri.UnescapeDataString(signInfo);
                }
                fileInfo.CertificateName = signInfo;
                fileInfo.Development = signInfo.StartsWith("iPhone Developer:");
            }
            return fileInfo;
        }

        private static List<string> LoadLocalProfiles()
        {
            string[] searchPath = new string[] {
                "{Home}/Library/MobileDevice/Provisioning Profiles"
            };
            List<string> localProfiles = new List<string>();
            foreach (var path in searchPath)
            {
                var profilesFolder = path.Replace("{Home}", Environment.GetFolderPath(Environment.SpecialFolder.Personal));
                if (!Directory.Exists(profilesFolder))
                {
                    continue;
                }
                foreach (var file in Directory.GetFiles(profilesFolder))
                {
                    if (Path.GetExtension(file) == ".mobileprovision")
                    {
                        //Debug.Log("add file: " + file);
                        localProfiles.Add(file);
                    }
                }
            }
            return localProfiles;
        }

        private static string RunCommand(string command)
        {
            ProcessStartInfo startInfo = new ProcessStartInfo("bash")
            {
                WorkingDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Personal),
                CreateNoWindow = true,
                ErrorDialog = true,
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = System.Text.UTF8Encoding.UTF8,
                StandardErrorEncoding = System.Text.UTF8Encoding.UTF8,
                Arguments = "-c \"" + command + " \""
            };

            Process process = new Process
            {
                StartInfo = startInfo
            };
            process.Start();

            process.ErrorDataReceived += delegate (object sender, DataReceivedEventArgs args) {
                UnityEngine.Debug.LogError(args.Data);
            };
            process.OutputDataReceived += delegate (object sender, DataReceivedEventArgs args) {
                UnityEngine.Debug.LogError(args.Data);
            };
            process.Exited += delegate (object sender, System.EventArgs args) {
                UnityEngine.Debug.LogError(args.ToString());
            };

            string line = process.StandardOutput.ReadToEnd();
            if (line != null)
            {
                return line.Replace("\\", "/");
            }

            string error = process.StandardError.ReadToEnd();
            if (error != null)
            {
                UnityEngine.Debug.LogError(error);
            }
            return null;
        }
    }
}

