# ![nodegraph](./images/3DConnections.png)
This is a Unity Extension used to primarily visualize and analyze scenes in Unity.

# Installation
- in the unity package manager, add this package via git: https://github.com/FlorianMehnert/3DConnections.git
- when extension is installed go to `Tools/3DConnections/` and execute the following:
  - Copy and Add Overlay Scene
  - Add OverlayScene Layer
- In a scene you want to analyze open the context menu (right click) in the Hierarchy: `3DConnections/Add Entrypoint`
- You might also want to change the render pipeline in `Edit/Project Settings` under Graphics to the Universal Render Pipeline Asset (not 2D) for bloom

## GUIBuilder
- execute all Button GUI Methods to ensure proper placement of UI Elements

## ClassParser & ClassReferences
- see SceneConnections

## SceneHandler
- mainly to add another Scene which is loaded in Addivite mode (overlaying the existing scenes)

## NodeBuilder
- Handle the creation and composition of the nodes later used to display dependencies

# How to register new settings
- say you have this field `private bool _showFPS;` you can register the setting using
```csharp
[RegisterModularSetting("Show FPS", "Show the current fps (F3)", "Debug", false)]
private bool _showFPS;
```
- somewhere later e.g. `Awake()` you need to call the following:
```csharp
# Get an instance of the ModularSettingsManager
var settingsManager = FindFirstObjectByType<ModularSettingsManager>();

# Register all settings for this class
if (settingsManager)
{
    RegisterModularSetting.RegisterAllSettings(this, settingsManager);
}
```

# Changelog

## 5.May
- Modular Menu which should be an addition to the existing handcrafted menu
- Allow registering settings in the menu using attributes

## 2.May
- Add the ArtificialGameObject Component to better differentiate between real GameObjects and those who reference Objects 
## 1.May
- fix the recursive drawing of the overlay scene
----
## 2.February
- prevent selection-box from drawing on ui interaction
- create nodes on just pressing <kbd>Return</kbd>
- Resize canvas containing ui buttons based on screen size
- Add color slider to apply the current color theme to all nodes

# 1.February
- trying to build on android - working, but scroll out is missing
- use connections scriptable object to split connectionsManager

# 30.January
- resize icons to be all the same size and set to be on by default

# 29.January
- trying to get into a nice background fast using shaders but took more time than expected
- fix ui is not responsive after reload/recompiling
- add fps display
- got into custom editors displaying buttons to execute functions within their monobehaviour
- add different color palettes

# 28.January
- code analyzation based on roslyn dumped currently in the code_metrics.json file to size known nodes based on maintainability
- add command input; most of the typed keys appear on the screen (:q) + some words are understood as commands and execute functions such as applying different physics sims to the nodegraph

# 26.January
- add different line thickness
- allow building again

# 24.January
- fix package to allow shipping without errors
- add scripts to copy the scene to the assets folder to allow opening from readlony package
- script to add the overlay scene to the build index
- right click context to add entrypoint prefab to your scene
- add license
- remove roslyn dependency


# 23.January
- cleanup
- allow to ignore transforms on creating the nodegraph
- add compute shader implementation of the physics sim
- correct asmdefs and csproj files
- add custom editor to execute camera functions
- add bounds to nodegraph to foucs on
- allow shift rectangle selection
- improve recall for prefab identification using name matching
- add bloom using urp
- improve the color palette

# 22.January
- fixing more bugs

# 21.January
- add package.json to this package
- add standaloneFileBrowser to this package
- fix and update dependencies

# 20.January
- tried and discarded ECS for physics sim due to lacking performance increase
- allow converting physics sim to burst

# 19.January
- add naming to connections
- add editor selection to highlight
- disable scene with entrypoint using overlayEvent
- add springjoints

# 18.January
- fix drawGrid not filling allNodes
- fix max node count is never reset
- add physics sim to resolve overlapping nodes
- fix node connection manager errors on unloading

# 17.January
- follow node connections using increment shortcut ctrl+i

## 16.January
- added selection box
- added camera focus on node-graph via hotkey

## 15.January
- **added nodeType and nodeConnections to node gameObjects to move away from a separate node data structure**
- added new SceneAnalyzer doing the job of AnalyzeScene from NodeBuilder tracking all gameObjects while also following components while spawning nodes without duplicates
- improve the demo scene to contain more cross-references
- automatically calculate button positions in the GUI-Manager now

## 12.January
- add manager to layout gameObjects for nodes using existing connections in the connection manager
- update radial layout to work with the new manager
- fix color is set to default color when selecting node using a new nodeColor component on nodes

## 8.January
- added radial layout
- added ping in selection manager to select node in the editor
- fix children field in nodes (got confused with to analyze gameObjects and node gameObjects)

## 6.January (2025)
- change button canvas to scale with screen size
- add component nodes and connections

## 25.December
- packing files as unitypackage - resolved all dependency problems

## 24.December
- 🌲 Merry Christmas

## 23.December
- Extract SceneHandler Methods
- remove unused prefabs

## 22.December
- Add scriptable object  for node graph data

## 21.December
- allow later configuring color of nodes using scriptable objects
- add tree building algorithm using transforms
- change node material to unlit
- delete old sprites
- add ping to object on double click
- add folders for managers etc.
- add dropdown prefab to only use GUIBuilder for Gui related
- add a scriptable object for Scene selection
- fix prefab dependencies

## 20.December
- modified selection logic using RaycastAll
- constructing a tree from scene transforms parent-child relationships

## 19.December
- documentation in UnityConnections README
- thinking about layouts for 3DConnections

## 18.December
- use prefab as parent object for node spawning
- added Clear method to clean up spawned nodes

## 13.December
- fix zoom with changing viewport size
- way too much time invested into fixing selection logic to be able to handle multi select, dragging, single click deselect as well as highlighting

## 12.December
- fix linux installation of roslyn by manually installing the SQLitePCLRaw.bundle_green package form NuGet
- fixing highlight/dragging interaction with nodes

## 11.December
- using prefabs for nodes and materials now as well for the highlighting of nodes

## 10.December
- trying out roslyn to parse classes
- install using NuGet (which is installed using the git url)
```bash
https://github.com/GlitchEnzo/NuGetForUnity.git?path=/src/NuGetForUnity
```
- install NuGet Package of roslyn: `Microsoft.CodeAnalysis` `Microsoft.CodeAnalysis.CSharp`
- resolved the issue of Workspaces*.dll could not load (linux only) by just deleting them 🙈
- added context menu (raycast still not working on second monitor)

## 1.December
- trying to switch between displays in the Editor
- trying to remove the nodes from the colliders
  - remove checkboxes in the project settings for the node layer (2D and 3D)
- delay the scene load until finally loaded

## 30.November (2024)
- add layer to which I can render stuff that only appears in display 2
  - requires camera in OverlayScene ("NewScene" currently) that renders **orthographically** to **display 2**
  - add new layer in Edit > Project Settings > Tags and Layers → new layer DisplayOverlay
  - select the game object
  - assign the cube to the DisplayOverlay layer in the _Inspector_
  - go to the Culling Mask property in the _Inspector_
  ### Configure Cameras to visualize (everything except) the overlay layer
  1. add a new layer: Edit > Project Settings > Tags and Layers → e.g., DisplayOverlay
  2. assuming the project structure looks as follows: camera 1 is in the original scene rendering something you want to visualize using the 3DConnections addon -> select the camera in the OverlayScene (camera2) and only enable the new Scene in the OcclusionLayer setting in the inspektor
  3. disable the new layer in cam1 (original scene)
  ```csharp
  public string display2LayerName = "Display2Only"; // Layer name to use
  int display2Layer = LayerMask.NameToLayer(display2LayerName); // get the layer index for the specified layer name
  
  public Camera display2Camera;
  display2Camera.cullingMask = 1 << display2Layer; // Set culling mask to include only the layer```
  
  Camera mainCamera = Camera.main;
  mainCamera.cullingMask &= ~(1 << display2Layer); // Eclude the culling mask layer from the main camera
