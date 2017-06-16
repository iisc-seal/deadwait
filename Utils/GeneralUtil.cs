using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Phx.IR;
using Phx.Types;
using Util;

using System.Runtime.Serialization;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;

namespace SafetyAnalysis.Util
{
    public class GeneralUtil
    {
        public static string ConvertToFilename(string str)
        {
            var newstr = String.Empty;
            var n = str.LastIndexOf("/");
            if(n != -1)
                str = str.Substring(0, n);
            foreach(var ch in str)
            {
                if (Char.IsLetterOrDigit(ch)
                    || ch == '.')
                    newstr += ch;
                else
                    newstr += "-";
            }
            if (String.IsNullOrEmpty(newstr))
                throw new NotSupportedException(str + " cannot  be converted to a filename");
            if (newstr.Length > 259)
                return newstr.Substring(0, 258);     
            return newstr;
        }

        public static string ConvertToFilename(Phx.FunctionUnit funit)
        {
            var typename = PhxUtil.GetTypeName(funit.FunctionSymbol.EnclosingAggregateType);
            var methodname = PhxUtil.GetFunctionName(funit.FunctionSymbol);
            var filename = GeneralUtil.ConvertToFilename(typename + "::" + methodname);
            filename = filename.Substring(filename.LastIndexOf('.') + 1);
            filename += funit.FunctionSymbol.ParameterSymbols.Count;
            return filename;
        }

        public static uint GetVariableId(Phx.IR.Operand operand)
        {
            return operand.IsTemporary ? operand.TemporaryId : operand.SymbolId;
        }

        public static List<InterestingVariable> RecordIfInteresting(CallInstruction inst)
        {
            uint lineNumber = inst.GetLineNumber();
            var fileName = inst.GetFileName();
            
            //if (inst.IsIndirectCall)
            //    Console.WriteLine("Processing Indirect: " + inst);
            //else
            //    Console.WriteLine("Processing: " + inst);

            //Console.WriteLine("CallTarget: " + inst.SourceOperand1.ToString());
            if (IsInteresting(inst.CallTargetOperand))
            {
                string funcname = inst.FunctionUnit.FunctionSymbol.QualifiedName;
                AggregateType receiverType = PhxUtil.GetNormalizedReceiver(inst);
                var rcvrOperand = inst.SourceOperand2;
                var rcvrType = PhxUtil.GetTypeName(receiverType);

                var s = inst.CallTargetOperand.ToString();

                InterestingVariable recvVar = new InterestingVariable(GetVariableId(rcvrOperand),
                        rcvrType, funcname, fileName, lineNumber);

                InterestingVariable retVar = null;

                List<InterestingVariable> l = new List<InterestingVariable>();

                if (s.Contains("AsyncTaskMethodBuilder") && s.Contains("get_Task"))
                {
                    var destinationOperand = inst.DestinationOperand1;
                    AggregateType destinationType = null;
                    if (destinationOperand.Type != null && destinationOperand.Type.AsAggregateType != null)
                        destinationType = PhxUtil.NormalizedAggregateType(destinationOperand.Type.AsAggregateType);
                    var destType = PhxUtil.GetTypeName(destinationType);
                    retVar = new InterestingVariable(GetVariableId(destinationOperand),
                        destType, funcname, fileName, lineNumber);
                    retVar.isReturnOfGetTask = true;
                    recvVar.isReceiverOfGetTask = true;
                }
                else if (s.Contains("AsyncTaskMethodBuilder") && s.Contains("SetResult"))
                    recvVar.isReceiverOfSetResult = true;
                else if (s.Contains("System.Threading.Tasks.Task::Wait"))
                    recvVar.isReceiverOfWait = true;
                else if (s.Contains("System.Threading.Tasks.Task") && s.Contains("get_Result"))
                    recvVar.isReceiverOfGetResult = true;
                else return l;

                l.Add(recvVar);
                if (retVar != null)
                    l.Add(retVar);              
                return l;
            }
            return null;
        }

        private static bool IsInteresting(Operand callTargetOperand)
        {
            if (callTargetOperand == null)
                return false;
            var s = callTargetOperand.ToString();
            return (s.Contains("AsyncTaskMethodBuilder") && s.Contains("SetResult"))
                || (s.Contains("AsyncTaskMethodBuilder") && s.Contains("get_Task"))
                || (s.Contains("System.Threading.Tasks.Task::Wait")) 
                || (s.Contains("System.Threading.Tasks.Task") && s.Contains("get_Result"));
        }       
    }
}
