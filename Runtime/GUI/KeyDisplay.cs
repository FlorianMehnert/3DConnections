using System;
using UnityEngine;
using System.Collections;
using UnityEngine.SceneManagement;

public class KeyDisplay : MonoBehaviour
{
    private string _inputString = "";
    private string _debugString = "";
    private const float ClearDelay = 1f; // Time in seconds before the input string disappears
    private float _lastInputTime;
    private float _lastDebugTime;
    private GUIStyle _style;
    private GUIStyle _debugStyle;

    private void Start()
    {
        _style = new();
        _style.normal.textColor = Color.white;
        _style.fontSize = 20;
        _style.alignment = TextAnchor.MiddleLeft;
        _debugStyle = new();
        _debugStyle.normal.textColor = Color.red;
        _debugStyle.fontSize = 20;
        _debugStyle.alignment = TextAnchor.MiddleLeft;
    }

    private void Log(string message)
    {
        _lastDebugTime = Time.time;
        _debugString = _debugString + " " + message;
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
        GUI.Label(new Rect(x, y-height, width, height), _debugString, _debugStyle);
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
                else if (keyCode is not (KeyCode.Mouse0 or KeyCode.Mouse1 or KeyCode.Mouse2 or KeyCode.LeftAlt or KeyCode.LeftControl or KeyCode.LeftCommand or KeyCode.LeftWindows or KeyCode.RightShift or KeyCode.Return))
                {
                    _inputString += keyCode switch
                    {
                        // Handle regular key presses
                        >= KeyCode.A and <= KeyCode.Z => keyCode.ToString().ToLower(),
                        KeyCode.Space => " ",
                        _ => keyCode + ""
                    };
                }
            }

            // Update the last input time
            _lastInputTime = Time.time;
            if (Input.GetKey(KeyCode.Return))
                _style.normal.textColor = Color.green;
                
            // Check for the ":q" sequence
            if (_inputString.Contains(":q") && Input.GetKey(KeyCode.Return))
            {
                ExitApplication();
            }else if (_inputString.Contains(":loadscene") && Input.GetKey(KeyCode.Return))
            {
                SceneManager.LoadSceneAsync(0, LoadSceneMode.Additive);
            }else if (_inputString.Contains(":nodecount") && Input.GetKey(KeyCode.Return))
            {
                var nodeGraph = GetNodeGraph();
                var message = nodeGraph ? "nodecount is: " + nodeGraph.AllNodes.Count : "there exists no nodegraph scriptable object";
                Debug.Log(message);
                Log(message);
            }else if (_inputString.Contains(":ui") && Input.GetKey(KeyCode.Return))
            {
                var gui = gameObject.GetComponent<GUIBuilder>();
                if (gui)
                    gui.Init();
            }

            // Limit the input string length to avoid overflow
            if (_inputString.Length > 50)
            {
                _inputString = _inputString[^50..];
            }
        }
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