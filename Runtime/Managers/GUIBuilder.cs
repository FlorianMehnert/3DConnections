using System.Collections.Generic;
using System.Linq;
using _3DConnections.Runtime.ScriptableObjects;
using SFB;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace _3DConnections.Runtime.Managers
{
    /// <summary>
    /// Manager class responsible for the Layout of all Buttons in scene1/2
    /// </summary>
    public class GUIBuilder : MonoBehaviour
    {
        private NodeBuilder _nodeBuilder;
        public string[] path;
        [SerializeField] private TMP_Dropdown dropdownPrefab;
        [SerializeField] private GameObject buttonPrefab;
        [SerializeField] private Canvas uiCanvas;
        private TMP_Dropdown _dropdownInstance;
        [SerializeField] private ToAnalyzeSceneScriptableObject analyzeSceneConfig;
        [SerializeField] private OverlaySceneScriptableObject overlaySceneConfig;

        private void Start()
        {
            _nodeBuilder = GetComponent<NodeBuilder>();
            if (_nodeBuilder == null)
            {
                Debug.Log("The NodeBuilder component is missing on the manager");
            }
            CreateSceneDropdown();
            CreateButtons();
        }

        private void CreateSceneDropdown()
        {
            // Instantiate the dropdown prefab as child of canvas
            var dropdownInstance = Instantiate(dropdownPrefab, uiCanvas.transform);
            _dropdownInstance = dropdownInstance;
        
            // Get the dropdown component (works with both standard and TMP dropdowns)
            var tmpDropdown = dropdownInstance.GetComponent<TMP_Dropdown>();
            var standardDropdown = dropdownInstance.GetComponent<Dropdown>();
        
            // Clear existing options
            if (tmpDropdown != null)
            {
                tmpDropdown.ClearOptions();
            }
            else if (standardDropdown != null)
            {
                standardDropdown.ClearOptions();
            }
        
            // Get all scenes in build settings
            var sceneCount = SceneManager.sceneCountInBuildSettings;
            var sceneOptions = new List<string>();
        
            for (var i = 0; i < sceneCount; i++)
            {
                var scenePath = SceneUtility.GetScenePathByBuildIndex(i);
                var sceneName = System.IO.Path.GetFileNameWithoutExtension(scenePath);
                sceneOptions.Add(sceneName);
            }
        
            if (tmpDropdown != null)
            {
                tmpDropdown.AddOptions(sceneOptions);
            }
            else if (standardDropdown != null)
            {
                standardDropdown.AddOptions(sceneOptions.Select(option => new Dropdown.OptionData(option)).ToList());
            }
        
            var rectTransform = dropdownInstance.GetComponent<RectTransform>();
            rectTransform.anchoredPosition = new Vector2(500, 400);
            SceneReference sr = ScriptableObject.CreateInstance<SceneReference>();
            //ToAnalyzeSceneScriptableObject.scene
            dropdownInstance.onValueChanged.AddListener(OnDropdownValueChanged);
        }

        private void CreateButtons()
        {
            CreateButton("Open File Browser", 14, new Vector2(500, 365), OnFileBrowserOpen);
            CreateButton("Draw Grid", 14, new Vector2(500, 330), () => _nodeBuilder.DrawGrid(path));
            CreateButton("Draw Tree", 14, new Vector2(500, 295), _nodeBuilder.DrawTree);
            CreateButton("Clear", 14, new Vector2(500, 260), _nodeBuilder.Clear);
        }

        private void CreateButton(string text, int fontSize, Vector2 anchoredPosition, UnityAction onClick)
        {
            var browserButtonInstance = Instantiate(buttonPrefab, uiCanvas.transform);
            var buttonComponent = browserButtonInstance.GetComponent<Button>();
            var rectTransform = browserButtonInstance.GetComponent<RectTransform>();
            
            rectTransform.anchoredPosition = anchoredPosition;
            var buttonText = browserButtonInstance.GetComponentInChildren<TextMeshProUGUI>();
            if (buttonText != null)
            {
                buttonText.text = text;
                buttonText.fontSize = fontSize;
            }

            if (buttonComponent != null) buttonComponent.onClick.AddListener(onClick);
        }

        private void OnDropdownValueChanged(int index)
        {
            var selectedScene = _dropdownInstance.options[index].text;
            var scene = SceneManager.GetSceneByName(selectedScene);

            var newSceneRef = ScriptableObject.CreateInstance<SceneReference>();
            newSceneRef.useStaticValues = false;
            newSceneRef.scene = scene;
            newSceneRef.sceneName = scene.name;
            newSceneRef.scenePath = scene.path;

            analyzeSceneConfig.scene = newSceneRef;
            Debug.Log("the new config scene is " + analyzeSceneConfig.scene.Name + " " + analyzeSceneConfig.scene.Path);
        }

        private void OnFileBrowserOpen()
        {
            path = StandaloneFileBrowser.OpenFolderPanel("Open File", "/home/florian/Bilder", false);
        }
    }
}