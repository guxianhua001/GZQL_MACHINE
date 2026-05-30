using Core.Models;

namespace Core.Abstraction
{
    public interface IZScanGlobalVariableLinkService
    {
        bool LinkVariable(string variableName, GlobalVariableType expectedType);
        void UnlinkVariable();
        object GetLinkedValue();
        void WriteBackValue(object value);
        bool IsLinked { get; }
        string LinkedVariableName { get; }
    }
}
