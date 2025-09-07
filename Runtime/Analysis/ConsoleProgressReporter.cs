using System;

namespace _3DConnections.Runtime.Analysis
{
    public class ConsoleProgressReporter : IProgressReporter
    {
        private string _currentOperation;
        private int _totalSteps;
        private int _lastPercentage = -1;

        public void StartOperation(string operationName, int totalSteps)
        {
            _currentOperation = operationName;
            _totalSteps = totalSteps;
            _lastPercentage = -1;
            Console.WriteLine($"Starting: {operationName}");
            Console.Write("Progress: [");
            Console.Write(new string(' ', 50));
            Console.Write("] 0%");
            Console.SetCursorPosition(Console.CursorLeft - 53, Console.CursorTop);
        }

        public void ReportProgress(string operation, int current, int total, string currentItem = null)
        {
            if (total == 0) return;

            int percentage = (int)((double)current / total * 100);
            int progressChars = (int)((double)current / total * 50);

            if (percentage != _lastPercentage)
            {
                // Update progress bar [[4]]
                Console.SetCursorPosition(Console.CursorLeft, Console.CursorTop);
                Console.Write("[");
                Console.Write(new string('█', progressChars));
                Console.Write(new string(' ', 50 - progressChars));
                Console.Write($"] {percentage}%");
            
                if (!string.IsNullOrEmpty(currentItem))
                {
                    Console.Write($" - {currentItem}");
                }
            
                _lastPercentage = percentage;
            }
        }

        public void CompleteOperation()
        {
            Console.WriteLine($"\n{_currentOperation} completed!");
        }
    }

}