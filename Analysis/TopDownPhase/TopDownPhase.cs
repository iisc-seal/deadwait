//using Phx;
//using QuickGraph.Algorithms.Search;
//using SafetyAnalysis.Framework.Graphs;
//using SafetyAnalysis.Purity.Summaries;
//using SafetyAnalysis.TypeUtil;
//using SafetyAnalysis.Util;
//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Text;

//namespace SafetyAnalysis.Purity
//{
//    class State
//    {
//        public WholeCGNode node;
//        public Call call;
//        public FunctionUnit callingUnit;
       
//        public State(WholeCGNode root, Call call, FunctionUnit funit)
//        {
//            this.node = root;
//            this.call = call;
//            this.callingUnit = funit;
//        }
//    }

//    public class TopDownPhase : Phx.Phases.Phase
//    {             
        
//#if (PHX_DEBUG_SUPPORT)
//        private static Phx.Controls.ComponentControl StaticAnalysisPhaseCtrl;
//#endif

//        public static void Initialize()
//        {
//#if PHX_DEBUG_SUPPORT
//            TopDownPhase.StaticAnalysisPhaseCtrl =
//                Phx.Controls.ComponentControl.New("TopDownPhase",
//                "Propagating heap information from callers to callees", "TopDownPhase.cs");            
//#endif
//        }

//        //---------------------------------------------------------------------
//        //
//        // Description:
//        //
//        //    Creates a new StaticAnalysisPhase object
//        //
//        // Arguments:
//        //
//        //    config - The encapsulating object that simplifies handling of
//        //    the phase list and pre and post phase events.
//        //
//        //---------------------------------------------------------------------
//        public static TopDownPhase New(Phx.Phases.PhaseConfiguration config)
//        {
//            TopDownPhase staticAnalysisPhase = new TopDownPhase();

//            staticAnalysisPhase.Initialize(config, "Top Down Phase");

//#if PHX_DEBUG_SUPPORT
//            staticAnalysisPhase.PhaseControl = TopDownPhase.StaticAnalysisPhaseCtrl;
//#endif
//            return staticAnalysisPhase;
//        }

//        ISet<PurityAnalysisData> GetInitFact()
//        {
//            var initFact = new HashSet<PurityAnalysisData>();
//            initFact.Add(new PurityAnalysisData(new HeapGraph()));
//            return initFact;
//        }

//        protected override void Execute(Phx.Unit unit)
//        {
//            if (!unit.IsPEModuleUnit)
//                return;

//            Phx.PEModuleUnit moduleUnit = unit.AsPEModuleUnit;

//            var wholecg = Purity.Summaries.CalleeSummaryReader.wholecg;
//            WholeCGNode root = wholecg.Vertices.Where(x => x.Name.Contains("::Main")).First();

//            var dfs = new DepthFirstSearchAlgorithm<WholeCGNode, WholeCGEdge>(wholecg);            
//            dfs.DiscoverVertex += (x) => { wholecg.flowFact[x] = GetInitFact(); };
//            dfs.Compute(root);
          
//            FunctionUnit mainfunit = null;
//            foreach(var funit in moduleUnit.ChildFunctionUnits)
//            {
//                if (funit.FunctionSymbol.NameString.Equals("Main"))
//                {
//                    mainfunit = funit;
//                }
//            }

//            if(mainfunit == null)
//            {
//                throw new System.Exception("Dll does not contain a main method");
//            }

//            //synthesise a dummy call instruction to main
//            var mainname = PhxUtil.GetFunctionName(mainfunit.FunctionSymbol);
//            var mainsig = PhxUtil.GetFunctionTypeSignature(mainfunit.FunctionSymbol.FunctionType);
//            var containingType = PhxUtil.GetTypeName(mainfunit.FunctionSymbol.EnclosingAggregateType);
//            var maincall = new StaticCall(29, 29, 29, null, mainname, containingType, mainsig, new List<string>());            

//            IWorklist<State> worklist = new MRWorklist<State>();
//            State init = new State(root, maincall, mainfunit);
//            worklist.Enqueue(init);

//            while (worklist.Any())
//            {
//                var state = worklist.Dequeue();
//                var node = state.node;
//                var call = state.call;
//                var callerUnit = state.callingUnit;

//                if (PurityAnalysisPhase.EnableConsoleLogging)
//                    Console.WriteLine("Processing Node: " + node.id + " Name: " + node.Name);

//                ISet<PurityAnalysisData> oldData = null;

//                if (wholecg.flowFact.ContainsKey(node))
//                    oldData = wholecg.flowFact[node];

//                HashSet<PurityAnalysisData> newData = new HashSet<PurityAnalysisData>();
//                var builder = new HigherOrderHeapGraphBuilder(callerUnit);
//                foreach (var fact in oldData)
//                {
//                    var outfact = fact.Copy();
//                    var calltype = CallUtil.GetCallType(call, outfact);
//                    if (calltype.stubbed == true)
//                    {
//                        CallStubManager summan;
//                        if (CallStubManager.TryGetCallStubManager(call, out summan))
//                        {
//                            var th = CombinedTypeHierarchy.GetInstance(moduleUnit);
//                            var calleeSummary = summan.GetSummary(call, outfact, th);
//                            ApplyTargetsSummary(call, outfact, calleeSummary, builder as HigherOrderHeapGraphBuilder);

//                            // resolve skipped calls
//                            builder.ComposeResolvableSkippedCalls(outfact);
//                            outfact.skippedCallTargets.UnionWith(builder.GetMergedTargets());

//                            newData.Add(outfact);
//                        }
//                        else
//                            throw new SystemException("Cannot find stub managaer for call: " + call);
//                    }
//                    else
//                    {
//                        var decTypename = node.declaringType;
//                        var methodname = node.methodname;
//                        var sig = node.signature;
//                        var typeinfo = CombinedTypeHierarchy.GetInstance(moduleUnit).LookupTypeInfo(decTypename);
//                        var methodInfos = typeinfo.GetMethodInfos(methodname, sig);
//                        var summaries = new List<PurityAnalysisData>();

//                        foreach (var methodinfo in methodInfos)
//                        {

//                            var calleeSum = methodinfo.GetSummary();
//                            if (calleeSum == null)
//                            {
//                                //callees are not downward closed here and we do not have stubs
//                                //log error msg
//                                string qualifiedName = decTypename + "::" + methodname + "/" + sig;
//                                Console.WriteLine("Cannot find the summary for: " + qualifiedName);
//                                MethodLevelAnalysis.unknownTargetCalls.Add(qualifiedName);

//                                //use unanalyzable call summary here
//                                var calleeData = SummaryTemplates.GetUnanalyzableCallSummary(methodname, decTypename);
//                                summaries.Add(calleeData);
//                                continue;
//                            }
//                            summaries.Add(calleeSum);
//                        }

//                        foreach (var s in summaries)
//                        {
//                            var f = outfact.Copy();
//                            ApplyTargetsSummary(call, f, s, builder as HigherOrderHeapGraphBuilder);

//                            // resolve skipped calls
//                            builder.ComposeResolvableSkippedCalls(f);
//                            f.skippedCallTargets.UnionWith(builder.GetMergedTargets());

//                            newData.Add(f);
//                        }
//                    }                   
//                }

//                if(oldData.Count != newData.Count || !oldData.Equals(newData))
//                {
//                    IEnumerable<WholeCGEdge> edges = null;
//                    if (wholecg.TryGetOutEdges(node, out edges))
//                    {
//                        foreach (var edge in edges)
//                        {
//                            var currentCall = edge.call;
//                            var cUnit = edge.callerFUnit;
//                            var newState = new State(edge.Target, currentCall, cUnit);
//                            wholecg.flowFact[edge.Target] = newData;
//                            worklist.Enqueue(newState);
//                        }
//                    }
//                }
                               
//            }

            

//            // 0. Initialize the flow fact for the main proc to contain a set with empty purity data
//            // Extend wholeCG edge to track a call object - done
//            // Extend wholeCG to allow one to associate a set of puritydata with each node - done   

//            // 1. Find the node for the main procedure from the wholeCG

//            // 2. Put it into a worklist.

//            // 3. While there exists anything in a worklist, pop it. retrieve its summary. apply to the current data flow fact.
//            // propagate the resulting set to all successors, and put them into the worklist if anything changes
//            // 3.1 Method to retrieve summary given a methodname and signature
//            // 3.2 Method to apply summary : given a set, apply on each elt of the set pointwise and return resulting set
//            // 3.3 Method to propagate and enq if change happens

//            // << === >>
//            // At this point get it working on at least 3-4 real examples (toy) and a real benchmark, and make sure
//            // There are no external vertices in the output
//            // The points-to sets make sense

//            // Figure out what library methods it is essential for us to model correctly

//            // --> Can fill this in later
//            // 4. Code to print/serialize outputs - perhaps only the relevant part - extract task information etc?


//        }

//        public static void PrintDataFlowFacts()
//        {
//            var wholecg = Purity.Summaries.CalleeSummaryReader.wholecg;
//            WholeCGNode root = wholecg.Vertices.Where(x => x.Name.Contains("::Main")).First();
//            var dfs = new DepthFirstSearchAlgorithm<WholeCGNode, WholeCGEdge>(wholecg);
//            dfs.DiscoverVertex += (x) => {
//                var f = wholecg.flowFact[x];
//                Console.WriteLine("Printing facts for " + x);
//                foreach(var abstractGraph in f)
//                {
//                    Console.WriteLine(abstractGraph.ToString());
//                }
//            };
//            dfs.Compute(root);
//        }

//        protected void ApplyTargetsSummary(
//           Call call,
//           PurityAnalysisData callerData,
//           PurityAnalysisData calleeSummary,
//           HigherOrderHeapGraphBuilder builder
//           )
//        {
//            //skip this callee if there no way to come back to the caller from the callee. 
//            //may happen when the callee terminates the program or throw an Exception unconditionally.             
//            if (calleeSummary != null &&
//                !calleeSummary.OutHeapGraph.IsVerticesEmpty)
//            {
//                var clonedCalleeData = AnalysisUtil.TranslateSummaryToCallerNamespace(
//                    calleeSummary, new List<object> { call.instructionContext }, call.callingMethodnames, call.directCallInstrID);

//                //add the return vertex to the data and mark it as strong update
//                //if it is not added    
//                if (call.HasReturnValue())
//                {
//                    var retvar = call.GetReturnValue();
//                    if (PurityAnalysisPhase.FlowSensitivity &&
//                        callerData.OutHeapGraph.ContainsVertex(retvar))
//                    {
//                        callerData.AddVertexWithStrongUpdate(retvar);
//                    }
//                }
//                builder.ComposeCalleeSummary(call, callerData, clonedCalleeData);               
//            }
//        }

//    }
//}
