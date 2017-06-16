using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Util
{
    [Serializable]
    public class InterestingVariable
    {
        readonly public uint Name;
        readonly public string Type;
        readonly public string DeclaringFunctionName;
        readonly public string SourceFileName;
        readonly public uint LineNumber;
        public bool isReturnOfGetTask { get; set; }                 // true if variable was used to store the result of AsyncTaskMethodBuilder::get_Task
        public bool isReceiverOfSetResult { get; set; }             // true if variable was used call AsyncTaskMethodBuilder::SetResult
        public bool isReceiverOfGetTask { get; set; }               // true if variable was used call AsyncTaskMethodBuilder::get_Task
        public bool isReceiverOfWait { get; set; }                  // true if variable was used call Task::Wait
        public bool isReceiverOfGetResult { get; set; }             // true if variable was used call Task::get_Result

        public InterestingVariable(uint name, string type, string declaringFunctionName, string sourceFileName, uint lineNumber)
        {
            this.Name = name;
            this.Type = type;
            this.DeclaringFunctionName = declaringFunctionName;
            this.SourceFileName = sourceFileName;
            this.LineNumber = lineNumber;
            this.isReturnOfGetTask = false;
            this.isReceiverOfSetResult = false;
            this.isReceiverOfGetTask = false;
            this.isReceiverOfWait = false;
            this.isReceiverOfGetResult = false;
        }  
                
        override
        public string ToString()
        {
            StringBuilder s = new StringBuilder();
            s.Append(Type + " " + Name + " in " + DeclaringFunctionName + " at " + SourceFileName + ":" + LineNumber);
            if (isReturnOfGetTask)
                s.Append(" isReturnOfGetTask");
            else if (isReceiverOfSetResult)
                s.Append(" isReceiverOfSetResult");
            else if (isReturnOfGetTask)
                s.Append(" isReceiverOfGetTask");
            else if (isReceiverOfWait)
                s.Append(" isReceiverOfWait");
            else 
                s.Append(" isReceiverOfGetResult");
            return s.ToString();
        }
                  
    }
}
