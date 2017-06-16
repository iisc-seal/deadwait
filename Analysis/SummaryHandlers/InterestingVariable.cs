using Phx;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SafetyAnalysis.Util;
using SafetyAnalysis.TypeUtil;
using SafetyAnalysis.Purity;
using Phx.Types;
using Phx.IR;
using SafetyAnalysis.Framework.Graphs;
using System.Diagnostics;

namespace Util
{
    [Serializable]
    public class InterestingVariable
    {
        readonly public uint Name;
        readonly public string Type;
        readonly public FunctionUnit DeclaringFunction;
        readonly public string SourceFileName;
        readonly public uint LineNumber;
        public bool isReturnOfGetTask { get; set; }                 // true if variable was used to store the result of AsyncTaskMethodBuilder::get_Task
        public bool isReceiverOfSetResult { get; set; }             // true if variable was used call AsyncTaskMethodBuilder::SetResult
        public bool isReceiverOfGetTask { get; set; }               // true if variable was used call AsyncTaskMethodBuilder::get_Task
        public bool isReceiverOfWait { get; set; }                  // true if variable was used call Task::Wait
        public bool isReceiverOfGetResult { get; set; }             // true if variable was used call Task::get_Result

        public static MultiValueDictionary<HeapVertexBase, HeapVertexBase> pointsTo = new MultiValueDictionary<HeapVertexBase, HeapVertexBase>();

        public InterestingVariable(uint name, string type, FunctionUnit declaringFunctionUnit, string sourceFileName, uint lineNumber)
        {
            this.Name = name;
            this.Type = type;
            this.DeclaringFunction = declaringFunctionUnit;
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
            s.Append(Type + " " + Name + " in " + DeclaringFunction + " at " + SourceFileName + ":" + LineNumber);
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
        
        public PurityAnalysisData GetSummary(Dictionary<String, PurityAnalysisData> methodToPurityData)
        {
            return methodToPurityData[DeclaringFunction.FunctionSymbol.QualifiedName];
        }

        public void printPointsTo(
                Dictionary<String, PurityAnalysisData> methodToPurityData, 
                MultiValueDictionary<HeapVertexBase, HeapVertexBase> vertexMapping
            )
        {
            var summary = methodToPurityData[DeclaringFunction.FunctionSymbol.QualifiedName];
            var heapGraph = summary.OutHeapGraph;
            var interestingVertices = summary.OutHeapGraph.Vertices
                                        .Where(x => x is VariableHeapVertex)
                                        .Where(x => (x as VariableHeapVertex).index == this.Name)
                                        .Where(x => (x as VariableHeapVertex).functionName.CompareTo(this.DeclaringFunction.FunctionSymbol.QualifiedName) == 0);
            if (interestingVertices.Count() > 1)
            {
                Console.WriteLine(this);
                foreach (var v in interestingVertices)
                    Console.WriteLine(v);
                throw new System.Exception("More than one root vertex found!");
            }
            var rootVertex = interestingVertices.FirstOrDefault();

            if (isReturnOfGetTask)
            {

            }
            else if (this.isReceiverOfSetResult)
            {
                PrintPtsInfoForReceiverOfSetResult(heapGraph, rootVertex, vertexMapping);
            }
            else if (this.isReceiverOfGetTask)
            {
                PrintPtsInfoForReceiverOfGetResult(heapGraph, rootVertex, vertexMapping);
            }
            else if (this.isReceiverOfWait)
            {

            }
            else if (this.isReceiverOfGetResult)
            {
                PrintPtsInfoForReceiverOfGetResult(heapGraph, rootVertex, vertexMapping);
            }

        }

        private void PrintPtsInfoForReceiverOfGetResult(HeapGraphBase heapGraph, HeapVertexBase rootVertex, MultiValueDictionary<HeapVertexBase, HeapVertexBase> vertexMapping)
        {
            if (rootVertex == null)
                return;
            Console.WriteLine("Printing points to information for variable {0} in {1}", Name, DeclaringFunction);
            Console.WriteLine("Root Vertex is \n #{0} {1}", rootVertex.Id, rootVertex);
            var pointsTo = heapGraph.OutEdges(rootVertex).Select(x => x.Target);
            Console.WriteLine("Targets: ");
            foreach (var heapLocation in pointsTo)
            {
                var edges = heapGraph.OutEdges(heapLocation);
                var targets = edges.Where(e => e.Field is NamedField && (e.Field as NamedField).Name.Contains("m_task"));
                
                foreach (var t in targets)
                {
                    Console.WriteLine("#{0} [{1}] {2}", t.Source.Id, t.Source.GetType(), t.Source);
                    Console.WriteLine("#[{0}] {1}", t.GetType(), t.Field);
                    Console.WriteLine("#{0} [{1}] {2}", t.Target.Id, t.Target.GetType(), t.Target);
                    var mappedTo = vertexMapping[t.Target].Distinct();
                    foreach (var v in mappedTo)
                    {
                        InterestingVariable.pointsTo.Add(rootVertex, v);
                        Console.WriteLine("\t #{0} [{1}] {2}", v.Id, v.GetType(), v);
                    }
                }
            }            
        }

        private void PrintPtsInfoForReceiverOfSetResult(HeapGraphBase heapGraph, HeapVertexBase rootVertex, MultiValueDictionary<HeapVertexBase, HeapVertexBase> vertexMapping)
        {
            PrintPtsInfoForReceiverOfGetResult(heapGraph, rootVertex, vertexMapping);
        }

        public static void PrintAliasing()
        {
            Console.WriteLine("=========================================");
            var keyList = new List<HeapVertexBase>(pointsTo.Keys);
            for(int i = 0; i < keyList.Count; i++)
            {
                for(int j = i+1; j < keyList.Count; j++)
                {
                    Console.WriteLine("Comparing {0} with {1}", keyList[i], keyList[j]);
                    var common = pointsTo[keyList[i]].Intersect(pointsTo[keyList[j]]);
                    Console.WriteLine("Common:");
                    foreach(var v in common)
                    {
                        Console.WriteLine("\t #{0} [{1}] {2}", v.Id, v.GetType(), v);
                    }
                }
            }
        }

        public PurityAnalysisData GetSummary()
        {
            var funit = DeclaringFunction;
            var munit = funit.ParentPEModuleUnit;
            var enclType = PhxUtil.NormalizedAggregateType(funit.FunctionSymbol.EnclosingAggregateType);
            var decType = PhxUtil.GetTypeName(enclType);
            var th = CombinedTypeHierarchy.GetInstance(munit);
            var typeinfo = th.LookupTypeInfo(decType);
            var methodname = PhxUtil.GetFunctionName(funit.FunctionSymbol);
            var sig = PhxUtil.GetFunctionTypeSignature(funit.FunctionSymbol.FunctionType);
            var methodInfos = typeinfo.GetMethodInfos(methodname, sig);
            List<PurityAnalysisData> l = new List<PurityAnalysisData>();
            foreach(var minfo in methodInfos)
            {
                var summary = minfo.GetSummary();
                l.Add(summary);
            }
            return l.First();
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
                        rcvrType, inst.FunctionUnit, fileName, lineNumber);

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
                        destType, inst.FunctionUnit, fileName, lineNumber);
                    retVar.isReturnOfGetTask = true;
                    recvVar.isReceiverOfGetTask = true;
                }
                else if (s.Contains("AsyncTaskMethodBuilder") && s.Contains("SetResult"))
                    recvVar.isReceiverOfSetResult = true;
                else if (s.Contains("System.Threading.Tasks.Task::Wait"))
                    recvVar.isReceiverOfWait = true;
                else if ( 
                        (s.Contains("System.Threading.Tasks.Task") && s.Contains("get_Result")) 
                        || (s.Contains("Awaiter") && s.Contains("GetResult"))
                    )
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
                || (s.Contains("System.Threading.Tasks.Task") && s.Contains("get_Result")
                || (s.Contains("ConfiguredTaskAwaiter") && s.Contains("get_IsCompleted"))
                || (s.Contains("ConfiguredTaskAwaiter") && s.Contains("GetResult"))
                || (s.Contains("System.Runtime.CompilerServices.TaskAwaiter") && s.Contains("get_IsCompleted"))
                || (s.Contains("System.Runtime.CompilerServices.TaskAwaiter") && s.Contains("GetResult"))
                );
        }

        public static uint GetVariableId(Phx.IR.Operand operand)
        {
            return operand.IsTemporary ? operand.TemporaryId : operand.SymbolId;
        }

    }
}
