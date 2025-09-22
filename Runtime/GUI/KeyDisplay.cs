using JetBrains.Annotations;
using System.Collections;

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

        private Coroutine _clearInputCoroutine;
        private Coroutine _clearDebugCoroutine;
        private Coroutine _fadeCoroutine;

        private void Start()
        {
            _style = new GUIStyle
            {
                normal = { textColor = Color.white },
                fontSize = 20,
                alignment = TextAnchor.MiddleLeft
            };
            _debugStyle = new GUIStyle
            {
                normal = { textColor = Color.red },
                fontSize = 20,
                alignment = TextAnchor.MiddleLeft
            };
        }

        private void OnEnable()
        {
            // Subscribe to Unity's input events instead of polling
            Application.focusChanged += OnApplicationFocus;
        }

        private void OnDisable()
        {
            Application.focusChanged -= OnApplicationFocus;
            
            // Clean up coroutines
            if (_clearInputCoroutine != null) StopCoroutine(_clearInputCoroutine);
            if (_clearDebugCoroutine != null) StopCoroutine(_clearDebugCoroutine);
            if (_fadeCoroutine != null) StopCoroutine(_fadeCoroutine);
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            // Reset input when application loses/gains focus
            if (!hasFocus)
            {
                _inputString = "";
                _debugString = "";
            }
        }

        private void Log(string message, float extendDelay = 0f)
        {
            _lastDebugTime = Time.time + extendDelay;
            _debugString = message;
            
            // Use coroutine instead of checking in Update
            if (_clearDebugCoroutine != null) StopCoroutine(_clearDebugCoroutine);
            _clearDebugCoroutine = StartCoroutine(ClearDebugAfterDelay(ClearDelay + extendDelay));
        }

        private void OnGUI()
        {
            // Handle input events here instead of Update - OnGUI is event-driven [[4]]
            if (Event.current.type == EventType.KeyDown)
            {
                HandleKeyInput(Event.current.keyCode);
            }

            // Set the position and size of the text area
            const float x = 10f;
            var y = Screen.height - 50f;
            const float width = 500f;
            const float height = 40f;

            // Draw the input string on the screen
            GUI.Label(new Rect(x, y, width, height), _inputString, _style);
            GUI.Label(new Rect(x, y - height, width, height), _debugString, _debugStyle);
        }

        private void HandleKeyInput(KeyCode keyCode)
        {
            _lastInputTime = Time.time;
            
            // Start fade coroutine
            if (_fadeCoroutine != null) StopCoroutine(_fadeCoroutine);
            _fadeCoroutine = StartCoroutine(FadeTextAfterDelay());
            
            // Start clear input coroutine
            if (_clearInputCoroutine != null) StopCoroutine(_clearInputCoroutine);
            _clearInputCoroutine = StartCoroutine(ClearInputAfterDelay());

            // Handle special cases for Shift + key combinations
            if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
            {
                if (keyCode == KeyCode.Backspace)
                {
                    _inputString = "";
                    return;
                }
                
                _inputString += GetShiftedCharacter(keyCode);
            }
            else
            {
                if (keyCode == KeyCode.Backspace)
                {
                    if (_inputString.Length > 0)
                        _inputString = _inputString.Remove(_inputString.Length - 1);
                    return;
                }
                
                if (ShouldIgnoreKey(keyCode) && !IsConfirm())
                    return;
                    
                _inputString += GetRegularCharacter(keyCode);
            }

            // Check for commands
            ProcessCommands();

            // Limit the input string length to avoid overflow
            if (_inputString.Length > 50) 
                _inputString = _inputString[^50..];
        }

        private string GetShiftedCharacter(KeyCode keyCode)
        {
            return keyCode switch
            {
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

        private string GetRegularCharacter(KeyCode keyCode)
        {
            return keyCode switch
            {
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
                _ => keyCode.ToString()
            };
        }

        private bool ShouldIgnoreKey(KeyCode keyCode)
        {
            return keyCode is KeyCode.Mouse0 or KeyCode.Mouse1 or KeyCode.Mouse2 
                or KeyCode.LeftAlt or KeyCode.LeftControl or KeyCode.LeftCommand 
                or KeyCode.LeftWindows or KeyCode.RightShift or KeyCode.Escape;
        }

        private void ProcessCommands()
        {
            if (_inputString.Contains(":q") && IsConfirm())
            {
                ExitApplication();
            }
            else if (_inputString.Contains("loadscene") && IsConfirm())
            {
                HandleLoadScene();
            }
            else if (_inputString.Contains("stats") && IsConfirm())
            {
                HandleStats();
            }
            else if ((_inputString.Contains("clear") || _inputString.Contains("reset")) && IsConfirm())
            {
                clearEvent.TriggerEvent();
            }
            else if (IsConfirm())
            {
                HandleSimulationCommands();
            }
        }

        private void HandleLoadScene()
        {
            if (!SceneManager.GetSceneAt(0).isLoaded)
                SceneManager.LoadSceneAsync(0, LoadSceneMode.Additive);
            else
                Log(SceneManager.GetSceneAt(0).name + " is already loaded", 2f);
        }

        private void HandleStats()
        {
            var nodeGraph = GetNodeGraph();
            var message = nodeGraph
                ? "nodeCount is: " + nodeGraph.AllNodes.Count + " "
                : "there exists no nodeGraph scriptable object ";
            message += "connection count is: " + ScriptableObjectInventory.Instance.conSo.connections.Count;
            Debug.Log(message);
            Log(message);
        }

        private void HandleSimulationCommands()
        {
            var simulationManager = FindFirstObjectByType<SimulationManager>();
            if (simulationManager)
            {
                if (_inputString.StartsWith("0"))
                {
                    SetSimulationType(SimulationType.Static, "Remove the simulation");
                    simulationManager.Simulate();
                }
                else if (_inputString.StartsWith("1"))
                {
                    SetSimulationType(SimulationType.UnityPhysics, "Apply a spring component based simulation");
                    simulationManager.Simulate();
                }
                else if (_inputString.StartsWith("2"))
                {
                    SetSimulationType(SimulationType.ForceDirected, "Apply a custom force-directed simulation");
                    simulationManager.Simulate();
                }

                _inputString = "";
            }
            else
            {
                Debug.Log("did not find SimulationManager script");
            }
        }

        private void SetSimulationType(SimulationType type, string message)
        {
            ScriptableObjectInventory.Instance.simulationParameters.simulationType = type;
            ScriptableObjectInventory.Instance.simConfig.SimulationType = type;
            _inputString = message;
        }

        // Coroutines to replace Update loop checks
        private IEnumerator ClearInputAfterDelay()
        {
            yield return new WaitForSeconds(ClearDelay);
            if (!string.IsNullOrEmpty(_inputString))
                _inputString = "";
        }

        private IEnumerator ClearDebugAfterDelay(float delay)
        {
            yield return new WaitForSeconds(delay);
            if (!string.IsNullOrEmpty(_debugString))
                _debugString = "";
        }

        private IEnumerator FadeTextAfterDelay()
        {
            yield return new WaitForSeconds(ClearDelay / 2);
            _style.normal.textColor = new Color(1, 1, 1, 1);
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

        [UsedImplicitly]
        public void ShowKeyAction(string actionName)
        {
            _inputString = actionName;
            
            // Reset timers and start coroutines
            _lastInputTime = Time.time;
            if (_clearInputCoroutine != null) StopCoroutine(_clearInputCoroutine);
            _clearInputCoroutine = StartCoroutine(ClearInputAfterDelay());
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
