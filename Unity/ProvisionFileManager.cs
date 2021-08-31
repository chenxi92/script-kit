using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

using Debug = UnityEngine.Debug;


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
            Debug.Log(description);
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
            Debug.LogError(args.Data);
        };
        process.OutputDataReceived += delegate (object sender, DataReceivedEventArgs args) {
            Debug.LogError(args.Data);
        };
        process.Exited += delegate (object sender, System.EventArgs args) {
            Debug.LogError(args.ToString());
        };

        string line = process.StandardOutput.ReadToEnd();
        if (line != null)
        {
            return line.Replace("\\", "/");
        }

        string error = process.StandardError.ReadToEnd();
        if (error != null)
        {
            Debug.LogError(error);
        }
        return null;
    }
}
