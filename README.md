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
# Experimental

## 30.November
- add layer to which I can render stuff that only appears in display 2
  - requires camera in OverlayScene ("NewScene" currently) that renders **orthographically** to **display 2**
  - add new layer in Edit > Project Settings > Tags and Layers -> new layer DisplayOverlay
  - select the game object
  - assign the cube to the DisplayOverlay layer in the _Inspector_
  - go to the Culling Mask property in the _Inspector_
  ### Configure Cameras to visualize (everything except) the overlay layer
  1. add a new layer: Edit > Project Settings > Tags and Layers -> e.g. DisplayOverlay
  2. assuming the project structure looks as follows: camera 1 is in the original scene rendering something you want to visualize using the 3DConnections addon -> select the camera in the OverlayScene (camera2) and only enable the new Scene in the OcclusionLayer setting in the inspektor
  3. disable the new layer in cam1 (original scene)
  ```csharp
  public string display2LayerName = "Display2Only"; // Layer name to use
  int display2Layer = LayerMask.NameToLayer(display2LayerName); // get the layer index for the specified layer name
  
  public Camera display2Camera;
  display2Camera.cullingMask = 1 << display2Layer; // Set culling mask to include only the layer```
  
  Camera mainCamera = Camera.main;
  mainCamera.cullingMask &= ~(1 << display2Layer); // Eclude the culling mask layer from the main camera
  
## 1.December
- trying to switch between displays in the Editor
- trying to remove the nodes from the colliders
  - remove checkboxes in the project settings for the node layer (2D and 3D)
- delay the scene load until finally loaded

## 10.December
- trying out roslyn to parse classes
- install using NuGet (which is installed using the git url)
```bash
https://github.com/GlitchEnzo/NuGetForUnity.git?path=/src/NuGetForUnity
```
- install NuGet Package of roslyn: `Microsoft.CodeAnalysis` `Microsoft.CodeAnalysis.CSharp`
- resolved the issue of Workspaces*.dll could not load (linux only) by just deleting them 🙈
- added context menu (raycast still not working on second monitor)
## 11.December
- using prefabs for nodes and materials now as well for the highlighting of nodes
## 12.December
- fix linux installation of roslyn by manually installing the SQLitePCLRaw.bundle_green package form NuGet
- fixing highlight/dragging interaction with nodes
## 13.December
- fix zoom with changing viewport size
- way too much time invested into fixing selection logic to be able to handle multi select, dragging, single click deselect as well as highlighting

## 18.December
- use prefab as parent object for node spawning
- added Clear method to clean up spawned nodes

## 19.December
- documentation in UnityConnections README
- thinking about layouts for 3DConnections

## 20.December
- modified selection logic using RaycastAll
- Started working on building a tree structure to display parent-child relationships