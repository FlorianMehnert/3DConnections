# Overview

- the RuntimeDependencyStreamer will require the NuGet package ` WebSocketSharp...` install this using NuGetForUnity
- this will stream the current scene structure as json:

```json
"objects": [
    {
        "name": "PanelSettings",
        "components": [
            "Transform",
            "PanelEventHandler",
            "PanelRaycaster"
        ]
    },
    {
        "name": "[Debug Updater]",
        "components": [
            "Transform",
            "DebugUpdater"
        ]
    },
    {
        "name": "OverlayCamera",
        "components": [
            "Transform",
            "Camera",
            "CameraController",
            "UniversalAdditionalCameraData",
            "PlayerInput",
            "NodeTextScaler",
            "GraphLODManager"
        ]
    },
    {
        "name": "ParentEdgesObject",
        "components": [
            "Transform"
        ]
    },
    {
        "name": "Global Volume",
        "components": [
            "Transform",
            "Volume"
        ]
    },
    {
        "name": "SelectionBoxCanvas",
        "components": [
            "RectTransform",
            "Canvas",
            "CanvasScaler",
            "GraphicRaycaster"
        ]
    },
    {
        "name": "ParentNodesObject",
        "components": [
            "Transform"
        ]
    },
    {
        "name": "CubeContextCanvas",
        "components": [
            "RectTransform",
            "Canvas",
            "CanvasScaler",
            "GraphicRaycaster"
        ]
    },
    {
        "name": "Canvas",
        "components": [
            "RectTransform",
            "Canvas",
            "CanvasScaler",
            "GraphicRaycaster"
        ]
    },
    {
        "name": "ScriptableObjectInventory",
        "components": [
            "Transform",
            "ScriptableObjectInventory"
        ]
    },
    {
        "name": "SettingsMenu",
        "components": [
            "Transform",
            "UIDocument",
            "SettingsMenuGeneral",
            "PlayerInput",
            "NavigatableMenu"
        ]
    },
    {
        "name": "EventSystem",
        "components": [
            "Transform",
            "EventSystem",
            "InputSystemUIInputModule"
        ]
    },
    {
        "name": "Simulations",
        "components": [
            "Transform"
        ]
    },
    {
        "name": "ModularMenu",
        "components": [
            "Transform",
            "UIDocument",
            "ModularSettingsManager"
        ]
    },
    {
        "name": "GUIManager",
        "components": [
            "Transform",
            "MenuManager",
            "HoverInfoToolkit"
        ]
    },
    {
        "name": "Manager",
        "components": [
            "Transform",
            "CubeSelector",
            "OverlayToggle",
            "NodeConnectionManager",
            "SpringSimulation",
            "SceneAnalyzer",
            "ComputeSpringSimulation",
            "KeyDisplay",
            "DebugOverlay",
            "MinimalForceDirectedSimulation",
            "SimulationManager",
            "StaticNodeLayoutManager"
        ]
    },
    {
        "name": "FPSDisplay",
        "components": [
            "Transform",
            "FPSDisplay"
        ]
    }
]
```

# Simple usage using nodejs

- in the Network folder execute `npm init -y` + `npm install ws`
- run `node client.js`