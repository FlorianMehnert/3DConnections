namespace _3DConnections.Runtime.Analysis
{
    public class NullProgressReporter : IProgressReporter
    {
        public void StartOperation(string operationName, int totalSteps) { }
        public void ReportProgress(string operation, int current, int total, string currentItem = null) { }
        public void CompleteOperation() { }
    }
}