#region Using namespace declarations

using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Xml;
using System.Xml.Serialization;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters;
using System.Runtime.Serialization.Formatters.Binary;
using System.Windows.Forms;

using Phx.Graphs;
using SafetyAnalysis.Purity.Properties;
using SafetyAnalysis.Framework.Graphs;
using SafetyAnalysis.Purity.Summaries;
using SafetyAnalysis.Purity.statistics;
using SafetyAnalysis.Util;
using SafetyAnalysis.Purity.callgraph;
using SafetyAnalysis.TypeUtil;
using QuickGraph.Algorithms.Search;
using Phx.Symbols;
#endregion


namespace SafetyAnalysis.Purity
{
    using ControlFlowAnalysisPhase;

    class State
    {
        public WholeCGNode node;        
        public Phx.FunctionUnit callingUnit;
        public Call call;

        public State(WholeCGNode root, Phx.FunctionUnit funit, Call call)
        {
            this.node = root;            
            this.callingUnit = funit;
            this.call = call;
        }

        public override bool Equals(object obj)
        {
            if(obj is State)
            {
                var other = obj as State;
                return this.node.Equals(other.node)
                    && this.callingUnit.Equals(other.callingUnit)
                    && this.call.Equals(other.call);
            }
            return false;
        }

        public override int GetHashCode()
        {
            return this.node.GetHashCode() 
                ^ this.callingUnit.GetHashCode() ^ this.call.GetHashCode();
        }
    }

    public class PurityAnalysisPhase : Phx.Phases.Phase
    {        
        //public static Dictionary<String, PurityAnalysisData> pointsTo = new Dictionary<string, PurityAnalysisData>();
        // public static MultiValueDictionary<Pair<String, HeapVertexBase>, HeapVertexBase> vertexMapping = new MultiValueDictionary<Pair<String, HeapVertexBase>, HeapVertexBase>();
#if (PHX_DEBUG_SUPPORT)
        private static Phx.Controls.ComponentControl StaticAnalysisPhaseCtrl;
#endif

        public static Dictionary<string, string> properties = new Dictionary<string, string>();
        public static string sealHome;                   
        public const string lambdaDelegateName = "CachedAnonymousMethodDelegate";    

        public static bool firstTime = true;
        public static string analysistype;

        #region output flags

        public static bool EnableStats = false;
        public static bool EnableConsoleLogging = false;
        public static bool EnableLogging = false;        

        #endregion output flags

        #region debug flags

        public static bool trackUnanalyzableCalls = false;
        public static bool TraceSkippedCallResolution = false;
        public static bool TraceSummaryApplication = false;
        public static bool DumpIR = false;
        public static bool DumpSummariesAsGraphs = false;
        public static bool RecordInteresting = false;
        public static bool DumpStateMachineGraphs = false;
        public static bool DumpPointsToGraphs = false;
        public static bool DumpFlowGraphs = false;
        public static bool FilterMustHB = true;
        public static bool FilterConfigured = true;
        #endregion debug flags

        #region analysis flags

        public static bool FlowSensitivity = false;        
        public static bool DisableExternalCallResolution = false;
        public static bool BoundContextStr = false;
        public static int  ContextStrBound = 0;                
        public static bool DisableSummaryReduce = false;
        public static bool clearDBContents = false;
        public static bool TrackPrimitiveTypes = false;
        public static bool TrackStringConstants = false;
        public static bool IsTopDown = false;

        #endregion analysis flags        

        #region outputfiles
        
        public static string outputdir = null;
        public static bool IsFramework = false;
        public static bool RemoveNonEscaping = true;
        public StreamWriter statsFileWriter;
        public StreamWriter unaCallsWriter;
        public XmlWriter chaCallGraphWriter;
        public XmlWriter finalCallGraphWriter;

        #endregion outputfiles        
       
        #region stats

        public static Dictionary<Phx.Graphs.CallNode, int> iterationCounts = new Dictionary<Phx.Graphs.CallNode, int>();           

        #endregion stats        

        public static PurityDBDataContext DataContext
        {
            get
            {
                string value;
                properties.TryGetValue("db", out value);

                //if (value.Equals("cloud"))
                //    return new PurityDBDataContext(DBSettings.Default.CloudDBConnectionString);
                //else if (value.Equals("localserver"))
                //    return new PurityDBDataContext(DBSettings.Default.LocalDBConnectionString);
                //else 
                if (value.Equals("file"))                
                    return new PurityDBDataContext(DBSettings.Default.FileDBConnectionString);                
                else
                    throw new NotSupportedException("Unknown usedb value");
            }
        }

        

        public static void Initialize()
        {
#if PHX_DEBUG_SUPPORT
            PurityAnalysisPhase.StaticAnalysisPhaseCtrl = Phx.Controls.ComponentControl.New("PurityAnalysis","Perform Static Analysis", "PurityAnlaysisPhase.cs");            
#endif
        }
        
        public static PurityAnalysisPhase New(Phx.Phases.PhaseConfiguration config)
        {
            PurityAnalysisPhase staticAnalysisPhase =
                new PurityAnalysisPhase();

            staticAnalysisPhase.Initialize(config, "Purity Analysis");

#if PHX_DEBUG_SUPPORT
            staticAnalysisPhase.PhaseControl = 
                PurityAnalysisPhase.StaticAnalysisPhaseCtrl;
#endif

            return staticAnalysisPhase;
        }

        public static void InitializeProperties()
        {
            //var assemblyname = moduleunit.Manifest.Name.NameString;

            //Read properties and perform initialization
            string value;
            if (properties.TryGetValue("analyzelist", out value))
                AnalyzableMethods.Initialize(value);

            if (properties.TryGetValue("statistics", out value))            
                EnableStats = Boolean.Parse(value);
                        

            if (properties.TryGetValue("logging", out value))
                EnableLogging = Boolean.Parse(value);            

            if (properties.TryGetValue("dumpprogresstoconsole", out value))
                EnableConsoleLogging = Boolean.Parse(value);            

            //intialize some properties
            string cleardb;
            if (PurityAnalysisPhase.properties.TryGetValue("cleardbcontents", out cleardb))
                clearDBContents = Boolean.Parse(cleardb);            

            //initialize debug flags
            if (properties.TryGetValue("trackunanalyzablecalls", out value))
                trackUnanalyzableCalls = Boolean.Parse(value);            

            if (properties.TryGetValue("traceskippedcallresolution", out value))
                TraceSkippedCallResolution = Boolean.Parse(value);            

            if (properties.TryGetValue("tracesummaryapplication", out value))
                TraceSummaryApplication = Boolean.Parse(value);

            if (properties.TryGetValue("dumpir", out value))
                DumpIR = Boolean.Parse(value);

            if (properties.TryGetValue("recordinteresting", out value))
                RecordInteresting = Boolean.Parse(value);

            if (properties.TryGetValue("dumpsummariesasgraphs", out value))
                DumpSummariesAsGraphs = Boolean.Parse(value);

            if (properties.TryGetValue("dumpstatemachinegraphs", out value))
                DumpStateMachineGraphs = Boolean.Parse(value);

            if (properties.TryGetValue("dumppointstographs", out value))
                DumpPointsToGraphs = Boolean.Parse(value);

            if (properties.TryGetValue("dumpsummariesasgraphs", out value))
                DumpSummariesAsGraphs = Boolean.Parse(value);

            if (properties.TryGetValue("dumpflowgraphs", out value))
                DumpFlowGraphs = Boolean.Parse(value);
                        
            //initialize analysis flags
            if (properties.TryGetValue("flowsensitivity", out value))
                FlowSensitivity = Boolean.Parse(value);            

            if (properties.TryGetValue("boundcontextstring", out value))
                BoundContextStr = Boolean.Parse(value);

            if (BoundContextStr)
            {
                properties.TryGetValue("contextbound", out value);
                ContextStrBound = Int32.Parse(value);
            }

            if (properties.TryGetValue("disableexternalcallresolution", out value))
                DisableExternalCallResolution = Boolean.Parse(value);

            if (properties.TryGetValue("disablesummaryreduce", out value))
                DisableSummaryReduce = Boolean.Parse(value);

            if (properties.TryGetValue("removenonescaping", out value))
                RemoveNonEscaping = Boolean.Parse(value);

            if (properties.TryGetValue("trackstringconstants", out value))
                TrackStringConstants = Boolean.Parse(value);

            if (properties.TryGetValue("trackprimitivetypes", out value))
                TrackPrimitiveTypes = Boolean.Parse(value);

            if (properties.TryGetValue("filtermusthb", out value))
                FilterMustHB = Boolean.Parse(value);

            if (properties.TryGetValue("filterconfigured", out value))
                FilterConfigured = Boolean.Parse(value);
        }

        private void InitializeInstanceProperties(Phx.PEModuleUnit moduleunit)
        {
            var assemblyname = moduleunit.Manifest.Name.NameString;
            var dirname = PurityAnalysisPhase.outputdir;
            
            //Read properties and perform initialization
            string value;
            if (EnableStats)
            {
                if (properties.TryGetValue("statsfilenamesuffix", out value))
                    statsFileWriter = new StreamWriter(new FileStream(dirname + assemblyname + value,
                        FileMode.Create, FileAccess.Write, FileShare.Read));
                else throw new ArgumentException("Cannot find the statsfilenamesuffix property");
            }

            if (properties.TryGetValue("chacgfilenamesuffix", out value))
            {
                chaCallGraphWriter = XmlWriter.Create(new FileStream(dirname + assemblyname + value, 
                    FileMode.Create, FileAccess.Write, FileShare.Read));
            }

            if (properties.TryGetValue("finalcgfilenamesuffix", out value))
            {
                finalCallGraphWriter = XmlWriter.Create(new FileStream(dirname + assemblyname + value, 
                    FileMode.Create, FileAccess.Write, FileShare.Read));
            }
            
            if (EnableLogging)
            {
                if (properties.TryGetValue("logfilenamesuffix", out value))
                    Trace.Listeners.Add(new TextWriterTraceListener(dirname + assemblyname + value));                
                else throw new ArgumentException("Cannot find the logfilenamesuffix property");
            }            
        }
       
        protected override void Execute(Phx.Unit unit)
        {
            if (!unit.IsPEModuleUnit)
                return;

            Phx.PEModuleUnit moduleUnit = unit.AsPEModuleUnit;
            var assemblyname = moduleUnit.Manifest.Name.NameString;

            InitializeInstanceProperties(moduleUnit);

            //start a stopwatch
            System.Diagnostics.Stopwatch sw = System.Diagnostics.Stopwatch.StartNew();
            sw.Start();

            //construct call graph
            Phx.Phases.PhaseConfiguration callgraphConfig =
                Phx.Phases.PhaseConfiguration.New(moduleUnit.Lifetime, "Callgraph construction phase");

            callgraphConfig.PhaseList.AppendPhase(CHACallGraphConstructionPhase.New(callgraphConfig));
            callgraphConfig.PhaseList.DoPhaseList(moduleUnit);

            if (PurityAnalysisPhase.EnableConsoleLogging)
            {
                Console.WriteLine("Number of call-graph nodes: " + moduleUnit.CallGraph.NodeCount);
                Console.WriteLine("Number of instructions analyzed: " + CHACallGraphConstructionPhase.instructionCount);
            }

            //dump the initial call graph            
            if (chaCallGraphWriter != null)
                GraphUtil.SerializeCallGraphAsDGML(moduleUnit.CallGraph, chaCallGraphWriter, null);

            //compute summaries (this will also construct call graph on the fly)
            AnalyzeBottomUp(moduleUnit);

            //dump the final call graph
            if (finalCallGraphWriter != null)
            {
                GraphUtil.SerializeCallGraphAsDGML(moduleUnit.CallGraph, finalCallGraphWriter,
                    (Phx.Graphs.CallNode n) =>
                    {
                        string summarysize = "";
                        var summary = n.CallGraph.SummaryManager.RetrieveSummary(
                            n, PurityAnalysisSummary.Type) as PurityAnalysisSummary;
                        if (summary != null && summary.PurityData != null)
                        {
                            summarysize += "[" + summary.PurityData.OutHeapGraph.EdgeCount
                               + "," + summary.PurityData.OutHeapGraph.VertexCount
                               + "," + summary.PurityData.SkippedCalls.Count() + "]";
                        }
                        return summarysize;
                    });
            }
            sw.Stop();

            //interaction            
            //Interact(moduleUnit);
            string value;
            //if (properties.TryGetValue("outputfilenamesuffix", out value))
            //    outputFileWriter = new StreamWriter(new FileStream(
            //        PurityAnalysisPhase.outputdir + assemblyname + value, FileMode.Create, FileAccess.Write, FileShare.Read));
            //else
            //    throw new ArgumentException("No output filename specified");

            //Phx.Phases.PhaseConfiguration reportConfig =
            //    Phx.Phases.PhaseConfiguration.New(moduleUnit.Lifetime, "Output generation phase");            
            //var reportphase = PurityReportGenerationPhase.New(reportConfig, this.outputFileWriter);
            //reportConfig.PhaseList.AppendPhase(reportphase);
            //reportConfig.PhaseList.DoPhaseList(moduleUnit);

            if (PurityAnalysisPhase.analysistype == "")
            {
                //dump analysis time and peak memory used
                Console.WriteLine("Analysis time: " + sw.Elapsed);
                Console.WriteLine("# Memory {0} KB",
                    (System.Diagnostics.Process.GetCurrentProcess().PeakVirtualMemorySize64 / 1000));
            }

            if (properties.TryGetValue("unacallsfilenamesuffix", out value))
            {
                this.unaCallsWriter = new StreamWriter(new FileStream(PurityAnalysisPhase.outputdir + assemblyname + value,
                    FileMode.Create, FileAccess.Write, FileShare.Read));
                StatisticsManager.GetInstance().DumpUnanalyzableCalls(unaCallsWriter);
                this.unaCallsWriter.Close();
            }                        
            
            if (PurityAnalysisPhase.properties.TryGetValue("summaryserialization", out value)
                && Boolean.Parse(value))
            {
                Console.WriteLine("serializing ...");
                PurityDBDataContext dbContext = PurityAnalysisPhase.DataContext;
                CombinedTypeHierarchy.GetInstance(moduleUnit).SerializeInternalTypes(moduleUnit, dbContext);
                try
                {
                    dbContext.SubmitChanges();
                }
                catch (System.Data.SqlClient.SqlException sqlE)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("Exception occurred on commiting changes: " + sqlE.Message);
                    Console.ResetColor();
                }
                //catch (System.Data.SqlServerCe.SqlCeException e)
                //{
                //    Console.ForegroundColor = ConsoleColor.Red;
                //    Console.WriteLine("Exception occurred on commiting changes: " + e.Message);
                //    Console.ResetColor();
                //}
                finally
                {
                    dbContext.Dispose();
                }
            }

            //TODO: Need to store summaries into a new database when firsttime is true
            /*
            if (analysistype == "sourcesinkanalysis" && PurityAnalysisPhase.firstTime)
            {
                SourceSinkUtil.createHashFile();
                if (EnableConsoleLogging) Console.WriteLine("serializing ...");
                ClearMyDB();
                PurityDBDataContext dbContext = PurityAnalysisPhase.MyDBContext;
                CombinedTypeHierarchy.GetInstance(moduleUnit).SerializeInternalTypes(moduleUnit, dbContext);
                try
                {
                    dbContext.SubmitChanges();
                }
                catch (System.Data.SqlClient.SqlException sqlE)
                {
                    //Console.ForegroundColor = ConsoleColor.Red;
                    //Console.WriteLine("Exception occurred on commiting changes: " + sqlE.Message);
                    //Console.ResetColor();
                }
                finally
                {
                    dbContext.Dispose();
                }
            }
             */

            if (EnableStats)
            {                
                this.statsFileWriter.Close();
            }
        }

        private void AnalyzeBottomUp(Phx.PEModuleUnit moduleUnit)
        {           
            Phx.Phases.PhaseConfiguration initConfig =
                Phx.Phases.PhaseConfiguration.New(moduleUnit.Lifetime, "Intialization Phases");
            
            initConfig.PhaseList.AppendPhase(InitializationPhase.New(initConfig));            
            initConfig.PhaseList.DoPhaseList(moduleUnit);
            moduleUnit.Context.PopUnit();            

            int methodsToAnalyse = 0, methodsSkipped = 0;            
            IWorklist<Phx.Graphs.CallNode> worklist = new MRWorklist<Phx.Graphs.CallNode>();           

            //analyse the methods in the post-order (add all the methods to the worklist)
            Phx.Graphs.NodeFlowOrder nodeOrder = Phx.Graphs.NodeFlowOrder.New(moduleUnit.Lifetime);                
            nodeOrder.Build(moduleUnit.CallGraph, Phx.Graphs.Order.ReversePostOrder);

            var toRemoveEdges = new HashSet<Phx.Graphs.CallEdge>();
            for (uint i = 1; i <= nodeOrder.NodeCount; i++)
            {
                Phx.Graphs.CallNode node = nodeOrder.Node(i).AsCallNode;
                if (!AnalyzableMethods.IsAnalyzable(node.FunctionSymbol))
                {
                    methodsSkipped++;
                    if (PurityAnalysisPhase.EnableConsoleLogging)
                    {
                        if (node.FunctionSymbol.UninstantiatedFunctionSymbol != null)
                            Console.WriteLine("skipping an instantiation");
                        else if (!PhxUtil.DoesBelongToCurrentAssembly(node.FunctionSymbol, moduleUnit))
                            Console.WriteLine("skipping an external method");
                        else if (node.FunctionSymbol.FunctionUnit == null)
                            Console.WriteLine("skipping an undefined method: " + node.FunctionSymbol.QualifiedName);
                        else
                            Console.WriteLine("skipping not to analyze method: " + node.FunctionSymbol.QualifiedName);
                    }
                }
                else
                {
                    worklist.Enqueue(node);
                    methodsToAnalyse++;
                    foreach (var edge in node.SuccessorEdges)
                        toRemoveEdges.Add(edge);
                }
            }

            //remove all edges from the call-graph
            foreach (var edge in toRemoveEdges)
                moduleUnit.CallGraph.RemoveEdge(edge);
            
            while (worklist.Any())
            {
                var node = worklist.Dequeue();

                if (PurityAnalysisPhase.EnableConsoleLogging)
                    Console.WriteLine("Processing Node: " + node.Id + " Qualified Name: " + node.FunctionSymbol.QualifiedName);

                if (iterationCounts.ContainsKey(node))
                    iterationCounts[node]++;
                else
                    iterationCounts.Add(node, 1);                

                Phx.FunctionUnit functionUnit = node.FunctionSymbol.FunctionUnit;
                if (!functionUnit.IRState.Equals(Phx.FunctionUnit.HighLevelIRFunctionUnitState))
                {
                    moduleUnit.Raise(node.FunctionSymbol, Phx.FunctionUnit.HighLevelIRFunctionUnitState);
                    functionUnit.Context.PopUnit();
                }

                //check the  instruction ids
                foreach (var inst in functionUnit.Instructions)
                {
                    if (inst.InstructionId == 0)
                        throw new SystemException("Instruction Ids not initialized");
                }

                var oldData = (moduleUnit.CallGraph.SummaryManager.RetrieveSummary(
                    node,PurityAnalysisSummary.Type) as PurityAnalysisSummary).PurityData.Copy();

                //var newData = (new MethodLevelAnalysis(functionUnit)).Execute();

                PurityAnalysisData newData = null;

                if (firstTime)
                {
                    newData = (new MethodLevelAnalysis(functionUnit)).Execute();
                }
                else
                {
                    //Currently, code will never reach here
                    /*
                    if (functionUnit.FirstEnterInstruction.GetFileName().ToString() != null
                        && SourceSinkUtil.sourceCheck(functionUnit) && SourceSinkUtil.sinkCheck(functionUnit))
                    {
                        if (PurityAnalysisPhase.EnableConsoleLogging)
                            Console.WriteLine("Can be taken from database");

                        string typename = PhxUtil.GetTypeName(node.FunctionSymbol.EnclosingAggregateType);
                        string methodname = PhxUtil.GetFunctionName(node.FunctionSymbol);
                        string signature = PhxUtil.GetFunctionTypeSignature(node.FunctionSymbol.FunctionType);


                        //TODO: Read from a different database
                        PurityDBDataContext dbContext = PurityAnalysisPhase.DataContext;
                        newData = ReadSummaryFromDatabase(dbContext, typename, methodname, signature);
                        dbContext.Dispose();

                    }

                    if (newData == null)
                    {
                        if (PurityAnalysisPhase.EnableConsoleLogging)
                            Console.WriteLine("Recomputing");
                        newData = (new MethodLevelAnalysis(functionUnit)).Execute();
                    }
                    */
                }

                //Get the function name in required format (this is to support the plugin)
                String classAndFunctionName = functionUnit.ToString();
                String[] tokenized = classAndFunctionName.Split(new char[1] { ':' });
                String className = tokenized[0];
                String functionName = functionUnit.FunctionSymbol.ToString();
                String completeFunctionName = className + "." + functionName;

                if (analysistype == "sourcesinkanalysis")
                {
                    if (SafetyAnalysis.Util.SourceSinkUtil.checkingFunction == completeFunctionName)
                    {
                        //We reached the analyzing function
                        bool flows = false;

                        //Checks for intersection between source vertices and sink vertices
                        List<int> sourceNodes = new List<int>();

                        if (PurityAnalysisPhase.EnableConsoleLogging)
                            Console.WriteLine("Source Nodes : ");

                        foreach (HeapVertexBase vertex in newData.OutHeapGraph.Vertices)
                        {
                            if (vertex is GlobalLoadVertex)
                            {
                                foreach (HeapEdgeBase edge in newData.OutHeapGraph.OutEdges(vertex))
                                {
                                    if (edge.Field.ToString().Equals("::" + SourceSinkUtil.sourceEdgeLabel))
                                    {
                                        if (PurityAnalysisPhase.EnableConsoleLogging)
                                            Console.WriteLine(edge.Target.Id);
                                        sourceNodes.Add(edge.Target.Id);
                                    }
                                }
                            }
                        }

                        if (PurityAnalysisPhase.EnableConsoleLogging)
                            Console.WriteLine("Sink Nodes : ");

                        foreach (HeapVertexBase vertex in newData.OutHeapGraph.Vertices)
                        {
                            if (vertex is GlobalLoadVertex)
                            {
                                foreach (HeapEdgeBase edge in newData.OutHeapGraph.OutEdges(vertex))
                                {
                                    if (edge.Field.ToString().Equals("::" + SourceSinkUtil.sinkEdgeLabel))
                                    {
                                        if (sourceNodes.Contains(edge.Target.Id))
                                        {
                                            flows = true;
                                        }
                                        if (PurityAnalysisPhase.EnableConsoleLogging)
                                            Console.WriteLine(edge.Target.Id);
                                    }
                                }
                            }
                        }

                        SafetyAnalysis.Util.SourceSinkUtil.answer = flows ? "May Flow" : "Does Not Flow";
                        //The analysis can be terminated here
                    }
                }

                if (analysistype == "castanalysis")
                {
                    if (TypeCastUtil.analyzingFunction == completeFunctionName)
                    {
                        //We reached the analyzing function
                        TypeCastUtil.check(newData, moduleUnit);

                        //The analysis can be terminated here
                    }
                }


                //attach the summary                          
                AttachSummary(functionUnit, newData);

                if (EnableStats)
                {
                    (StatisticsManager.GetInstance()).DumpSummaryStats(node, newData, statsFileWriter);
                }

                //check if the summary has changed
                if ((oldData != null && newData != null && !oldData.Equivalent(newData))
                            || (oldData == null && oldData != newData))
                {
                    //add all the predecessors of this node belonging the scc to the worklist.
                    foreach (var predEdges in node.PredecessorEdges)
                    {
                        var predNode = predEdges.CallerCallNode;
                        if (!worklist.Contains(predNode))
                        {
                            worklist.Enqueue(predNode);
                        }
                    }
                }
                //update progress (progress need not be monotonically increasing) 
                var progress = Math.Round(((methodsToAnalyse - worklist.Count()) * 100.0 / methodsToAnalyse),1);

                if (PurityAnalysisPhase.EnableConsoleLogging)
                    Console.WriteLine("Progress: {0}%", progress);
            }

            if(PurityAnalysisPhase.IsFramework == false)
            for (uint i = 1; i <= nodeOrder.NodeCount; i++)
            {
                Phx.Graphs.CallNode node = nodeOrder.Node(i).AsCallNode;
                Debug.WriteLine("Considering {0} {1}", node, node.FunctionSymbol.FunctionUnit);
                if (AnalyzableMethods.IsAnalyzable(node.FunctionSymbol))
                {
                    if(node.FunctionSymbol.FunctionUnit != null)
                        new StateMachineAnalysis().Analyze(node.FunctionSymbol.FunctionUnit);
                }
            }
                    
             //wrap up 
             //if(moduleUnit.Next != null)
                NodeEquivalenceRelation.Reset();            
            //moduleUnit.Context.PopUnit();            
        }
     
        private void SanityCheckEdges(WholeCGEdge e)
        {
            throw new System.Exception("Call graph edge added during top down phase!");
        }

        private void SanityCheckVertices(WholeCGNode n)
        {
            throw new System.Exception("Call graph Vertex added during top down phase!");
        }

        public void AnalyzeTopDown(Phx.PEModuleUnit moduleUnit)
        {
            /*Context sensitivity, extra modeling*/
            Purity.Summaries.CalleeSummaryReader.wholecg.ShortCircuitStart();
            Purity.Summaries.CalleeSummaryReader.wholecg.ShortCircuitExec();
            Purity.Summaries.CalleeSummaryReader.wholecg.ModelTaskAsyncMethods();

            PurityAnalysisPhase.IsTopDown = true;

            var wholecg = Purity.Summaries.CalleeSummaryReader.wholecg;
            //Console.WriteLine("Trying Short Circuit");
            //wholecg.tryShortCircuit();
            wholecg.PrintSCCs();
            
            //CalleeSummaryReader.trackCallGraph = false;
            WholeCGNode mainVertex = wholecg.Vertices.Where(x => x.Name.Contains("::Main") || x.Name.Contains("::main")).First();

            var dfs = new DepthFirstSearchAlgorithm<WholeCGNode, WholeCGEdge>(wholecg);
            dfs.DiscoverVertex += (x) => {               
                // wholecg.flowFact[x] = new PurityAnalysisData(new HeapGraph());
                // wholecg.processedFact[x] = GetInitFact();
                // wholecg.finalFact[x] = new HashSet<PurityAnalysisData>();
                wholecg.reachable.Add(x);
            };

           
            var bfs = new BreadthFirstSearchAlgorithm<WholeCGNode, WholeCGEdge>(wholecg);
            bfs.DiscoverVertex += (x) =>
            {
                Console.WriteLine("Reached " + x.Name);
            };

            bfs.Compute(mainVertex);
            dfs.Compute(mainVertex);

            wholecg.interesting = new HashSet<WholeCGNode>(wholecg.GetInteresting().Intersect(wholecg.reachable));
            var methodsToAnalyse = wholecg.interesting.Count();
            Console.WriteLine("Printing reachable:");
            foreach (var node in wholecg.reachable)
                Console.WriteLine(node);
            Console.WriteLine("\n\n\n\n");
            Console.WriteLine("Printing interesting:");
            foreach (var node in wholecg.interesting)
                Console.WriteLine(node);
            Console.WriteLine("\n\n\n\n");

            Phx.FunctionUnit mainfunit = null;

            foreach (Phx.Unit fu in moduleUnit.ChildUnits)
            {
                if (fu is Phx.FunctionUnit)
                {
                    var f = fu.AsFunctionUnit;
                    if (f.FunctionSymbol.NameString.Equals("Main") || f.FunctionSymbol.NameString.Equals("main"))
                    {
                        mainfunit = f;
                    }
                }
            }

            if (mainfunit == null)
            {
                throw new System.Exception("Dll does not contain a main method");
            }

            //synthesise a dummy call instruction to main
            var mainname = PhxUtil.GetFunctionName(mainfunit.FunctionSymbol);
            var mainsig = PhxUtil.GetFunctionTypeSignature(mainfunit.FunctionSymbol.FunctionType);
            var containingType = PhxUtil.GetTypeName(mainfunit.FunctionSymbol.EnclosingAggregateType);
            var maincall = new StaticCall(29, 29, 29, null, mainname, containingType, mainsig, new List<string>());

            AddDummyRoot(wholecg, mainfunit, mainVertex, maincall);

            // Register sanity check delegates after adding dummy root
            wholecg.EdgeAdded += SanityCheckEdges;
            wholecg.VertexAdded += SanityCheckVertices;

            Queue<WholeCGNode> worklist = new Queue<WholeCGNode>();
            // State init = new State(root, mainfunit, maincall);
            worklist.Enqueue(mainVertex);           
            while (worklist.Any())
            {
                var node = worklist.Dequeue();

                if (PurityAnalysisPhase.EnableConsoleLogging)
                {
                    Console.WriteLine("Processing Node: " + node.id + " Name: " + node.Name);                    
                }

                PurityAnalysisData originalFact = null;
                if (wholecg.flowFact.ContainsKey(node))
                    originalFact = wholecg.flowFact[node];
                
                var outData = new HashSet<PurityAnalysisData>();

                // Begin by assuming that our node has a summary
                
                List<PurityAnalysisData> summaries = new List<PurityAnalysisData>();
                PopulateSummaries(moduleUnit, node, summaries);
                
                foreach (var inEdge in wholecg.InEdges(node).Where(x => wholecg.flowFact.ContainsKey(x.Source)))
                {
                    var callerUnit = inEdge.callerFUnit;
                    var incomingCall = inEdge.call;
                    var builder = new HigherOrderHeapGraphBuilder(callerUnit, false);
                    var incomingFact = wholecg.flowFact[inEdge.Source];
                    var data = incomingFact.Copy();
                    var outfact = data.Copy();
                    Call call = inEdge.call;

                    if (incomingCall.directCallInstruction != null)
                        call = new CallSummaryHandler().GetCallFromInstruction(incomingCall.directCallInstruction, outfact);

                    CallStubManager summan;
                    if (CallStubManager.TryGetCallStubManager(call, out summan))
                    {
                        var th = CombinedTypeHierarchy.GetInstance(moduleUnit);
                        var calleeSummary = summan.GetSummary(call, outfact, th);
                        builder.ComposeCalleeSummary(call, outfact, calleeSummary);

                        builder.ComposeResolvableSkippedCalls(outfact);
                        outfact.skippedCallTargets.UnionWith(builder.GetMergedTargets());
                        TranslateToCalleeNamespace(outfact, calleeSummary, call, node);
                        // outfact.SanityCheck();
                        outData.Add(outfact);
                    }
                    else
                    {
                        if (summaries.Count() == 0)
                        {
                            var calleeData = SummaryTemplates.GetUnanalyzableCallSummary(node.methodname, node.declaringType);
                            summaries.Add(calleeData);
                        }
                        foreach (var calleeSummary in summaries)
                        {                          
                            // var copy = calleeSummary.Copy();
                            var callerData = outfact.Copy();                           
                            builder.ComposeCalleeSummary(call, callerData, calleeSummary);

                            // resolve skipped calls
                            builder.ComposeResolvableSkippedCalls(callerData);
                            callerData.skippedCallTargets.UnionWith(builder.GetMergedTargets());
                            TranslateToCalleeNamespace(callerData, calleeSummary, call, node);
                            CleanLocalVars(callerData, node);
                            outData.Add(callerData);                           
                        }
                    }                   
                }

                var finalFact = new PurityAnalysisData(new HeapGraph());
                finalFact.JoinAllData(outData);

               

                if (originalFact == null || !finalFact.Equivalent(originalFact))
                {
                    finalFact.Union(originalFact);
                    wholecg.flowFact[node] = finalFact;                  
                    Console.WriteLine("Fact for {0} has {1} nodes and {2} edges", node, finalFact.OutHeapGraph.VertexCount, finalFact.OutHeapGraph.EdgeCount);
                    foreach (var outEdge in wholecg.OutEdges(node))
                    {
                        if (!worklist.Contains(outEdge.Target) && wholecg.interesting.Contains(outEdge.Target))
                            worklist.Enqueue(outEdge.Target);
                    }
                }              
                var progress = Math.Round(((methodsToAnalyse - worklist.Count()) * 100.0 / methodsToAnalyse), 1);

                //if (PurityAnalysisPhase.EnableConsoleLogging)
                    Console.WriteLine("Progress: {0}%", progress);

            }
           
            // Un-register sanity check delegates
            wholecg.EdgeAdded -= SanityCheckEdges;
            wholecg.VertexAdded -= SanityCheckVertices;                      
        }

        private static void PopulateSummaries(Phx.PEModuleUnit moduleUnit, WholeCGNode node, List<PurityAnalysisData> summaries)
        {            
            var sig = node.signature;
            var typeinfo = CombinedTypeHierarchy.GetInstance(moduleUnit).LookupTypeInfo(node.declaringType);
            var methodInfos = typeinfo.GetMethodInfos(node.methodname, sig);            
            foreach (var methodinfo in methodInfos)
            {
                var calleeSum = methodinfo.GetSummary();
                if (calleeSum == null)
                {
                    string qualifiedName = node.declaringType + "::" + node.methodname + "/" + sig;                    
                    MethodLevelAnalysis.unknownTargetCalls.Add(qualifiedName);

                    var calleeData = SummaryTemplates.GetUnanalyzableCallSummary(node.methodname, node.declaringType);
                    summaries.Add(calleeData);
                    continue;
                }
                summaries.Add(calleeSum);
            }                     
        }

        private void CleanLocalVars(PurityAnalysisData callerData, WholeCGNode node)
        {
            var fName = node.methodname;
            var decType = node.declaringType;
            //var unreachableVertices = from vertex in callerData.OutHeapGraph.Vertices
            //                          where !callerData.OutHeapGraph.InEdges(vertex).Any()
            //                                && !callerData.OutHeapGraph.OutEdges(vertex).Any()
            //                                && !(callerData.types[vertex].Contains("[mscorlib]System.Threading.Tasks.Task`1")
            //                                  || callerData.types[vertex].Contains("[mscorlib]System.Threading.Tasks.Task"))                                         
            //                          select vertex;
            //if (fName.Contains("CredentialsFromCache"))
            //{
            //    var filename = GeneralUtil.ConvertToFilename(fName+"/"+node.signature+node.id) + ".before";
            //    callerData.OutHeapGraph.DumpAsDGML(PurityAnalysisPhase.outputdir + filename + ".dgml");
            //}
            HashSet<HeapVertexBase> toRemove = new HashSet<HeapVertexBase>();
            foreach(var vertex in callerData.OutHeapGraph.Vertices)
            {
                //if (vertex is ReturnedValueVertex)
                //    toRemove.Add(vertex);
                //if (vertex is LoadHeapVertex)
                //{
                //    // only retain load heap vertices that are targets of m_builder fields
                //    bool flag = false;
                //    foreach(var inEdge in callerData.OutHeapGraph.InEdges(vertex))
                //    {
                //        if (inEdge.Field.ToString().Contains("m_builder"))
                //        {
                //            flag = true;
                //            break;
                //        }
                //    }
                //    if(flag == false)
                //        toRemove.Add(vertex);
                //}
                if (vertex is VariableHeapVertex)
                {
                    if (!(vertex as VariableHeapVertex).functionName.Contains(fName))
                    {
                        //Console.WriteLine("Removing VariableHeapVertex " + vertex + " in " + fName);
                        //Console.WriteLine((vertex as VariableHeapVertex).functionName);
                        toRemove.Add(vertex);
                    }
                }
                if (callerData.OutHeapGraph.IsOutEdgesEmpty(vertex) && !callerData.OutHeapGraph.InEdges(vertex).Any())
                {
                    if (vertex.ToString().Contains("_EXCEPTION"))
                        continue;
                    if (callerData.types.ContainsKey(vertex))
                    {
                        if (!(callerData.types[vertex].Contains("[mscorlib]System.Threading.Tasks.Task`1")
                                              || callerData.types[vertex].Contains("[mscorlib]System.Threading.Tasks.Task")))
                        {
                            toRemove.Add(vertex);
                        }
                    }
                    else
                        toRemove.Add(vertex);
                }
            }
            
            var skcallVertices = AnalysisUtil.GetSkcallVariables(callerData);

            var varsToRemove = toRemove.Except(skcallVertices);
            //var isolatedToRemove = unreachableVertices.Except(skcallVertices);
                      
            callerData.OutHeapGraph.RemoveVertices(varsToRemove);
            //callerData.OutHeapGraph.RemoveVertices(isolatedToRemove);
            //if (fName.Contains("CredentialsFromCache"))
            //{
            //    var filename = GeneralUtil.ConvertToFilename(fName + "/" + node.signature + node.id) + ".after";
            //    callerData.OutHeapGraph.DumpAsDGML(PurityAnalysisPhase.outputdir + filename + ".dgml");
            //}
        }

        private void TranslateToCalleeNamespace(PurityAnalysisData callerData, PurityAnalysisData calleeSummary, Call call, WholeCGNode target)
        {
            // Translate summary to callee namespace
            var vertexMap = MapCallerToCallee(call, callerData, calleeSummary, target);
            List<VariableHeapVertex> parameterVertices = new List<VariableHeapVertex>();
            var skcallVertices = AnalysisUtil.GetSkcallVariables(callerData);
            foreach(var kv in vertexMap)
            {
                //var targets = from edge in callerData.OutHeapGraph.OutEdges(kv.Key)
                //              select edge.Target;

                //foreach (var v in kv.Value)
                //    callerData.OutHeapGraph.AddVertex(v);

                //foreach (var t in targets)
                //    foreach (var v in kv.Value)
                //    {
                //        parameterVertices.Add(v as VariableHeapVertex);
                //        callerData.OutHeapGraph.AddEdge(new InternalHeapEdge(v, t, null));
                //    }
                if(!skcallVertices.Contains(kv.Key))
                    callerData.OutHeapGraph.RemoveVertex(kv.Key);
            }
                      
        }

        

        private void AddDummyRoot(WholeProgramCG wholecg, Phx.FunctionUnit mainFUnit, WholeCGNode root, Call call)
        {

            var dummyVertex = WholeCGNode.New("dummy/", "dummySig", "dummyNode", "DUMMY");
            wholecg.AddVertex(dummyVertex);

            var pd = new PurityAnalysisData(new HeapGraph());
            pd.OutHeapGraph.AddVertex(ExceptionVertex.GetInstance());
            pd.OutHeapGraph.AddVertex(GlobalLoadVertex.GetInstance());
            wholecg.flowFact[dummyVertex] = pd;
            wholecg.AddEdge(dummyVertex, root, call, dummyVertex.id, mainFUnit);           
        }

        public void PrintDataFlowFacts()
        {            
            var wholecg = Purity.Summaries.CalleeSummaryReader.wholecg;
            WholeCGNode root = wholecg.Vertices.Where(x => x.Name.Contains("::Main") || x.Name.Contains("::main")).First();
            var dfs = new DepthFirstSearchAlgorithm<WholeCGNode, WholeCGEdge>(wholecg);
            dfs.DiscoverVertex += (x) => {
                Console.WriteLine("Proccessing Node DFS " + x);
                if (wholecg.flowFact.ContainsKey(x))
                {

                    var f = wholecg.flowFact[x];
                    Console.WriteLine("++++++++++++++++++++++++++");
                    Console.WriteLine("Printing facts for " + x.Name);
                    int i = 0;
                    //foreach (var abstractGraph in f)
                    //{
                    // Console.WriteLine(abstractGraph.ToString());
                    var filename = GeneralUtil.ConvertToFilename(x.Name);
                    f.OutHeapGraph.DumpAsDGML(PurityAnalysisPhase.outputdir + filename + ".ohg" + ".dgml");
                    i++;
                    //}
                    Console.WriteLine("++++++++++++++++++++++++++");

                }
            };
            dfs.Compute(root);
        }
        
        private VertexMap MapCallerToCallee(
            Call call,
            PurityAnalysisData callerData,
            PurityAnalysisData calleeData,
            WholeCGNode target)
        {
            var mappedSet = new VertexMap();

            //map global load vertex to itself (this mapping can be removed without affecting correctness)
            //var glv = GlobalLoadVertex.GetInstance();
            //mappedSet.Add(glv, glv);

            FunctionSymbol funcSym;
            Dictionary<int, VariableHeapVertex> paramVertices = new Dictionary<int, VariableHeapVertex>();
            if (call.directCallInstruction != null)
            {
                funcSym = PhxUtil.NormalizedFunctionSymbol(call.directCallInstruction.FunctionSymbol);
                var calleeUnit = funcSym.FunctionUnit;
                if (calleeUnit != null)
                {
                    var paramOperandList = calleeUnit.FirstEnterInstruction.ExplicitDestinationOperands;
                    int count = 0;
                    foreach (Phx.IR.Operand operand in paramOperandList)
                    {
                        var id = NodeEquivalenceRelation.GetVariableId(operand);
                        var vertex = NodeEquivalenceRelation.CreateVariableHeapVertex(target.methodname, id, Context.EmptyContext, operand.Symbol.NameString);
                        paramVertices[count] = vertex;
                        count++;
                    }
                    if (funcSym.IsInstanceMethod)
                    {
                        var thisSym = calleeUnit.LookupThisParameter();
                        paramVertices[-1] = NodeEquivalenceRelation.CreateVariableHeapVertex(target.methodname, thisSym.Id, Context.EmptyContext);
                    }
                }
                else
                {
                    //var pSymbols = funcSym.ParameterSymbols;
                    //int count = 0;
                    //foreach(var p in pSymbols)
                    //{
                    //    var id = p.DefaultValueSymbol.LocalId;
                    //    var vertex = NodeEquivalenceRelation.CreateVariableHeapVertex(target.methodname, id, Context.EmptyContext, p.Name.NameString);
                    //    paramVertices[count] = vertex;
                    //    count++;
                    //}
                }
               
            }

            if (call is DelegateCall)
            {
                var dcall = call as DelegateCall;

                var delEdges = from edge in callerData.OutHeapGraph.OutEdges(dcall.GetTarget())
                               from deledge in callerData.OutHeapGraph.OutEdges(edge.Target)
                               select deledge;

                var targetMethods = from edge in delEdges
                                    where edge.Field.Equals(DelegateMethodField.GetInstance())
                                        && (edge.Target is MethodHeapVertex)
                                    select edge.Target as MethodHeapVertex;

                var isStatic = targetMethods.Any((MethodHeapVertex m) => (!m.IsInstance));
                var isInstance = targetMethods.Any((MethodHeapVertex m) => (m.IsInstance));

                if (isInstance)
                {
                    //match this with recvr vertex
                  
                    //var thisvertex = VariableHeapVertex.New(target.methodname, call.directCallInstrID, Context.EmptyContext);
                    if(paramVertices.ContainsKey(-1))
                        mappedSet.Add(dcall.GetTarget(), paramVertices[-1]);
                    MapCallerParametersInSequence(call, callerData, calleeData, 1, mappedSet, paramVertices);                    
                }

                if (isStatic)
                {
                    MapCallerParametersInSequence(call, callerData, calleeData, 0, mappedSet, paramVertices);
                }
            }
            else
                MapCallerParametersInSequence(call, callerData, calleeData, 0, mappedSet, paramVertices);
            return mappedSet;
        }

        private void MapCallerParametersInSequence(Call call, PurityAnalysisData callerData,
            PurityAnalysisData calleeData, int parameterCount, VertexMap mappedSet, Dictionary<int, VariableHeapVertex> paramVertices)
        {
            foreach (var param in call.GetAllParams())
            {
               
                if (!callerData.OutHeapGraph.ContainsVertex(param))
                {
                    Trace.TraceWarning("Argument {0} not present in caller", param);
                    continue;
                }

                
                //var calleeVertices =
                //        from paramVertex in calleeData.OutHeapGraph.Vertices.OfType<ParameterHeapVertex>()
                //        where paramVertex.Index == parameterCount
                //        select paramVertex;

                //if (!calleeVertices.Any())
                //{
                //    Trace.TraceWarning("No callee vertices for parameter #: " + parameterCount);
                //}

                if(paramVertices.ContainsKey(parameterCount))
                    mappedSet.Add(param, paramVertices[parameterCount]);
                parameterCount++;

            }

        }

        protected void ApplyTargetsSummary(
           Call call,
           PurityAnalysisData callerData,
           PurityAnalysisData calleeSummary,
           HigherOrderHeapGraphBuilder builder
           )
        {
            //skip this callee if there no way to come back to the caller from the callee. 
            //may happen when the callee terminates the program or throw an Exception unconditionally.             
            if (calleeSummary != null &&
                !calleeSummary.OutHeapGraph.IsVerticesEmpty)
            {
                var clonedCalleeData = AnalysisUtil.TranslateSummaryToCallerNamespace(
                    calleeSummary, new List<object> { call.instructionContext }, call.callingMethodnames, call.directCallInstrID);

                //add the return vertex to the data and mark it as strong update
                //if it is not added    
                if (call.HasReturnValue())
                {
                    var retvar = call.GetReturnValue();
                    if (PurityAnalysisPhase.FlowSensitivity &&
                        callerData.OutHeapGraph.ContainsVertex(retvar))
                    {
                        callerData.AddVertexWithStrongUpdate(retvar);
                    }
                }
                builder.ComposeCalleeSummary(call, callerData, clonedCalleeData);         
            }
        }

        /*
        private PurityAnalysisData ReadSummaryFromDatabase(PurityDBDataContext dbcontext, string typename, string methodname, string signature)
        {

            var reports = (from report in dbcontext.puritysummaries
                           where report.typename.Equals(typename)
                                     && report.methodname.Equals(methodname)
                                     && report.methodSignature.Equals(signature)
                           select report).ToList();
            int summaryCount = 0;
            var sumlist = new List<PurityAnalysisData>();
            foreach (var report in reports)
            {
                summaryCount++;
                //deserialize the summary
                if (report.purityData != null)
                {
                    MemoryStream ms = new MemoryStream(report.purityData.ToArray());
                    BinaryFormatter deserializer = new BinaryFormatter();
                    var sum = (PurityAnalysisData)deserializer.Deserialize(ms);
                    sumlist.Add(sum);
                    ms.Close();
                }
                else
                    Trace.TraceWarning("DB data for {0} is null", (report.typename + "::" + report.methodname + "/" + report.methodSignature));
            }

            if (sumlist.Any())
            {
                PurityAnalysisData summary = AnalysisUtil.CollapsePurityData(sumlist);
                return summary;
            }
            return null;
        }
        */



        /// <summary>
        /// Interacts with the user.
        /// </summary>
        /// <param name="moduleUnit"></param>
        private void Interact(Phx.PEModuleUnit moduleUnit)
        {
            while (true)
            {
                Console.WriteLine("Enter Function Symbol name (type \"exit\" to quit): ");
                string input = Console.ReadLine();
                if (input.Equals("exit"))
                    break;
                Phx.Graphs.NodeFlowOrder nodeOrder = Phx.Graphs.NodeFlowOrder.New(moduleUnit.Lifetime);
                nodeOrder.Build(moduleUnit.CallGraph, Phx.Graphs.Order.PostOrder);
                for (uint i = 1; i <= nodeOrder.NodeCount; i++)
                {
                    Phx.Graphs.CallNode node = nodeOrder.Node(i).AsCallNode;
                    if (node.FunctionSymbol.QualifiedName.Equals(input))
                    {
                        PurityAnalysisSummary summary = (PurityAnalysisSummary)
                            moduleUnit.CallGraph.SummaryManager.RetrieveSummary(node, PurityAnalysisSummary.Type);
                        if (summary != null)
                        {                            
                            var isConstructor = PhxUtil.IsConstructor(node.FunctionSymbol.NameString);
                            var report = PurityReportUtil.GetPurityReport(
                                node.FunctionSymbol.QualifiedName, 
                                PhxUtil.GetFunctionTypeSignature(node.FunctionSymbol.FunctionType),
                                summary.PurityData, isConstructor);

                            report.Dump();
                            summary.PurityData.OutHeapGraph.Dump();

                            //report.Print(this.outputFileWriter);
                            //display the heap graph as well
                            //this.outputFileWriter.WriteLine("Summary: ");
                            //this.outputFileWriter.WriteLine(summary.PurityData.OutHeapGraph.ToString());
                            //this.outputFileWriter.Flush();
                            //summary.PurityData.OutHeapGraph.DumpAsDGML("hg.dgml");
                        }
                    }
                }                
            }
        }

        public static void AttachSummary(Phx.FunctionUnit functionUnit, PurityAnalysisData data)
        {
            //Contract.Requires(functionUnit.FunctionSymbol.CallNode != null);
            //Contract.Requires(functionUnit.ParentPEModuleUnit.CallGraph != null);
            //Contract.Requires(data is PurityAnalysisData);

            Phx.Graphs.CallGraph callGraph = functionUnit.ParentPEModuleUnit.CallGraph;

            PurityAnalysisSummary puritySummary =
                PurityAnalysisSummary.New(callGraph.SummaryManager, data);

            //remove all summaries attached to this node and add the new summary
            callGraph.SummaryManager.RemoveAllSummary(functionUnit.FunctionSymbol.CallNode);
            callGraph.SummaryManager.PurgeSummary(functionUnit.FunctionSymbol.CallNode);
            bool summaryAdded = 
                callGraph.SummaryManager.AddSummary(functionUnit.FunctionSymbol.CallNode, puritySummary);
            
            //Contract.Assert(summaryAdded);
        }
        
    }
}
