# 3D Connections
- this is a Unity Asset requires to be symlinked into the Packages folder of the project you want to use it in
- add an empty and add the GUIBuilder Script to it
```bash
# in the folder of 3D
ln -s <PATH-TO-3DConnections> <PATH-TO-ASSETS-FOLDER-IN-YOUR-PROJECT>/3DConnections
```
- to add UnityStandaloneFileBrowser to your Project please download this [unitypackage](https://github.com/gkngkc/UnityStandaloneFileBrowser) and import it to your project: Assets/Import Package

## GUIBuilder
- executes all Button GUI Methods to ensure proper placement of UI Elements

## ClassParser & ClassReferences
- see SceneConnections

## SceneHandler
- mainly to add another Scene which is loaded in Addivite mode (overlaying the existing scenes)

## NodeBuilder
- Handle the creation and composition of the nodes later used to display dependencies

## Deprecated
- ScriptDependencyAnalyzer, ScriptVisualization, MainScript (All used to play around with node creation and adapting the SceneConnections approach)