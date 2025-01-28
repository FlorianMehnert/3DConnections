using System;
using UnityEngine;
using System.Collections;
using UnityEngine.SceneManagement;

public class KeyDisplay : MonoBehaviour
{
    private string _inputString = "";
    private const float ClearDelay = 1f; // Time in seconds before the input string disappears
    private float _lastInputTime;
    private GUIStyle _style;

    private void Start()
    {
        _style = new GUIStyle();
        _style.normal.textColor = Color.white;
        _style.fontSize = 20;
        _style.alignment = TextAnchor.MiddleLeft;
    }

    private void OnGUI()
    {
        // Set the position and size of the text area
        const float x = 10f;
        var y = Screen.height - 50f;
        const float width = 500f; // Increased width to accommodate longer text
        const float height = 40f;

        // Draw the input string on the screen
        GUI.Label(new Rect(x, y, width, height), "Input: " + _inputString, _style);
    }

    private void Update()
    {
        if (Time.time - _lastInputTime > ClearDelay / 2)
            _style.normal.textColor = new Color(1, 1, 1, 1);
        if (Time.time - _lastInputTime > ClearDelay && !string.IsNullOrEmpty(_inputString))
            _inputString = "";

        // Check for key presses
        if (!Input.anyKeyDown) return;
        foreach (KeyCode keyCode in System.Enum.GetValues(typeof(KeyCode)))
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
                if (keyCode is not (KeyCode.Mouse0 or KeyCode.Mouse1 or KeyCode.Mouse2 or KeyCode.LeftAlt or KeyCode.LeftControl or KeyCode.LeftCommand or KeyCode.LeftWindows or KeyCode.RightShift or KeyCode.Return))
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
                SceneManager.LoadScene(0);
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