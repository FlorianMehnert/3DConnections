namespace _3DConnections.Runtime.Analysis
{
    public interface IProgressReporter
    {
        void ReportProgress(string operation, int current, int total, string currentItem = null);
        void StartOperation(string operationName, int totalSteps);
        void CompleteOperation();
    }

}