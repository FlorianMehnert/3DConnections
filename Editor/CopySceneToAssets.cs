using UnityEngine;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using System.IO;
using System.Linq;

public class CopySceneFromPackage
{
    private static ListRequest _listRequest;
    private static string packageName = "com.florian-mehnert.3d-connections";
    private static string sceneName = "OverlayScene.unity";

    [MenuItem("Tools/Copy and Add Overlay Scene")]
    public static void CopyOverlayScene()
    {
        _listRequest = Client.List();
        EditorApplication.update += OnPackageListRequest;
    }

    private static void OnPackageListRequest()
    {
        if (_listRequest.IsCompleted)
        {
            EditorApplication.update -= OnPackageListRequest;

            if (_listRequest.Status == StatusCode.Success)
            {
                foreach (var package in _listRequest.Result)
                {
                    if (package.name == packageName)
                    {
                        string packagePath = package.resolvedPath;
                        string scenePath = Path.Combine(packagePath, "Assets/Scenes/", sceneName);
                        string destinationPath = Path.Combine("Assets/Scenes/", sceneName);

                        if (File.Exists(scenePath))
                        {
                            if (File.Exists(destinationPath))
                            {
                                // Delete existing file before copying
                                File.Delete(destinationPath);
                                Debug.Log("Deleted existing scene file: " + destinationPath);
                            }

                            // Ensure the target directory exists
                            string destinationDir = Path.GetDirectoryName(destinationPath);
                            if (!Directory.Exists(destinationDir))
                            {
                                Directory.CreateDirectory(destinationDir);
                                Debug.Log("Created directory: " + destinationDir);
                            }

                            // Copy the scene to the Assets/Scenes folder
                            FileUtil.CopyFileOrDirectory(scenePath, destinationPath);
                            Debug.Log("Scene copied successfully to " + destinationPath);

                            // Add the scene to build settings
                            AddSceneToBuildSettings(destinationPath);

                            // Refresh the Unity asset database
                            AssetDatabase.Refresh();
                        }
                        else
                        {
                            Debug.LogError("Scene not found in package: " + scenePath);
                        }

                        return;
                    }
                }

                Debug.LogError("Package not found: " + packageName);
            }
            else
            {
                Debug.LogError("Failed to retrieve package list.");
            }
        }
    }

    private static void AddSceneToBuildSettings(string scenePath)
    {
        var scenes = EditorBuildSettings.scenes.ToList();

        if (!scenes.Any(s => s.path == scenePath))
        {
            scenes.Add(new EditorBuildSettingsScene(scenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
            Debug.Log("Added scene to build settings: " + scenePath);
        }
        else
        {
            Debug.Log("Scene already exists in build settings: " + scenePath);
        }
    }
}

