using Phx;
using Phx.Graphs;
using Phx.IR;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.CodeDom;
using System.CodeDom.Compiler;
using SafetyAnalysis.Util;

namespace SafetyAnalysis.Purity
{
    public enum DispatchMethod { If, Switch };

    public class AsyncUtil
    {   
        /*
         * Try and divine the kind of state machine generated for the async fUnit
         * passed in - the C# 5.0 compiler uses typically uses if statements 
         * to control state transfers for machines with < 3 states, and switch
         * case blocks otherwise
         */     
        public static DispatchMethod IdentifyDispatchMethod(FunctionUnit fUnit)
        {            
            var enterTryInstrs = new List<Instruction>();
            foreach (var instr in fUnit.Instructions)
            {
                if (instr.Opcode.Equals(Phx.Common.Opcode.EnterTry))
                    enterTryInstrs.Add(instr);
            }
            if (!enterTryInstrs.Any())
                throw new System.ArgumentException("Are you sure you passed in an async method?");
            var firstEnterTry = enterTryInstrs.First();
            var bb = firstEnterTry.BasicBlock;

            if (bb.LastInstruction is SwitchInstruction)
                return DispatchMethod.Switch;

            Trace.Assert(bb.LastInstruction is BranchInstruction, "Unknown dispatch mode");
            return DispatchMethod.If;
        }

        /* Return the call instructions calling the compiler generated 
         * AwaitUnsafeOnCompleted - this method registers a continuation
         * on the awaiter associated with a task. 
         */
        public static List<CallInstruction> GetAwaitCalls(FunctionUnit fUnit)
        {
            List<CallInstruction> awaitCalls = new List<CallInstruction>();
            foreach (var instr in fUnit.Instructions)
            {
                if (instr is CallInstruction)
                {                    
                    var cI = instr.AsCallInstruction;
                    if (cI.IsDirectCall)
                    {
                        //Debug.WriteLine(instr);
                        if (cI.CallTargetOperand.ToString().Contains("AwaitUnsafeOnCompleted"))
                            awaitCalls.Add(cI);
                    }
                }
            }
            return awaitCalls;
        }

        /*
         * Return the basic block ids for blocks calling the SetResult signaling
         * procedure - these are final states 
         */
        public static HashSet<uint> GetBlocksCallingSetResult(FunctionUnit fUnit)
        {
            HashSet<uint> blocksCallingSetResult = new HashSet<uint>();
            foreach (var instr in fUnit.Instructions)
            {
                if (instr is CallInstruction)
                {
                    //Debug.WriteLine(instr.Opcode);
                    var cI = instr.AsCallInstruction;
                    if (cI.IsDirectCall)
                    {

                        if (cI.CallTargetOperand.ToString().Contains("SetResult") && cI.CallTargetOperand.ToString().Contains("MethodBuilder"))
                            blocksCallingSetResult.Add(instr.BasicBlock.Id);
                    }
                }
            }
            return blocksCallingSetResult;
        }

        /*
         * A data flow fact reaching any basic block calling SetResult denotes
         * a final state
         */
        public static HashSet<int> getFinalStates(HashSet<uint> blocksCallingSetResult, 
            Dictionary<uint, HashSet<int>> dataFlowFacts)
        {
            HashSet<int> finalStates = new HashSet<int>();
            foreach (var s in blocksCallingSetResult)
            {
                Console.WriteLine("Block {0} calls SetResult", s);
                finalStates.UnionWith(dataFlowFacts[s]);
            }
            return finalStates;
        }

        /*
         * Returns a map from a basic block successor pair to a pair comprising
         * the predicate guarding the transition and its string representation
         * provided the transition is via a switch on "Local0", the 'state'
         * variable of the state machine
         */
        public static Dictionary<Pair<uint, uint>, Pair<Predicate<int>, String>> 
            GetSwitchPredicates(FunctionUnit fUnit)
        {
            var switchPredicate = new Dictionary<Pair<uint, uint>, Pair<Predicate<int>, String>>();
            foreach (var bb in fUnit.FlowGraph.BasicBlocks)
            {
                // skip basic blocks whose exit is not via a switch
                if (!bb.LastInstruction.IsSwitchInstruction)
                    continue;

                var switchInstr = bb.LastInstruction as SwitchInstruction;

                var op = switchInstr.SourceOperand;
                var defInstr = op.FindExactDefinitionBackwardBlock(switchInstr);

                // skip the block if the switch is not on the 'state' variable
                if (defInstr.SourceOperand.ToString().CompareTo("Local0") != 0)
                    continue;

                var currId = bb.Id;
                long lb = switchInstr.GetLowerBound();
                long ub = switchInstr.GetUpperBound();

                if(PurityAnalysisPhase.EnableConsoleLogging)
                    Console.WriteLine(switchInstr + " has " + (ub - lb + 2) + " cases");

                var defaultTarget = switchInstr.DefaultLabelInstruction.AsLabelInstruction;
                var defaultLabel = defaultTarget.DestinationOperand.LabelId;
                Predicate<int> defaultPredicate = (x => x == -1);
                var defaultKey = new Pair<uint, uint>(currId, defaultTarget.BasicBlock.Id);

                // handle the defualt case
                switchPredicate[defaultKey] = new Pair<Predicate<int>, string>(defaultPredicate, "(x => x == -1)");
                long i = lb - 1;
                foreach (var operand in switchInstr.SourceOperands)
                {
                    if (operand.IsLabelOperand && operand.LabelId != defaultLabel)
                    {
                        var target = fUnit.FlowGraph.FindContainingBlock(operand.AsLabelOperand).Id;
                        int copy = (int)i;
                        Predicate<int> pred = (x => x == copy);
                        var key = new Pair<uint, uint>(currId, target);
                        // the target could be reached for multiple cases
                        if (switchPredicate.ContainsKey(key))
                        {
                            var currentPredPair = switchPredicate[key];
                            Predicate<int> updatedPred = (x => currentPredPair.Key(x) || pred(x));
                            string s = String.Format("(x => x == {0})", copy) + " || " + currentPredPair.Value;
                            switchPredicate[key] = new Pair<Predicate<int>, string>(updatedPred, s);
                        }
                        else
                            switchPredicate[key] = new Pair<Predicate<int>, string>(pred, String.Format("(x => x == {0})", copy));
                        i++;
                    }
                }
            }
            return switchPredicate;
        }

        /*
         * Returns a map from a basic block successor pair to a pair comprising
         * the predicate guarding the transition and its string representation
         * provided the transition is via an if condition on "Local0", the 'state'
         * variable of the state machine. This is a set of hacks because of all
         * the weird corner cases the C# 5.0 compiler uses in its async transform
         */
        public static Dictionary<Pair<uint, uint>, Pair<Predicate<int>, String>> GetEntryPredicates(FunctionUnit fUnit)
        {            
            var entryPredicate = new Dictionary<Pair<uint, uint>, Pair<Predicate<int>, String>>();
            
            foreach (var bb in fUnit.FlowGraph.BasicBlocks)
            {
                if (!(bb.LastInstruction is BranchInstruction))
                    continue;

                if (!bb.LastInstruction.AsBranchInstruction.IsConditional)
                    continue;

                var bInstr = bb.LastInstruction as BranchInstruction;

                var conditionCode = bInstr.ConditionCode;
                var sourceOperand = bInstr.SourceOperand;
                var currId = bInstr.BasicBlock.Id;
                var trueTarget = bInstr.TrueLabelInstruction.BasicBlock.Id;
                var falseTarget = bInstr.FalseLabelInstruction.BasicBlock.Id;

                var sourceDefInstruction = sourceOperand.FindExactDefinitionBackwardBlock(bInstr);
                Pair<Predicate<int>, string> predPair = null;
                if (sourceDefInstruction is CompareInstruction)
                {
                    var defCompareInstruction = sourceDefInstruction.AsCompareInstruction;
                    var compareConditionCode = defCompareInstruction.ConditionCode;

                    var op1DefInstr = defCompareInstruction.SourceOperand1.FindExactDefinitionBackwardBlock(defCompareInstruction);

                    Operand constOperand = null;

                    if (defCompareInstruction.SourceOperand2.IsImmediateOperand)
                    {
                        constOperand = defCompareInstruction.SourceOperand2;
                    }
                    else
                    {
                        var op2DefInstr = defCompareInstruction.SourceOperand2.FindExactDefinitionBackwardBlock(defCompareInstruction);
                        constOperand = op2DefInstr.SourceOperand;
                    }

                    if (op1DefInstr == null || op1DefInstr.SourceOperand == null || !op1DefInstr.SourceOperand.ToString().Contains("Local0"))
                        continue;

                    var smOperand = op1DefInstr.SourceOperand;
                    
                    Trace.Assert(!constOperand.ToString().Contains("Local0"), "Unexpected order of compare operands");

                    var constant = constOperand.AsImmediateOperand.Value.AsIntValue.Int32;
                    predPair = GetPredicateFrom(compareConditionCode, constant, conditionCode);
                }
                else
                {
                    if (!(sourceDefInstruction is ValueInstruction && sourceDefInstruction.Opcode.Equals(Phx.Common.Opcode.Assign)))
                    {
                        if(PurityAnalysisPhase.EnableLogging)
                            Console.WriteLine("Missed tracking if branch {0} " +
                                "is on the state machine state variable", bInstr);
                    }

                    //var smOperand = sourceDefInstruction.SourceOperand;
                    if (sourceDefInstruction == null || sourceDefInstruction.SourceOperand == null || !sourceDefInstruction.SourceOperand.ToString().Contains("Local0"))
                        continue;
                    predPair = GetPredicateFrom(conditionCode);
                }

                var p = (Predicate<int>)predPair.Key.Clone();
                var trueKey = new Pair<uint, uint>(currId, trueTarget);
                if (entryPredicate.Keys.Contains(trueKey))
                    throw new System.Exception("Multiple edges from branch instr with same target");
                entryPredicate[trueKey] = new Pair<Predicate<int>, string>(p, predPair.Value);

                p = (Predicate<int>)predPair.Key.Clone();
                var falseKey = new Pair<uint, uint>(currId, falseTarget);
                if (entryPredicate.ContainsKey(falseKey))
                    throw new System.Exception("Multiple edges from branch instr with same target");
                entryPredicate[falseKey] = new Pair<Predicate<int>, string>((x => !p(x)), "!" + predPair.Value);
            }
            return entryPredicate;
        }

        private static Pair<Predicate<int>, string> GetPredicateFrom(int conditionCode)
        {
            if (conditionCode.Equals(ConditionCode.False))
            {                
                return new Pair<Predicate<int>, string>((x => x == 0), "(x => x == 0)");
            }
            return new Pair<Predicate<int>, string>((x => x != 0), "(x => x != 0)");
        }

        private static Pair<Predicate<int>, string> GetPredicateFrom(int compareConditionCode, int constant, int branchConditionCode)
        {
            bool compareWith = branchConditionCode.Equals(ConditionCode.True) ? true : false;
            Predicate<int> pred = null;
            string oprator;
            switch (compareConditionCode)
            {
                case ConditionCode.EQ:
                    if (compareWith)
                    {
                        pred = (x => (x == constant)); oprator = "==";
                    }
                    else {
                        pred = (x => (x != constant)); oprator = "!=";
                    }
                    break;
                case ConditionCode.NE:
                    if (compareWith)
                    {
                        pred = (x => (x != constant)); oprator = "!=";
                    }
                    else {
                        pred = (x => (x == constant)); oprator = "==";
                    }
                    break;
                case ConditionCode.ULT:
                case ConditionCode.LT:
                    if (compareWith)
                    {
                        pred = (x => (x < constant)); oprator = "<";
                    }
                    else {
                        pred = (x => (x >= constant)); oprator = ">=";
                    }
                    break;
                case ConditionCode.ULE:
                case ConditionCode.LE:
                    if (compareWith)
                    {
                        pred = (x => (x <= constant)); oprator = "<=";
                    }
                    else {
                        pred = (x => (x > constant)); oprator = ">";
                    }
                    break;
                case ConditionCode.UGT:
                case ConditionCode.GT:
                    if (compareWith)
                    {
                        pred = (x => (x > constant)); oprator = ">";
                    }
                    else {
                        pred = (x => (x <= constant)); oprator = "<=";
                    }
                    break;
                case ConditionCode.UGE:
                case ConditionCode.GE:
                    if (compareWith)
                    {
                        pred = (x => (x >= constant)); oprator = ">=";
                    }
                    else {
                        pred = (x => (x < constant)); oprator = "<";
                    }
                    break;

                default: throw new NotImplementedException("Unknown condition code");
            }
            return new Pair<Predicate<int>, string>(pred, String.Format("x => (x {0} {1})", oprator, constant));
        }

        /*
         * Pattern match on IR generated by the compiler to obtain the successor
         * state set before an await call - tag the successor basic block
         * with the valuation given to the 'state' variable         
         */ 
        public static Dictionary<uint, int> TagSuccessorState(FunctionUnit fUnit)
        {
            var awaitCalls = GetAwaitCalls(fUnit);
            var successorMap = new Dictionary<uint, int>();
            foreach (var call in awaitCalls)
            {
                int successorStateNumber = GetSuccessorStateNumber(call);
                var awaitBB = call.BasicBlock;
                Trace.Assert(awaitBB.PredecessorCount == 1, "Multiple predecessors for an await block!");
                var fastPathConditionBB = awaitBB.UniquePredecessorBlock;
                Trace.Assert(fastPathConditionBB.LastInstruction is BranchInstruction, "Unexpected control flow!");
                var bi = fastPathConditionBB.LastInstruction as BranchInstruction;
                var falseBranch = bi.FalseLabelInstruction.BasicBlock.Id;
                successorMap[falseBranch] = successorStateNumber;
            }
            return successorMap;
        }

        /*
         * Obtain the edges representing sychronous transfer of control in case
         * the awaiter is already complete           
         */
        public static HashSet<Pair<uint, uint>> SyncEdges(FunctionUnit fUnit)
        {
            var awaitCalls = GetAwaitCalls(fUnit);
            var syncEdges = new HashSet<Pair<uint, uint>>();
            foreach (var call in awaitCalls)
            {
                var awaitBB = call.BasicBlock;
                Trace.Assert(awaitBB.PredecessorCount == 1, "Multiple predecessors for an await block!");
                var fastPathConditionBB = awaitBB.UniquePredecessorBlock;
                Trace.Assert(fastPathConditionBB.LastInstruction is BranchInstruction, "Unexpected control flow!");
                var bi = fastPathConditionBB.LastInstruction as BranchInstruction;
                var falseBranch = bi.FalseLabelInstruction.BasicBlock.Id;
                syncEdges.Add(new Pair<uint, uint>(awaitBB.Id, falseBranch));
            }
            return syncEdges;
        }

        public static HashSet<BasicBlock> GetExitFinallyBlocks(FunctionUnit fUnit)
        {
            var l = new HashSet<BasicBlock>();
            foreach (var bb in fUnit.FlowGraph.BasicBlocks)
            {
                var lastInstr = bb.LastInstruction;
                if (lastInstr.Opcode.Equals(Phx.Common.Opcode.ExitFinally))
                    l.Add(bb);
            }
            return l;
        }

        public static Dictionary<uint, uint> ProcessExitTryWithFinallyBlocks(FunctionUnit fUnit)
        {
            Dictionary<uint, uint> trackContinuation = new Dictionary<uint, uint>();
            foreach (var bb in fUnit.FlowGraph.BasicBlocks)
            {
                if (!bb.LastInstruction.IsBranchInstruction)
                    continue;
                var bInstr = bb.LastInstruction.AsBranchInstruction;
                if (!bInstr.Opcode.Equals(Phx.Common.Opcode.ExitTryWithFinallyCall))
                    continue;
                foreach (var operand in bInstr.SourceOperands)
                {
                    if (operand.IsLabelOperand)
                    {
                        var lop = operand.AsLabelOperand;
                        if (lop.IsContinuationReference)
                        {
                            trackContinuation[fUnit.FlowGraph.FindContainingBlock(lop).Id] = bb.Id;
                        }
                    }
                }
            }
            return trackContinuation;
        }

        /*
         * Record for each await:
         * 1. the synchronous successor
         * 2. the async successor, and
         * 2. the state corresponding to the asynchronous successor
         */ 
        public static void ProcessAwaitBlocks(FunctionUnit fUnit,
            out HashSet<Pair<uint, uint>> syncEdges,
            out Dictionary<uint, Pair<string, uint>> edgeLabels,
            out HashSet<uint> continuationBB)
        {
            var awaitCalls = GetAwaitCalls(fUnit);
            //successorState = new Dictionary<uint, int>();
            edgeLabels = new Dictionary<uint, Pair<string, uint>>();
            syncEdges = new HashSet<Pair<uint, uint>>();
            continuationBB = new HashSet<uint>();

            foreach (var call in awaitCalls)
            {
                int successorStateNumber = GetSuccessorStateNumber(call);
                var awaitBB = call.BasicBlock;
                Trace.Assert(awaitBB.PredecessorCount == 1, "Multiple predecessors for an await block!");
                var fastPathConditionBB = awaitBB.UniquePredecessorBlock;
                Trace.Assert(fastPathConditionBB.LastInstruction is BranchInstruction, "Unexpected control flow!");
                var bi = fastPathConditionBB.LastInstruction as BranchInstruction;
                var trueBlockID = bi.TrueLabelInstruction.BasicBlock.Id;
                var trueBlock = fUnit.FlowGraph.Block(trueBlockID);
                var targetOfGetResult = trueBlock.FirstInstruction.Next.DestinationOperand;

                //uint awaitCallInstructionId = 0;
                Operand configuredTaskVariable = null;
                Operand taskVariable = null;
                var local = trueBlock.FirstInstruction.Next.SourceOperand;
                foreach (var instr in fastPathConditionBB.Instructions)
                {
                    if (instr is ValueInstruction && instr.Opcode.Equals(Phx.Common.Opcode.Assign))
                    {
                        var assignInstr = instr.AsValueInstruction;
                        var lhs = instr.DestinationOperand;
                        if (local.ToString().CompareTo("&" + lhs.ToString()) == 0)
                        {
                            var awaiter = instr.SourceOperand;
                            var defInstr = awaiter.FindExactDefinitionBackwardBlock(assignInstr);
                            Trace.Assert(defInstr is CallInstruction);
                            var cI = defInstr.AsCallInstruction;
                            Trace.Assert(cI.CallTargetOperand.ToString().Contains("GetAwaiter"));                            
                            taskVariable = cI.SourceOperand2;                            
                            break;
                        }
                    }
                    if (instr is CallInstruction)
                    {
                        var cI = instr.AsCallInstruction;
                        if (cI.CallTargetOperand.ToString().Contains("ConfigureAwait"))
                        {
                            configuredTaskVariable = cI.SourceOperand2;
                        } 

                    }
                }

                //successorState[trueBranch] = successorStateNumber;
                syncEdges.Add(new Pair<uint, uint>(fastPathConditionBB.Id, trueBlockID));
                continuationBB.Add(awaitBB.Id);
                foreach (var instr in fastPathConditionBB.Instructions)
                {
                    if (instr.IsCallInstruction)
                    {
                        var cI = instr.AsCallInstruction;
                        if (cI.CallTargetOperand.ToString().Contains("get_IsCompleted"))
                        {
                            var type = PhxUtil.GetNormalizedReceiver(cI);
                            var typeName = PhxUtil.GetTypeName(type);
                            // uint id = awaitCallInstructionId;
                            uint id = taskVariable.IsTemporary ? taskVariable.TemporaryId : taskVariable.SymbolId;
                            Debug.WriteLine("Awaiter type: " + typeName);
                            if (typeName.Contains("ConfiguredTaskAwaitable") && taskVariable.DefinitionInstruction != null)
                            {
                                // This is hard coded based on pattern matching 
                                // on the ast generated by the compiler
                                var op = taskVariable.DefinitionInstruction.ConditionDefinitionInstruction.ConditionDefinitionInstruction.SourceOperand2;
                                //id = op.Instruction.Previous.Previous.InstructionId;
                                id = op.IsTemporary ? op.TemporaryId : op.SymbolId;
                                edgeLabels[fastPathConditionBB.Id] = new Pair<string, uint>("d", id);
                            }
                            else if (typeName.Contains("ConfiguredTaskAwaitable") && taskVariable.DefinitionInstruction == null)
                            {
                                var op = configuredTaskVariable;
                                //id = op.DefinitionInstruction.InstructionId;
                                id = op.IsTemporary ? op.TemporaryId : op.SymbolId;
                                edgeLabels[fastPathConditionBB.Id] = new Pair<string, uint>("d", id);
                            }
                            else
                                edgeLabels[fastPathConditionBB.Id] = new Pair<string, uint>("p", id);
                        }                        
                    }
                }
                if (!edgeLabels.Keys.Contains(fastPathConditionBB.Id))
                {                    
                    Console.WriteLine("Warning! Imprecise awaiter tracking for basic block {0} in function {1}", fastPathConditionBB.Id, fUnit.FunctionSymbol.QualifiedName);
                    throw new System.Exception("Could not track awaiter!");
                }
            }
        }
        
        /*
         * Get the value assigned to the state variable just prior to
         * suspending - has some hard coded patterns based on the bytecode
         * generated by the C# 5.0 compiler - handles both configured and 
         * vanilla awaiters
         */
        private static int GetSuccessorStateNumber(CallInstruction awaitCall)
        {
            var awaitBB = awaitCall.BasicBlock;
            foreach (var instr in awaitBB.Instructions)
            {
                if (instr.Opcode.Equals(Phx.Common.Opcode.Assign))
                {
                    var destOperand = instr.DestinationOperand;
                    var srcOperand = instr.SourceOperand;
                    if (!destOperand.ToString().Contains("1__state"))
                        continue;
                    var def1 = srcOperand.FindExactDefinitionBackwardBlock(instr);
                    // var def2 = def1.SourceOperand.FindExactDefinitionBackwardBlock(def1);
                    int value = -1;
                    if (def1.SourceOperand.IsImmediateOperand == false)
                    {
                        if(def1.ConditionDefinitionInstruction == null)
                        {
                            throw new System.Exception("Unknown pattern to set next state in " + awaitCall.FunctionUnit);
                        }
                        var op = def1.ConditionDefinitionInstruction.SourceOperand.AsImmediateOperand;
                        value = op.Value.AsIntValue.Int32;
                    }                    
                    else
                    {
                        var immediate = def1.SourceOperand.AsImmediateOperand;
                        value = immediate.Value.AsIntValue.Int32;
                    }                    
                    return value;
                }
            }
            return -1;
        }

        /*
         * Data flow analysis to compute a map from basic block(ID)s 
         * to the set of states they belong to
         */
        public static Dictionary<uint, HashSet<int>> TagBasicBlocks(FunctionUnit fUnit,
            int numStates,
            Dictionary<Pair<uint, uint>, Predicate<int>> entryPredicates,
            HashSet<Pair<uint, uint>> syncEdges,
            //HashSet<uint> continuationBB,
            Dictionary<uint, uint> targetContinuation
            )
        {
            Dictionary<uint, HashSet<int>> dataFlowFacts = new Dictionary<uint, HashSet<int>>();
            Queue<BasicBlock> worklist = new Queue<BasicBlock>();
            Initialize(fUnit, numStates, dataFlowFacts, worklist);

            var exitFinallyBlocks = GetExitFinallyBlocks(fUnit);

            while (worklist.Any())
            {
                var curr = worklist.Dequeue();                
                var currFact = dataFlowFacts[curr.Id];

                if(PurityAnalysisPhase.EnableConsoleLogging)
                    Console.WriteLine("Processing " + curr.Id);

                // special handling for ExitFinally blocks
                // read the Pheonix documentation, and look at a few example
                // flow graphs to see why this is necessary
                if (curr.LastInstruction.Opcode.Equals(Phx.Common.Opcode.ExitFinally))
                {
                    ExitFinally(targetContinuation, dataFlowFacts, worklist, curr, currFact);
                }
                else
                    HandleBasicBlock(entryPredicates, syncEdges, dataFlowFacts, worklist, curr, currFact, exitFinallyBlocks);
            }

            return dataFlowFacts;
        }

        // Apply the transfer function for the successors of curr
        private static void HandleBasicBlock(Dictionary<Pair<uint, uint>, Predicate<int>> entryPredicates,
            HashSet<Pair<uint, uint>> syncEdges,
            Dictionary<uint, HashSet<int>> dataFlowFacts,
            Queue<BasicBlock> worklist,
            BasicBlock curr,
            HashSet<int> currFact,
            HashSet<BasicBlock> exitFinallyBlocks)
        {
            foreach (var edge in curr.SuccessorEdges)
            {
                var target = edge.SuccessorNode.Id;

                // Don't propagate state along fast-path
                // the fast path kills the current facts, and the fast-path target
                // has already been initialized to the right values
                if (syncEdges.Contains(new Pair<uint, uint>(curr.Id, target)))
                    continue;

                Predicate<int> edgePred = null;
                var pair = new Pair<uint, uint>(curr.Id, target);
                if (entryPredicates.ContainsKey(pair))
                    edgePred = entryPredicates[pair];
                HashSet<int> targetFactInitial = new HashSet<int>();
                if (dataFlowFacts.ContainsKey(target))
                {
                    targetFactInitial = dataFlowFacts[target];
                }

                HashSet<int> targetFactFinal = new HashSet<int>(targetFactInitial);

                ApplyTransferFunction(currFact, edgePred, targetFactFinal);

                var changed = CheckForChange(dataFlowFacts, worklist, edge, target, targetFactInitial, targetFactFinal);

                // A change in the dataflow fact for an ExitTryWithFinallyCall block can trigger changes
                // in the successors of exitFinally blocks, which is why we enqueue all such blocks again
                if (edge.SuccessorNode.LastInstruction.Opcode.Equals(Phx.Common.Opcode.ExitTryWithFinallyCall) && changed)
                {
                    foreach (var bb in exitFinallyBlocks)
                        worklist.Enqueue(bb);
                }

                if(PurityAnalysisPhase.EnableConsoleLogging)
                Console.WriteLine("For edge {0} init: {1} final: {2}", edge, string.Join(" ", targetFactInitial.ToArray()),
                    string.Join(" ", targetFactFinal.ToArray()));
            }
        }

        private static void ApplyTransferFunction(HashSet<int> currFact, Predicate<int> edgePred, HashSet<int> targetFactFinal)
        {
            if (edgePred == null)
            {
                targetFactFinal.UnionWith(currFact);
            }
            else {
                foreach (var fact in currFact)
                {
                    if (edgePred(fact))
                        targetFactFinal.Add(fact);
                }
            }
        }

        private static void ExitFinally(Dictionary<uint, uint> targetContinuation, Dictionary<uint, HashSet<int>> dataFlowFacts, Queue<BasicBlock> worklist, BasicBlock curr, HashSet<int> currFact)
        {
            foreach (var edge in curr.SuccessorEdges)
            {
                var target = edge.SuccessorNode.Id;

                HashSet<int> targetFactInitial = new HashSet<int>();
                if (dataFlowFacts.ContainsKey(target))
                {
                    targetFactInitial = dataFlowFacts[target];
                }
                HashSet<int> targetFactFinal = new HashSet<int>(targetFactInitial);

                //targetFactFinal.UnionWith(targetFactInitial);

                ApplyExitTryTransferFunction(targetContinuation, dataFlowFacts, currFact, target, targetFactFinal);

                CheckForChange(dataFlowFacts, worklist, edge, target, targetFactInitial, targetFactFinal);
                if (PurityAnalysisPhase.EnableConsoleLogging)
                    Console.WriteLine("For edge {0} init: {1} final: {2}", edge, string.Join(" ", targetFactInitial.ToArray()),
                    string.Join(" ", targetFactFinal.ToArray()));
            }
        }

        private static bool CheckForChange(Dictionary<uint, HashSet<int>> dataFlowFacts, Queue<BasicBlock> worklist, FlowEdge edge, uint target, HashSet<int> targetFactInitial, HashSet<int> targetFactFinal)
        {
            if (!targetFactFinal.SetEquals(targetFactInitial))
            {
                // targetFactFinal is initialized to targetFactInitial
                dataFlowFacts[target] = targetFactFinal; 
                worklist.Enqueue(edge.SuccessorNode);
                return true;
            }
            return false;
        }

        private static void ApplyExitTryTransferFunction(Dictionary<uint, uint> targetContinuation, Dictionary<uint, HashSet<int>> dataFlowFacts, HashSet<int> currFact, uint target, HashSet<int> targetFactFinal)
        {
            if (targetContinuation.ContainsKey(target))
            {
                targetFactFinal.UnionWith(dataFlowFacts[targetContinuation[target]]);
            }
            else
            {
                targetFactFinal.UnionWith(currFact);
            }
        }

        private static void Initialize(FunctionUnit fUnit, int numStates, Dictionary<uint, HashSet<int>> dataFlowFacts, Queue<BasicBlock> worklist)
        {
            // initialize to \emptyset
            BasicBlock exceptionSource = null;
            foreach (var bb in fUnit.FlowGraph.BasicBlocks)
            {
                dataFlowFacts[bb.Id] = new HashSet<int>();
                if (bb.PredecessorCount == 0 && bb != fUnit.FlowGraph.StartBlock)
                {
                    exceptionSource = bb;
                }
            }

            for (int i = -1; i < numStates - 1; i++)
            {
                dataFlowFacts[fUnit.FlowGraph.StartBlock.Id].Add(i);
                if (exceptionSource != null)
                    dataFlowFacts[exceptionSource.Id].Add(i);
            }
            worklist.Enqueue(fUnit.FlowGraph.StartBlock);
            if (exceptionSource != null)
                worklist.Enqueue(exceptionSource);
        }

        
        public static void PrintDfsInformation(FunctionUnit fUnit)
        {
            var fg = fUnit.FlowGraph;
            fg.BuildDepthFirstNumbers();
            foreach (var bb in fg.BasicBlocks)
            {
                foreach (var edge in bb.SuccessorEdges)
                {
                    if (edge.IsBack)
                    {
                        var loopHeadBlock = edge.PredecessorNode;
                        var loopHeadLabel = loopHeadBlock.FirstInstruction;

                        String fileName = fUnit.DebugInfo.GetFileName(loopHeadLabel.DebugTag);
                        uint lineNumber = fUnit.DebugInfo.GetLineNumber(loopHeadLabel.DebugTag);

                        Debug.WriteLine("Found loop: Function {0} file {1} line {2}",
                           Phx.Utility.Undecorate(fUnit.NameString, false),
                           fileName, lineNumber);
                    }

                    if (edge.IsCross)
                    {

                    }

                    if (edge.IsTree)
                    {

                    }

                    if (edge.IsForward)
                    {

                    }
                }
            }
            fg.DropDepthFirstNumbers();
        }

        /*
         * Almost all the code that follows is to find out if a method
         * in an assembly is an async method. Unfortunately, the async
         * keyword is not preserved in the bytecode, so we have to jump
         * through hoops to recover this. This code can be improved further          
         */

        /*
         * Find the assemblies in a particular set of paths 
         */
        public static IEnumerable<Assembly> GetReferencedAssemblies(List<string> paths)
        {
            var assemblies = paths.Select(p => Assembly.LoadFrom(p));
            var referenced = assemblies.SelectMany(a => a.GetReferencedAssemblies());
            foreach(var r in referenced.Distinct())
            {
                yield return Assembly.Load(r);
            }          
        }

        public static List<string> GetAsyncMethods(List<string> paths)
        {
            List<string> l = new List<string>();
            var assemblies = GetReferencedAssemblies(paths);
            Type[] types = assemblies.SelectMany(a => a.GetTypes()).ToArray();
            return GetAsyncMethod(l, types);
        }

        public static List<string> GetAsyncMethods(string path)
        {
            List<string> l = new List<string>();
            Assembly a = Assembly.LoadFrom(path);
            Type[] types = a.GetTypes();
            return GetAsyncMethod(l, types);
        }
        
        /* 
         * The async method names in the dlls are returned as a list of strings.
         * They are renamed to reflect the naming convention followed by Phoenix
         */
        private static List<string> GetAsyncMethod(List<string> l, Type[] types)
        {
            foreach (Type type in types)
            {
                System.Reflection.MethodInfo[] methods = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Static);
                foreach (var method in methods)
                {
                    //Debug.WriteLine("Processing: " + type.Name + "." + method.Name);
                    if (IsAsyncMethod(type, method))
                    {
                        //Debug.WriteLine(type.Name + "." + method.Name);
                        var name = GetQualifiedFunctionName(method).Replace("[mscorlib]System.String", "System.String").Replace("+", "::").Replace("[mscorlib]System.Byte", "MgdArr[u8]");
                        Debug.WriteLine(name);
                        l.Add(name);
                    }
                }
            }
            return l;
        }

        private static bool IsAsyncMethod(Type classType, System.Reflection.MethodInfo method)
        {
            Attribute attrib = null;
            try
            {
                Type attType = typeof(AsyncStateMachineAttribute);
                attrib = (AsyncStateMachineAttribute)method.GetCustomAttribute(attType);
            }
            catch (AmbiguousMatchException)
            {
                Console.WriteLine("Ambiguous match for : {0}", method.Name);
            }
            if (attrib != null)
                return true;
            return false;
        }

        public static string GetFunctionTypeSignature(System.Reflection.MethodInfo mInfo)
        {
            string sig = "(";

            bool first = true;
            foreach (var param in mInfo.GetParameters())
            {
                if (first)
                {
                    first = false;
                }
                else
                {
                    sig += ",";
                }
                sig += GetTypeName(param.ParameterType);
            }
            sig += ")";
            sig += GetTypeName(mInfo.ReturnType);            
            return sig;
        }

        public static string GetFunctionName(System.Reflection.MethodInfo m)
        {
            return "[" + m.DeclaringType.Assembly.GetName().Name + "]" + m.DeclaringType.FullName + "::" + m.Name;
        }

        public static string GetTypeName(Type type)
        {
            if (type.IsPrimitive)
            {
                CodeDomProvider provider = CodeDomProvider.CreateProvider("CSharp");

                var typeRef = new CodeTypeReference(type);
                return provider.GetTypeOutput(typeRef).Replace("long", "i64").Replace("int", "i32").Replace("bool", "bool8");
            }
            string fullName;
            if (type.FullName != null)
            {
                fullName = type.FullName;
                int index = fullName.IndexOf("[");
                if (index > 0)
                    fullName = fullName.Substring(0, index);
            }
            else {
                fullName = type.Namespace + "." + type.Name;
            }
            return "[" + type.Assembly.GetName().Name + "]" + fullName;
        }

        public static string GetQualifiedFunctionName(System.Reflection.MethodInfo funcsym)
        {
            //var typename = PhxUtil.GetTypeName(funcsym.EnclosingAggregateType);
            var funcname = GetFunctionName(funcsym);
            var sig = GetFunctionTypeSignature(funcsym);
            return funcname + "/" + sig;
        }
        
        public static bool IsAsyncStateMachine(Phx.FunctionUnit fUnit)
        {
            if (fUnit.FunctionSymbol.QualifiedName.Contains("MoveNext"))
            {
                var enclType = PhxUtil.NormalizedAggregateType(fUnit.FunctionSymbol.EnclosingAggregateType);
                var normaggtype = PhxUtil.NormalizedAggregateType(enclType);

                if (normaggtype.BaseTypeLinkList != null)
                {
                    var list = normaggtype.BaseTypeLinkList;
                    while (list != null)
                    {
                        var suptype = PhxUtil.NormalizedAggregateType(list.BaseAggregateType);
                        string tName = PhxUtil.GetTypeName(suptype);
                        if (tName != null)
                            if (tName.Contains("[mscorlib]System.Runtime.CompilerServices.IAsyncStateMachine"))
                                return true;
                        list = list.Next;
                    }
                }

            }
            return false;
        }
    }
}