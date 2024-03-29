using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
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
        public string CertificateName
        {
            get
            {
                if (!string.IsNullOrEmpty(AppleCertificateName))
                {
                    return AppleCertificateName;
                } 
                else 
                {
                    return iPhoneCertificateName;
                }
            }
        }

        public string iPhoneCertificateName;
        public string AppleCertificateName;

        public void Description()
        {
            Debug.Log(this.ToString());
        }

        override public string ToString()
        {
            StringBuilder builder = new StringBuilder();
            builder.Append($"Name: {Name} \n");
            builder.Append($"Development: {Development} \n");
            builder.Append($"BundleId: {BundleIdentifier} \n");
            builder.Append($"TeamIdentifier: {TeamIdentifier} \n");
            builder.Append($"CertificateName: {CertificateName} \n");
            builder.Append($"iPhoneCertificateName: {iPhoneCertificateName} \n");
            builder.Append($"AppleCertificateName: {AppleCertificateName} \n");
            builder.Append($"UDID: {UDID} \n");
            builder.Append($"ExpirationDate: {ExpirationDate} \n");
            builder.Append($"FilePath: {FilePath} \n");
            return builder.ToString();
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
            ProvisionFileInfo fileInfo = ParseFile(filePath);
            provisionFileInfos.Add(fileInfo);
        }

        List<ProvisionFileInfo> filterProvisionFileInfo = provisionFileInfos
            .Where(p => p.BundleIdentifier == bundleIdentifier)
            .Where(p => p.Development == isDevelopment)
            .OrderByDescending(p => p.ExpirationDateTimeStamp)
            .ToList();

        if (filterProvisionFileInfo.Count > 0)
        {
            return filterProvisionFileInfo[0];
        }
        return null;
#else
        return null;
#endif
    }

    public static ProvisionFileInfo ParseFile(string filePath)
    {
        ProvisionFileInfo fileInfo = new ProvisionFileInfo
        {
            FilePath = filePath
        };

        try
        {
            var provisioningFileContents = File.ReadAllText(filePath);

            string patternUUID = "<key>UUID<\\/key>[\n\t]*<string>((\\w*\\-?){5})";
            Match matchUDID = Regex.Match(provisioningFileContents, patternUUID, RegexOptions.Singleline);
            if (matchUDID.Success)
            {
                string udid = matchUDID.Groups[1].Value;
                fileInfo.UDID = udid;
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

            filePath = filePath.Replace(" ", "\\ ");

            string nameCmd = $"/usr/libexec/PlistBuddy -c 'Print :Name' /dev/stdin <<< $(security cms -D -i {filePath})";
            string name = RunCommand(nameCmd);
            if (!string.IsNullOrEmpty(name))
            {
                fileInfo.Name = name;
            }

            HandleSignInfo(filePath, 0, fileInfo);
            HandleSignInfo(filePath, 1, fileInfo);

            string apsEnvironmentCmd = $"/usr/libexec/PlistBuddy -c 'Print :Entitlements:aps-environment' /dev/stdin <<< $(security cms -D -i {filePath})";
            string apsEnvironment = RunCommand(apsEnvironmentCmd);
            if (!string.IsNullOrEmpty(apsEnvironment))
            {
                fileInfo.Development = apsEnvironment == "development";
            }
            else 
            {
                if (!string.IsNullOrEmpty(fileInfo.CertificateName))
                {
                    fileInfo.Development = fileInfo.CertificateName.StartsWith("iPhone Developer");
                } else if (!string.IsNullOrEmpty(fileInfo.AppleCertificateName))
                {
                    fileInfo.Development = fileInfo.CertificateName.StartsWith("Apple Development");
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"{filePath} occur exception: {e}");
        }
        return fileInfo;
    }

    private static void HandleSignInfo(string filePath, int certificateIndex, ProvisionFileInfo fileInfo)
    {
        string signInfo = ExtractSignInfo(filePath, certificateIndex);
        if (!string.IsNullOrEmpty(signInfo))
        {
            if (signInfo.StartsWith("iPhone"))
            {
                fileInfo.iPhoneCertificateName = signInfo;
            }
            else if (signInfo.StartsWith("Apple"))
            {
                fileInfo.AppleCertificateName = signInfo;
            }
        }
    }

    private static string ExtractSignInfo(string filePath, int certificateIndex = 0)
    {
        // example data:
        // subject= /UID=2Z2VV22UHQ/CN=iPhone Developer: zeze huang (6YD3597U59)/OU=9XJ9HHK8NJ/O=KARMAGAME HK LIMITED/C=HK
        // subject= /UID=Q7YXYXBXMK/CN=Apple Development: bdNnew peak (5QJ63R923J)/OU=9XJ9HHK8NJ/O=KARMAGAME HK LIMITED/C=HK
        string cmd = $"/usr/libexec/PlistBuddy -c 'Print :DeveloperCertificates:{certificateIndex}' /dev/stdin <<< $(security cms -D -i {filePath}) | openssl x509 -inform DER -noout -subject";
        string signInfo = RunCommand(cmd);
        if (!string.IsNullOrEmpty(signInfo))
        {
            string pattern = @"CN=(.*?)/OU";
            var match = Regex.Match(signInfo, pattern);
            if (match.Success)
            {
                string result = match.Groups[1].Value;
                if (result.Contains("/x"))
                {
                    // 中文: 转成UTF8格式， 添加%, 使用URLDecode来解码
                    result = result.Replace("/x", "%");
                    return Uri.UnescapeDataString(result);
                }
                else
                {
                    return result;
                }
            }
        }
        return "";
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

        process.ErrorDataReceived += delegate (object sender, DataReceivedEventArgs args)
        {
            Debug.LogError(args.Data);
        };
        process.OutputDataReceived += delegate (object sender, DataReceivedEventArgs args)
        {
            Debug.LogError(args.Data);
        };
        process.Exited += delegate (object sender, System.EventArgs args)
        {
            Debug.LogError(args.ToString());
        };

        string line = process.StandardOutput.ReadToEnd();
        if (line != null)
        {
            return line.Replace("\\", "/").Replace("\n", "");
        }

        string error = process.StandardError.ReadToEnd();
        if (error != null)
        {
            Debug.LogError(error);
        }
        return null;
    }
}
