using Phx;
using Phx.Graphs;
using Phx.IR;
using Phx.Phases;
using Phx.Symbols;
using QuickGraph;
using SafetyAnalysis.Util;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace SafetyAnalysis.Purity.ControlFlowAnalysisPhase
{
    
    using SMEdge = TaggedEdge<int, Pair<string, uint>>;
    public class StateMachineAnalysis
    {
        #region StaticMembers

       /* 
        * The map to hold state machine transitions follows the paper: 
        * QuickGraph's tagged edge is TaggedEdge<TVertex, TTag> - 
        * our statemachine vertices are ints, and the edges are tagged with
        * (string s, uint v) - here, s says whether the awaiter is configured,
        * and v holds the task variable id for the task whose completion can
        * result in this transition
        */
        public static Dictionary<string, AdjacencyGraph<int, SMEdge>> transitions
            = new Dictionary<string, AdjacencyGraph<int, SMEdge>>();

        // Maps call instruction ids to the set of states they belong to
        public static Dictionary<string, Dictionary<uint, HashSet<int>>> callIdToState = new Dictionary<string, Dictionary<uint, HashSet<int>>>();
        
        public static string outputdir = PurityAnalysisPhase.outputdir;

        // Record the final states corresponding to every MoveNext method
        public static Dictionary<string, HashSet<int>> finalStates = new Dictionary<string, HashSet<int>>();

        #endregion

        public void Analyze(Phx.Unit unit)
        {
            if (!unit.IsFunctionUnit)
            {
                return;
            }

            Phx.FunctionUnit functionUnit = unit.AsFunctionUnit;
            Phx.PEModuleUnit moduleUnit = unit.ParentPEModuleUnit;


            // This pass will only analyze compiler generated state machines corresponding to async methods
            if (!AsyncUtil.IsAsyncStateMachine(functionUnit))
                return;

            // call if earlier passes have not done the pre processing
            //PreprocessFunctionUnit(functionUnit, moduleUnit);

            functionUnit.BuildFlowGraphWithoutEH();
            if (PurityAnalysisPhase.DumpFlowGraphs)
            {
                DumpFlowGraphAsDGML(functionUnit);
            }
            var awaitCalls = AsyncUtil.GetAwaitCalls(functionUnit);
            PrintDebugInformation(functionUnit, awaitCalls);
            if (awaitCalls.Count == 0)
            {
                Console.WriteLine("Method {0} lacks awaits, skipping", functionUnit.FunctionSymbol.QualifiedName);
                return;
            }

            HashSet<Pair<uint, uint>> syncEdges;        // Edges representing synchronous control when (IsCompleted) on awaiters holds            
            HashSet<uint> continuationBB;               // Basic blocks registering continuations

            // Map from blocks calling (IsCompleted) on awaiters to a pair<str, uint> representing
            // First: the kind of awaiter: "d" for configured awaiters, and "p" otherwise
            // Second: the variable id corresponding to the awaiter local on which .IsCompleted was invoked
            Dictionary<uint, Pair<string, uint>> edgeLabels;

            // Process each await to find async successors
            AsyncUtil.ProcessAwaitBlocks(functionUnit, out syncEdges, out edgeLabels, out continuationBB);

            // Compute path conditions involving the state machine state variable
            // the conditions are reprerented as a map from the target branch [of the condition] to a predicate on integers
            // representing the condition
            Dictionary<Pair<uint, uint>, Predicate<int>> predMap = ComputePredicates(functionUnit);

            // the Phoenix CFG does not qualify edges leaving a finally block
            // this map helps recover that information by looking at the 
            // continuations associated with ExitTryWithFinallyBlocks
            /*
            Before reading this, look up the topic "Intermediate Representation 
            for Exception Handling" in the Phoenix docs.
            Multiple states in the state machine could have the same "finally" block
            but different code after exiting the finally.
            
            try{

            } --> Exit try with finally
            finally {

            } --> Exit finally
            --> Code after the finally, Phoenix calls this "continuation" [see 
            reference indicated above]

            The code to execute after exiting the finally is encoded into the
            EXITTRYWITHFINALLY instruction, rather than the EXITFINALLY instruction
            (ie on exiting the "try" block, not the "finally" block)
            In particular, the BB that ends with EXITFINALLY has successor edges
            to *all* continuations
            The right thing to do in our setting is: the entry fact of a successor s
            of the EXITFINALLY basic block should be the exit fact at the end of 
            the basic block with the "EXITTRYWITHFINALLY" instruction, that stored s
            as a continuation

            We use the targetContinuation variable to map the successor s of an
            EXITFINALLY block to the EXITTRYWITHFINALLY block s is a continuation of.

            */
            var targetContinuation = AsyncUtil.ProcessExitTryWithFinallyBlocks(functionUnit);

            // perform the actual data flow analysis, and populate data structures using the results
            // dfFacts maps every basic block to the set of states that may execute it
            var dfFacts = AsyncUtil.TagBasicBlocks(functionUnit, awaitCalls.Count + 1, predMap, syncEdges, targetContinuation);            
            finalStates[functionUnit.FunctionSymbol.QualifiedName] = AsyncUtil.getFinalStates(AsyncUtil.GetBlocksCallingSetResult(functionUnit), dfFacts);
            
            AdjacencyGraph<int, TaggedEdge<int, Pair<string, uint>>> stateMachineGraph = ComputeStateMachine(syncEdges, edgeLabels, dfFacts);            
            transitions[functionUnit.FunctionSymbol.QualifiedName] = stateMachineGraph;            
            Dictionary<uint, HashSet<int>> callToStateMap = ComputeCallToStateMap(functionUnit, dfFacts);
            callIdToState[functionUnit.FunctionSymbol.QualifiedName] = callToStateMap;

            // print, if required
            if (PurityAnalysisPhase.DumpStateMachineGraphs)
                WriteStateMachine(functionUnit, stateMachineGraph);
            if (PurityAnalysisPhase.EnableLogging)
            {
                Console.WriteLine("Printing data flow facts for " + functionUnit.FunctionSymbol.QualifiedName);
                PrintDataFlowFacts(dfFacts);
            }

            // cleanup
            functionUnit.DeleteFlowGraph();
        }

        private Dictionary<Pair<uint, uint>, Predicate<int>> ComputePredicates(FunctionUnit functionUnit)
        {
            var switchPredMap = AsyncUtil.GetSwitchPredicates(functionUnit);    // conditions arising out of switch statements
            var ifPredMap = AsyncUtil.GetEntryPredicates(functionUnit);         // conditions arising out of if statements
            if (PurityAnalysisPhase.EnableLogging)
                PrintPredicates(switchPredMap, ifPredMap);
            var predMap = switchPredMap.Concat(ifPredMap).ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Key);   // union the conditions
            return predMap;
        }

        private void PreprocessFunctionUnit(FunctionUnit functionUnit, PEModuleUnit moduleUnit)
        {
            if (!functionUnit.IRState.Equals(Phx.FunctionUnit.HighLevelIRFunctionUnitState))
                moduleUnit.Raise(functionUnit.FunctionSymbol, Phx.FunctionUnit.HighLevelIRFunctionUnitState);
            AddInstructionIds(functionUnit);
        }
       
        private void PrintPredicates(Dictionary<Pair<uint, uint>, Pair<Predicate<int>, string>> switchPredMap, Dictionary<Pair<uint, uint>, Pair<Predicate<int>, string>> ifPredMap)
        {
            var pred = switchPredMap.Concat(ifPredMap).ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Value);
            foreach (var kv in pred)
            {
                Debug.WriteLine(kv.Key + "-->" + kv.Value);
            }
        }

        private static void DumpFlowGraphAsDGML(FunctionUnit functionUnit)
        {
            var fg = functionUnit.FlowGraph;

            var graph = new BidirectionalGraph<Pair<uint, string>, Edge<Pair<uint, string>>>();

            HashSet<Pair<uint, string>> toShort = new HashSet<Pair<uint, string>>();

            Dictionary<uint, Pair<uint, string>> nodes = new Dictionary<uint, Pair<uint, string>>();

            foreach (var bb in fg.BasicBlocks)
            {
                StringBuilder s = new StringBuilder();
                s.Append(bb.Id + "\n");
                foreach (var instr in bb.Instructions)
                {
                    //if (instr is CallInstruction || instr.IsBranchInstruction || instr.IsLabelInstruction)
                    s.Append(instr.InstructionId + " " + instr + " [" + instr.GetType() + "]\n");

                    //if(instr is LabelInstruction)
                    //{
                    //    var li = instr.AsLabelInstruction;
                    //    s.Append("Label opcode " + li.Opcode + "\t");                        
                    //}

                    if (instr is BranchInstruction)
                    {
                        DumpBranchInstruction(s, instr);
                    }

                    if (instr is SwitchInstruction)
                    {
                        DumpSwitchInstruction(s, instr);
                    }

                    nodes[bb.Id] = new Pair<uint, string>(bb.Id, WordWrap(s.ToString(), 120));
                }
            }

            foreach (var bb in fg.BasicBlocks)
            {
                foreach (var succ in bb.SuccessorEdges)
                {
                    graph.AddVerticesAndEdge(new Edge<Pair<uint, string>>(nodes[bb.Id], nodes[succ.SuccessorNode.Id]));
                }
            }


            string filename = outputdir + ConvertToFilename(functionUnit) + ".fg.dgml";
            GraphUtil.DumpAsDGML<Pair<uint, string>, Edge<Pair<uint, string>>>(
                            filename,
                            graph,
                            node => (int)node.Key,
                            node => node.Value,
                            null, null, null
                            );
        }

        private static void DumpSwitchInstruction(StringBuilder s, Instruction instr)
        {
            var sInstr = instr as SwitchInstruction;
            s.Append(sInstr + "\n");
            foreach (var operand in sInstr.SourceOperands)
                s.Append(operand + "\t");
        }

        private static void DumpBranchInstruction(StringBuilder s, Instruction instr)
        {
            var bi = instr.AsBranchInstruction;
            if (bi.IsConditional)
            {
                s.Append(bi.ConditionCode.ToString() + "\t" + bi.TrueLabelInstruction + "\t" + bi.FalseLabelInstruction);
                s.Append("\n Opcode: " + bi.Opcode + "\t" + bi.Opcode.IsHir);
                s.Append("\n ConditionCode: " + PhxUtil.ConditionCodeToString(bi.ConditionCode));

                s.Append("\n Source Operands: ");
                foreach (var operand in bi.SourceOperands)
                {
                    s.Append(operand + "\t");
                    s.Append(operand.FindExactDefinitionBackwardBlock(bi) + "\n");
                }
                s.Append("\n Destination Operands: ");
                foreach (var operand in bi.DestinationOperands)
                {
                    s.Append(operand + "\t");

                }
            }
        }

        private void AddInstructionIds(Phx.FunctionUnit functionUnit)
        {
            uint id = 1;
            foreach (var inst in functionUnit.Instructions)
                inst.InstructionId = id++;
        }

        private static Dictionary<uint, HashSet<int>> ComputeCallToStateMap(FunctionUnit functionUnit, Dictionary<uint, HashSet<int>> dfFacts)
        {
            var callToStateMap = new Dictionary<uint, HashSet<int>>();
            foreach (var instr in functionUnit.Instructions)
            {
                if (instr.IsCallInstruction || instr.Opcode.Equals(Phx.Common.Opcode.InitObj))
                {
                    callToStateMap[instr.InstructionId] = dfFacts[instr.BasicBlock.Id];
                    if (instr.Opcode.Equals(Phx.Common.Opcode.InitObj))
                    {
                        Debug.WriteLine("INITOBJ: " + instr.GetType());
                    }
                }
            }

            return callToStateMap;
        }

        private static AdjacencyGraph<int, TaggedEdge<int, Pair<string, uint>>> ComputeStateMachine(HashSet<Pair<uint, uint>> syncEdges, Dictionary<uint, Pair<string, uint>> edgeLabels, Dictionary<uint, HashSet<int>> dfFacts)
        {
            var stateMachineGraph = new AdjacencyGraph<int, TaggedEdge<int, Pair<string, uint>>>();
            foreach (var edge in syncEdges)
            {
                var src = edge.Key;
                var target = edge.Value;
                var srcFacts = dfFacts[src];
                var targetFacts = dfFacts[target];
                foreach (var srcfact in srcFacts)
                    foreach (var targetfact in targetFacts)
                    {
                        TaggedEdge<int, Pair<string, uint>> e = new TaggedEdge<int, Pair<string, uint>>(srcfact, targetfact, edgeLabels[src]);
                        stateMachineGraph.AddVerticesAndEdge(e);
                    }
            }

            return stateMachineGraph;
        }

        private static void PrintDebugInformation(FunctionUnit functionUnit, List<CallInstruction> awaitCalls)
        {
            if (PurityAnalysisPhase.EnableLogging)
            {
                Console.WriteLine("Analyzing " + functionUnit.FunctionSymbol.QualifiedName);
                var dispatchMethod = AsyncUtil.IdentifyDispatchMethod(functionUnit);
                Console.WriteLine("DispatchMethod {0}", dispatchMethod);

                Console.WriteLine("Estimated # states {0}", awaitCalls.Count + 1);
                AsyncUtil.PrintDfsInformation(functionUnit);
                var successorMap = AsyncUtil.TagSuccessorState(functionUnit);
                Console.WriteLine("Successor Map: ");
                foreach (var kv in successorMap)
                {
                    Console.WriteLine(kv.Key + "-->" + kv.Value);
                }
            }
        }

        private static void WriteStateMachine(FunctionUnit functionUnit, AdjacencyGraph<int, TaggedEdge<int, Pair<string, uint>>> stateMachineGraph)
        {
            string f = outputdir + ConvertToFilename(functionUnit) + ".sm.dgml";
            GraphUtil.DumpAsDGML<int, TaggedEdge<int, Pair<string, uint>>>(
                f,
                stateMachineGraph,
                node => node,
                node => node.ToString() + (finalStates[functionUnit.FunctionSymbol.QualifiedName].Contains(node) ? " f" : ""),
                edge => edge.Tag.ToString(), null, null
                );
        }

        private static void PrintDataFlowFacts(Dictionary<uint, HashSet<int>> dfFacts)
        {
            foreach (var kv in dfFacts)
            {
                Console.Write(kv.Key + " ---> { ");
                foreach (var tag in kv.Value)
                {
                    Console.Write(tag + ",");
                }
                Console.WriteLine("}\n");
            }
        }

        public static string ConvertToFilename(string str)
        {
            var newstr = String.Empty;
            foreach (var ch in str)
            {
                if (Char.IsLetterOrDigit(ch)
                    || ch == '.')
                    newstr += ch;
                else
                    newstr += "-";
            }
            if (String.IsNullOrEmpty(newstr))
                throw new NotSupportedException(str + " cannot  be converted to a filename");
            return newstr;
        }

        public static string ConvertToFilename(FunctionUnit funit)
        {
            var typename = PhxUtil.GetTypeName(funit.FunctionSymbol.EnclosingAggregateType);
            var methodname = PhxUtil.GetFunctionName(funit.FunctionSymbol);
            var filename = ConvertToFilename(typename + "::" + methodname);
            filename = filename.Substring(filename.LastIndexOf('.') + 1);
            filename += funit.FunctionSymbol.ParameterSymbols.Count;
            return filename;
        }



        protected const string _newline = "\r\n";
        

        public static string WordWrap(string the_string, int width)
        {
            int pos, next;
            StringBuilder sb = new StringBuilder();

            // Lucidity check
            if (width < 1)
                return the_string;

            // Parse each line of text
            for (pos = 0; pos < the_string.Length; pos = next)
            {
                // Find end of line
                int eol = the_string.IndexOf(_newline, pos);

                if (eol == -1)
                    next = eol = the_string.Length;
                else
                    next = eol + _newline.Length;

                // Copy this line of text, breaking into smaller lines as needed
                if (eol > pos)
                {
                    do
                    {
                        int len = eol - pos;

                        if (len > width)
                            len = BreakLine(the_string, pos, width);

                        sb.Append(the_string, pos, len);
                        sb.Append(_newline);

                        // Trim whitespace following break
                        pos += len;

                        while (pos < eol && Char.IsWhiteSpace(the_string[pos]))
                            pos++;

                    } while (eol > pos);
                }
                else sb.Append(_newline); // Empty line
            }

            return sb.ToString();
        }

        /// <summary>
        /// Locates position to break the given line so as to avoid
        /// breaking words.
        /// </summary>
        /// <param name="text">String that contains line of text</param>
        /// <param name="pos">Index where line of text starts</param>
        /// <param name="max">Maximum line length</param>
        /// <returns>The modified line length</returns>
        public static int BreakLine(string text, int pos, int max)
        {
            // Find last whitespace in line
            int i = max - 1;
            while (i >= 0 && !Char.IsWhiteSpace(text[pos + i]))
                i--;
            if (i < 0)
                return max; // No whitespace found; break at maximum length
                            // Find start of whitespace
            while (i >= 0 && Char.IsWhiteSpace(text[pos + i]))
                i--;
            // Return length of text before whitespace
            return i + 1;
        }
    }
}

