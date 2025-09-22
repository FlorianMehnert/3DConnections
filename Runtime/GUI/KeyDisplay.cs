using JetBrains.Annotations;

namespace _3DConnections.Runtime.Managers
{
    using Events;
    using System;
    using UnityEngine;
    using UnityEngine.SceneManagement;
    using ScriptableObjectInventory;
    using ScriptableObjects;

    public class KeyDisplay : MonoBehaviour
    {
        private string _inputString = "";
        private string _debugString = "";

        private const float ClearDelay = 1f;

        private float _lastInputTime;
        private float _lastDebugTime;
        private GUIStyle _style;
        private GUIStyle _debugStyle;
        [SerializeField] private ClearEvent clearEvent;

        private void Start()
        {
            _style = new GUIStyle
            {
                normal =
                {
                    textColor = Color.white
                },
                fontSize = 20,
                alignment = TextAnchor.MiddleLeft
            };
            _debugStyle = new GUIStyle
            {
                normal =
                {
                    textColor = Color.red
                },
                fontSize = 20,
                alignment = TextAnchor.MiddleLeft
            };
        }

        private void Log(string message, float extendDelay = 0f)
        {
            _lastDebugTime = Time.time + extendDelay;
            _debugString = message;
        }

        private void OnGUI()
        {
            // Set the position and size of the text area
            const float x = 10f;
            var y = Screen.height - 50f;
            const float width = 500f; // Increased width to accommodate longer text
            const float height = 40f;

            // Draw the input string on the screen
            GUI.Label(new Rect(x, y, width, height), _inputString, _style);
            GUI.Label(new Rect(x, y - height, width, height), _debugString, _debugStyle);
        }

        private NodeGraphScriptableObject GetNodeGraph()
        {
            var nodegraphs = Resources.FindObjectsOfTypeAll<NodeGraphScriptableObject>();
            switch (nodegraphs.Length)
            {
                case 0:
                    return null;
                case 1:
                    return nodegraphs[0];
                default:

                    Log("there exist multiple nodegraph scriptable objects");
                    return nodegraphs[0];
            }
        }

        private void Update()
        {
            if (Time.time - _lastInputTime > ClearDelay / 2)
                _style.normal.textColor = new Color(1, 1, 1, 1);
            if (Time.time - _lastInputTime > ClearDelay && !string.IsNullOrEmpty(_inputString))
                _inputString = "";
            if (Time.time - _lastDebugTime > ClearDelay && !string.IsNullOrEmpty(_debugString))
                _debugString = "";

            // Check for key presses
            if (!Input.anyKeyDown) return;
            foreach (KeyCode keyCode in Enum.GetValues(typeof(KeyCode)))
            {
                if (!Input.GetKeyDown(keyCode)) continue;
                // Handle special cases for Shift + key combinations
                if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
                {
                    if (Input.GetKey(KeyCode.Backspace))
                        _inputString = "";
                    else
                        _inputString += keyCode switch
                        {
                            // Convert Shift + key to uppercase
                            >= KeyCode.A and <= KeyCode.Z => keyCode.ToString().ToUpper() + " ",
                            KeyCode.Alpha1 => "!",
                            KeyCode.Alpha2 => "@",
                            KeyCode.Alpha3 => "#",
                            KeyCode.Alpha4 => "$",
                            KeyCode.Alpha5 => "%",
                            KeyCode.Alpha6 => "^",
                            KeyCode.Alpha7 => "&",
                            KeyCode.Alpha8 => "*",
                            KeyCode.Alpha9 => "(",
                            KeyCode.Alpha0 => ")",
                            KeyCode.Minus => "_",
                            KeyCode.Equals => "+",
                            KeyCode.LeftBracket => "{",
                            KeyCode.RightBracket => "}",
                            KeyCode.Quote => "\"",
                            KeyCode.Comma => ";",
                            (KeyCode)66 => ">",
                            KeyCode.Slash => "?",
                            KeyCode.Period => ":",
                            _ => ""
                        };
                }
                else
                {
                    if (keyCode is KeyCode.Backspace)
                    {
                        if (_inputString != "")
                            _inputString = _inputString.Remove(_inputString.Length - 1);
                    }
                    else if (keyCode is not (KeyCode.Mouse0 or KeyCode.Mouse1 or KeyCode.Mouse2 or KeyCode.LeftAlt
                                 or KeyCode.LeftControl or KeyCode.LeftCommand or KeyCode.LeftWindows
                                 or KeyCode.RightShift or KeyCode.Escape) || IsConfirm())
                    {
                        _inputString += keyCode switch
                        {
                            // Handle regular key presses
                            >= KeyCode.A and <= KeyCode.Z => keyCode.ToString().ToLower(),
                            KeyCode.Space => " ",
                            KeyCode.Alpha0 => "0",
                            KeyCode.Alpha1 => "1",
                            KeyCode.Alpha2 => "2",
                            KeyCode.Alpha3 => "3",
                            KeyCode.Alpha4 => "4",
                            KeyCode.Alpha5 => "5",
                            KeyCode.Alpha6 => "6",
                            KeyCode.Alpha7 => "7",
                            KeyCode.Alpha8 => "8",
                            KeyCode.Alpha9 => "9",
                            KeyCode.Period => ".",
                            KeyCode.Keypad1 => "1",
                            KeyCode.Keypad2 => "2",
                            KeyCode.Keypad3 => "3",
                            KeyCode.Return => "",
                            _ => keyCode + ""
                        };
                    }
                }

                // Update the last input time
                _lastInputTime = Time.time;
                // Check for the ":q" sequence
                if (_inputString.Contains(":q") && IsConfirm())
                {
                    ExitApplication();
                }
                else if (_inputString.Contains("loadscene") && IsConfirm())
                {
                    if (!SceneManager.GetSceneAt(0).isLoaded)
                        SceneManager.LoadSceneAsync(0, LoadSceneMode.Additive);
                    else
                        Log(SceneManager.GetSceneAt(0).name + " is already loaded", 2f);
                }
                else if (_inputString.Contains("stats") && IsConfirm())
                {
                    var nodeGraph = GetNodeGraph();
                    var message = nodeGraph
                        ? "nodeCount is: " + nodeGraph.AllNodes.Count + " "
                        : "there exists no nodeGraph scriptable object ";
                    message += "connection count is: " + ScriptableObjectInventory.Instance.conSo.connections.Count;
                    Debug.Log(message);
                    Log(message);
                }
                else if ((_inputString.Contains("clear") && IsConfirm()) ||
                         (_inputString.Contains("reset") && IsConfirm()))
                {
                    clearEvent.TriggerEvent();
                }
                else if (IsConfirm())
                {
                    var simulationManager = FindFirstObjectByType<SimulationManager>();
                    if (simulationManager)
                    {
                        if (_inputString.StartsWith("0"))
                        {
                            ScriptableObjectInventory.Instance.simulationParameters.simulationType = SimulationType.Static;
                            ScriptableObjectInventory.Instance.simConfig.SimulationType = SimulationType.Static;
                            simulationManager.Simulate();
                            _inputString = "Remove the simulation";
                        }
                        else if (_inputString.StartsWith("1"))
                        {
                            ScriptableObjectInventory.Instance.simulationParameters.simulationType = SimulationType.UnityPhysics;
                            ScriptableObjectInventory.Instance.simConfig.SimulationType = SimulationType.UnityPhysics;
                            simulationManager.Simulate();
                            _inputString =  "Apply a spring component based simulation";
                        }
                        else if (_inputString.StartsWith("2"))
                        {
                            ScriptableObjectInventory.Instance.simulationParameters.simulationType = SimulationType.ForceDirected;
                            ScriptableObjectInventory.Instance.simConfig.SimulationType = SimulationType.ForceDirected;
                            simulationManager.Simulate();
                            _inputString = "Apply a custom force-directed simulation";
                        }

                        _inputString = "";
                    }
                    else
                    {
                        Debug.Log("did not find SettingMenuGeneral script");
                    }
                }

                // Limit the input string length to avoid overflow
                if (_inputString.Length > 50) _inputString = _inputString[^50..];
            }
        }

        /// <summary>
        /// Manually register for each event and describe what is being executed
        /// </summary>
        /// <param name="actionName"></param>
        [UsedImplicitly]
        public void ShowKeyAction(string actionName)
        {
            _inputString = actionName;
        }

        private static bool IsConfirm()
        {
            return Input.GetKey(KeyCode.Return) || Input.GetKey(KeyCode.KeypadEnter);
        }

        private static void ExitApplication()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}