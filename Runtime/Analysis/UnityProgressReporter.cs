using System;

namespace _3DConnections.Runtime.Analysis
{
#if UNITY_EDITOR
    using UnityEditor;

    public class UnityProgressReporter : IProgressReporter
    {
        private string _currentOperation;
        private bool _isDisplaying;

        public void StartOperation(string operationName, int totalSteps)
        {
            _currentOperation = operationName;
            _isDisplaying = true;
        }

        public void ReportProgress(string operation, int current, int total, string currentItem = null)
        {
            if (!_isDisplaying || total == 0) return;

            float progress = (float)current / total;
            string info = currentItem ?? $"Step {current} of {total}";
        
            // Use Unity's progress bar [[7]]
            if (EditorUtility.DisplayCancelableProgressBar(_currentOperation, info, progress))
            {
                EditorUtility.ClearProgressBar();
                throw new OperationCanceledException("Analysis cancelled by user");
            }
        }

        public void CompleteOperation()
        {
            if (_isDisplaying)
            {
                EditorUtility.ClearProgressBar();
                _isDisplaying = false;
            }
        }
    }
#endif

}