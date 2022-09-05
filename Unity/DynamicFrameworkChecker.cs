// Translator

using System;
using System.IO;
using UnityEngine;

public class DynamicFrameworkChecker
{
    /// <summary>
    /// Check iOS dynamic framework settings.
    /// </summary>
    /// <param name="dymaicFrameworkList">The target dynamic framework list.</param>
    /// <param name="searchPath">The work path to search the dynamic framework.</param>
    public static void Run(string[] dymaicFrameworkList, string searchPath = "Assets/Plugins/IOS")
    {
        foreach (string filePath in Directory.GetDirectories(searchPath, "*.framework", searchOption: SearchOption.AllDirectories))
        {
            bool isFrameworkFind = false;
            foreach (string frameworkName in dymaicFrameworkList)
            {
                if (filePath.EndsWith(frameworkName))
                {
                    isFrameworkFind = true;
                    break;
                }
            }
            if (!isFrameworkFind)
            {
                continue;
            }

            UnityEditor.PluginImporter importer = UnityEditor.AssetImporter.GetAtPath(filePath) as UnityEditor.PluginImporter;
            if (importer == null)
            {
                Debug.LogError($"PluginImporter not found: {filePath}");
                throw new System.Exception("PluginImporter not found");
            }

            string oldValue = importer.GetPlatformData(UnityEditor.BuildTarget.iOS, "AddToEmbeddedBinaries");
            if (oldValue != "true")
            {
                Debug.Log($"begin set AddToEmbeddedBinaries at: {filePath}");
                importer.SetPlatformData(UnityEditor.BuildTarget.iOS, "AddToEmbeddedBinaries", "true");
                importer.SaveAndReimport();
            }
            else
            {
                Debug.LogFormat($"Already set AddToEmbeddedBinaries at: {filePath}.");
            }
        }
    }
}
