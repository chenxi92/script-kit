#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography.X509Certificates;
using System.Text.RegularExpressions;
using System.Xml;
using UnityEditor;
using UnityEngine;

public class MobileProvision
{
    public string Name { get; private set; }
    public string UUID { get; private set; }
    public string TeamIdentifier { get; private set; }
    public string ApplicationIdentifier { get; private set; }
    public string TeamName { get; private set; }
    public DateTime CreationDate { get; private set; }
    public DateTime ExpirationDate { get; private set; }
    public bool IsExpired => DateTime.Now > ExpirationDate;
    public List<X509Certificate2> DeveloperCertificates { get; private set; }
    public List<string> Devices { get; private set; }

    private static readonly string[] ProfileDirectories;

    static MobileProvision()
    {
        string personal = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        ProfileDirectories = new string[] {
            // Xcode >= 16.x
            Path.Combine(personal, "Library/Developer/Xcode/Xcode/UserData/Provisioning Profiles"),
            // Xcode < 16.x
            Path.Combine(personal, "Library/MobileDevice/Provisioning Profiles")
        };
    }

    [MenuItem("Build/Test Mobile Provision")]
    public static void Test()
    {
        var mobileProvisionList = new List<MobileProvision>();
        foreach (string dir in ProfileDirectories)
        {
            if (!Directory.Exists(dir))
            {
                continue;
            }
            foreach (string file in Directory.GetFiles(dir))
            {
                if (!file.EndsWith(".mobileprovision"))
                {
                    continue;
                }
                var mobileProvision = LoadFromFile(file);
                if (mobileProvision != null)
                {
                    mobileProvisionList.Add(mobileProvision);
                }

            }
        }
        Debug.Log($"Found {mobileProvisionList.Count} mobileprovision files.");
        mobileProvisionList.RemoveAll(x => x.IsExpired);
        Debug.Log($"Found {mobileProvisionList.Count} valid mobileprovision files.");
    
        // Sort by ExpirationDate descending
        mobileProvisionList.Sort((x, y) => y.ExpirationDate.CompareTo(x.ExpirationDate));
        
        // Sort by ExpirationDate ascending
        mobileProvisionList.Sort((x, y) => y.CreationDate.CompareTo(x.CreationDate));

        mobileProvisionList.RemoveAll(x => x.ApplicationIdentifier != "com.karma.cellveillanceios");
        foreach (var mobileProvision in mobileProvisionList)
        {
            Debug.Log("Provision: " + mobileProvision.ToJson());
        }
    }

    private static MobileProvision LoadFromFile(string filePath)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException("The specified mobileprovision file was not found.", filePath);
        }

        // Step 1: Read the raw content of the .mobileprovision file
        string rawContent = File.ReadAllText(filePath);

        // Step 2: Extract the XML portion between <?xml ... ?> and </plist>
        var xmlMatch = Regex.Match(rawContent, @"<\?xml.*?</plist>", RegexOptions.Singleline);
        if (!xmlMatch.Success)
        {
            throw new FormatException("Could not find valid XML content in the .mobileprovision file.");
        }

        string xmlContent = xmlMatch.Value;

        // Step 3: Load the XML into an XmlDocument for further processing
        XmlDocument xmlDoc = new XmlDocument();
        xmlDoc.LoadXml(xmlContent);

        // Step 4: Extract relevant fields from the XML document
        var rootDict = xmlDoc.SelectSingleNode("/plist/dict");

        MobileProvision mobileProvision = new MobileProvision
        {
            Devices = new List<string>(),
            DeveloperCertificates = new List<X509Certificate2>()
        };
        foreach (XmlNode node in rootDict.ChildNodes)
        {
            if (node.Name != "key")
            {
                continue;
            }
            // string key = node.InnerText;
            // string value = node.NextSibling.InnerText;
            // Debug.Log($"Name: {node.NextSibling.Name}, {key} : {value}");

            switch (node.InnerText)
            {
                case "ProvisionedDevices":
                    ParseDevices(node.NextSibling, mobileProvision.Devices);
                    break;
                case "TeamIdentifier":
                    mobileProvision.TeamIdentifier = node.NextSibling.ChildNodes[0].InnerText;
                    break;
                case "TeamName":
                    mobileProvision.TeamName = GetNextStringValue(node.NextSibling);
                    break;
                case "CreationDate":
                    mobileProvision.CreationDate = DateTime.Parse(node.NextSibling.InnerText);
                    break;
                case "ExpirationDate":
                    mobileProvision.ExpirationDate = DateTime.Parse(node.NextSibling.InnerText);
                    break;
                case "Name":
                    mobileProvision.Name = GetNextStringValue(node.NextSibling);
                    break;
                case "UUID":
                    mobileProvision.UUID = GetNextStringValue(node.NextSibling);
                    break;
                case "Entitlements":
                    ParseEntitlements(node.NextSibling, mobileProvision);
                    break;
                case "DeveloperCertificates":
                    ParseDeveloperCertificates(node.NextSibling, mobileProvision.DeveloperCertificates);
                    break;
                default:
                    // Debug.LogWarning($"Unknown <{node.InnerText}>, {node.NextSibling.Name} - {node.NextSibling.InnerText}");
                    break;
            }

        }
        // Debug.Log("Provision: " + mobileProvision.ToJson());
        return mobileProvision;
    }

    private static void ParseEntitlements(XmlNode node, MobileProvision mobileProvision)
    {
        var applicationIdentifier = "";
        var applicationIdentifierPrefix = "";
        if (node != null && node.Name == "dict")
        {
            foreach (XmlNode childNode in node.ChildNodes)
            {
                if (childNode.Name == "key")
                {
                    string key = childNode.InnerText;
                    string value = childNode.NextSibling.InnerText;
                    if (key == "application-identifier")
                    {
                        applicationIdentifier = value;
                    }
                    else if (key == "com.apple.developer.team-identifier")
                    {
                        applicationIdentifierPrefix = value;
                    }
                }
            }
        }
        mobileProvision.ApplicationIdentifier = applicationIdentifier.Replace($"{applicationIdentifierPrefix}.", "");
       
    }
    
    private static void ParseDevices(XmlNode node, List<string> devices)
    {
        if (node != null && node.Name == "array")
        {
            foreach (XmlNode deviceNode in node.ChildNodes)
            {
                if (deviceNode.Name == "string")
                {
                    devices.Add(deviceNode.InnerText);
                }
            }
        }
    }
    private static void ParseDeveloperCertificates(XmlNode node, List<X509Certificate2> certificates)
    {
        if (node == null || node.Name != "array")
        {
            return;
        }
        foreach (XmlNode certNode in node.ChildNodes)
        {
            if (certNode.Name == "data")
            {
                // Base64 decode the certificate data
                string base64Cert = certNode.InnerText.Trim();
                byte[] certData = Convert.FromBase64String(base64Cert);

                // Create an X509Certificate2 object from the decoded data
                try
                {
                    X509Certificate2 certificate = new X509Certificate2(certData);
                    certificates.Add(certificate);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Failed to parse a certificate: {ex.Message}");
                }
            }
        }
    }

    private static string GetNextStringValue(XmlNode node)
    {
        if (node == null)
        {
            Debug.LogWarning("Node is null. Returning null.");
            return null;
        }

        if (node.Name == "string")
        {
            return node.InnerText;
        }

        Debug.LogWarning($"Unexpected node type: {node.Name}. Expected 'string'.");
        return null;
    }

    public string ToJson()
    {
        
        var jsonBuilder = new System.Text.StringBuilder();
        jsonBuilder.AppendLine("{");
        jsonBuilder.AppendLine($"  \"Name\": \"{Name}\",");
        jsonBuilder.AppendLine($"  \"UUID\": \"{UUID}\",");
        jsonBuilder.AppendLine($"  \"TeamIdentifier\": \"{TeamIdentifier}\",");
        jsonBuilder.AppendLine($"  \"ApplicationIdentifier\": \"{ApplicationIdentifier}\",");
        jsonBuilder.AppendLine($"  \"TeamName\": \"{TeamName}\",");
        jsonBuilder.AppendLine($"  \"CreationDate\": \"{CreationDate}\",");
        jsonBuilder.AppendLine($"  \"ExpirationDate\": \"{ExpirationDate}\",");
        jsonBuilder.AppendLine($"  \"IsExpired\": {IsExpired.ToString().ToLower()},");
        
        jsonBuilder.AppendLine("  \"DeveloperCertificates\": [");
        if (DeveloperCertificates != null && DeveloperCertificates.Count > 0)
        {
            for (int i = 0; i < DeveloperCertificates.Count; i++)
            {
                var cert = DeveloperCertificates[i];
                jsonBuilder.AppendLine("    {");
                jsonBuilder.AppendLine($"      \"Subject\": \"{cert.Subject}\",");
                jsonBuilder.AppendLine($"      \"Issuer\": \"{cert.Issuer}\",");
                jsonBuilder.AppendLine($"      \"Thumbprint\": \"{cert.Thumbprint}\",");
                jsonBuilder.AppendLine($"      \"NotBefore\": \"{cert.NotBefore}\",");
                jsonBuilder.AppendLine($"      \"NotAfter\": \"{cert.NotAfter}\"");
                jsonBuilder.AppendLine($"      \"CommonName\": \"{cert.GetCommonName()}\"");
                jsonBuilder.Append("    }");
                if (i < DeveloperCertificates.Count - 1)
                {
                    jsonBuilder.AppendLine(",");
                }
                else
                {
                    jsonBuilder.AppendLine();
                }
            }
        }
        jsonBuilder.AppendLine("  ]");

        if (Devices == null || Devices.Count == 0)
        {
            jsonBuilder.AppendLine("  \"Devices\": []");
        }
        else
        {
            jsonBuilder.AppendLine($"  \"Devices\": [");
            jsonBuilder.AppendLine($"    {string.Join(", ", Devices)}");
            jsonBuilder.AppendLine("  ]");
        }
        

        jsonBuilder.AppendLine("}");
        return jsonBuilder.ToString();
    }
}

public static class X509Certificate2Extension
{
    public static string GetCommonName(this X509Certificate2 certificate)
    {
        string subject = certificate.Subject;
        int cnIndex = subject.IndexOf("CN=", StringComparison.Ordinal);
        if (cnIndex >= 0)
        {
            int start = cnIndex + 3; // Start after "CN="
            int end = subject.IndexOf(',', start);
            if (end == -1)
            {
                end = subject.Length; // No more commas, take the rest of the string
            }
            return subject.Substring(start, end - start);
        }
        return null;
    }
}

#endif