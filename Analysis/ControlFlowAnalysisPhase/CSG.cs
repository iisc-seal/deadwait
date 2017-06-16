using QuickGraph;
using SafetyAnalysis.Framework.Graphs;
using SafetyAnalysis.Purity.ControlFlowAnalysisPhase;
using SafetyAnalysis.Purity.Summaries;
using SafetyAnalysis.Util;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace SafetyAnalysis.Purity
{
    public class CSG : QuickGraph.BidirectionalGraph<WholeCGNode, CSGEdgeBase>
    {
        // The following maps track information pertaining to the state machine expansion of MoveNext methods

        // init state to other states
        public Dictionary<WholeCGNode, HashSet<WholeCGNode>> moveNextTracker = new Dictionary<WholeCGNode, HashSet<WholeCGNode>>();
        // init state to final states
        public Dictionary<WholeCGNode, List<WholeCGNode>> finalStateTracker = new Dictionary<WholeCGNode, List<WholeCGNode>>();
        // original move next to init state
        Dictionary<int, WholeCGNode> initialStateTracker = new Dictionary<int, WholeCGNode>();

        // Map from node to thread, synchronous
        Dictionary<WholeCGNode, HashSet<String>> nodeToThreadSync = new Dictionary<WholeCGNode, HashSet<string>>();

        // Map from node to thread, asynchronous
        Dictionary<WholeCGNode, HashSet<String>> nodeToThreadAsync = new Dictionary<WholeCGNode, HashSet<string>>();

        // Set of MoveNext nodes in the CG. Used to maintain context sensitivity in the path computation
        HashSet<int> moveNextNodes = new HashSet<int>();

        // Hold the return variables of async procedures - used in applying the rule R4 from the paper
        List<Pair<VariableHeapVertex, WholeCGEdge>> asyncReturnVars;

        // Reachable nodes in the CallGraph
        private HashSet<WholeCGNode> reachable;

        // populate the nodes that are interesting to display to the user [graphically]
        private HashSet<WholeCGNode> interesting;

        public Dictionary<WholeCGNode, HashSet<String>> GetNodeToThreadMappingAsync()
        {
            return nodeToThreadAsync;
        }

        public Dictionary<WholeCGNode, HashSet<String>> GetNodeToThreadMappingSync()
        {
            return nodeToThreadSync;
        }
        
        public CSG()
        {
            Construct();
            PopulateNodeToThreadMapping();
        }

        public List<WholeCGNode> GetFinalStatesFor(WholeCGNode original)
        {
            if(initialStateTracker.ContainsKey(original.id))
                return finalStateTracker[initialStateTracker[original.id]];
            
            // modeling framework methods WhenAny etc
            var l = new List<WholeCGNode>();
            l.Add(original);
            return l;
        }

        public void AddCallBackEdge(WholeCGNode srcnode, WholeCGNode destnode, uint smVariable, VariableHeapVertex retVar)
        {
            
            if (!this.ContainsVertex(srcnode))
                this.AddVertex(srcnode);
            if (!this.ContainsVertex(destnode))
                this.AddVertex(destnode);
            var edge = new CallBackEdge(srcnode, destnode, smVariable, retVar);
            if (!this.ContainsEdge(edge))
                this.AddEdge(edge);            
        }

        public void AddCallGraphEdge(WholeCGNode srcnode, WholeCGNode destnode, Call call, int resolvingCallerID)
        {
            if (!this.ContainsVertex(srcnode))
                this.AddVertex(srcnode);
            if (!this.ContainsVertex(destnode))
                this.AddVertex(destnode);
            var edge = new CallGraphEdge(srcnode, destnode, call, resolvingCallerID);
            if (!this.ContainsEdge(edge))
                this.AddEdge(edge);
        }

        public void AddStateMachineEdge(WholeCGNode srcnode, WholeCGNode destnode, uint variableIndex, string configuration)
        {
            if (!this.ContainsVertex(srcnode))
                this.AddVertex(srcnode);
            if (!this.ContainsVertex(destnode))
                this.AddVertex(destnode);
            var edge = new StateMachineEdge(srcnode, destnode, variableIndex, configuration);
            if (!this.ContainsEdge(edge))
                this.AddEdge(edge);
        }

        public void DumpAsDGML(string filename)
        {
            //label of the vertices
            var labelmap = new Dictionary<WholeCGNode, string>();

            var finalStates = finalStateTracker.Values.SelectMany(i => i);

            //colour of the vertices
            var colormap = new Dictionary<WholeCGNode, string>();

            foreach (var v in Vertices)
            {
                string syn = nodeToThreadSync.ContainsKey(v) ? String.Join(",", nodeToThreadSync[v]) : "";
                string asyn = nodeToThreadAsync.ContainsKey(v) ? String.Join(",", nodeToThreadAsync[v]) : "";

                // this is the string denoting the node to thread mapping
                string suffix = "[" + syn + "]" + "[" + asyn + "]";
                if (v.Name.Contains("MoveNext") && !v.Name.Contains("::Enumerator::") && !finalStates.Contains(v))
                {
                    labelmap.Add(v, v.ToString() + suffix);
                    colormap.Add(v, "LightPink");
                }
                else if (v.Name.Contains("Start/") && v.Name.Contains("System.Runtime.CompilerServices.AsyncTaskMethodBuilder"))
                {
                    labelmap.Add(v, v.ToString() + suffix);
                    colormap.Add(v, "LightGreen");
                }
                else if (finalStates.Contains(v))
                {
                    labelmap.Add(v, v.ToString() + suffix);
                    colormap.Add(v, "Red");
                }
                else
                {
                    labelmap.Add(v, v.ToString());
                    colormap.Add(v, "White");
                }
            }

            WriteXML(filename, labelmap, colormap);
        }

        private void WriteXML(string filename, Dictionary<WholeCGNode, string> labelmap, Dictionary<WholeCGNode, string> colormap)
        {
            XmlWriter xmlWriter = XmlWriter.Create(PurityAnalysisPhase.outputdir + filename, new XmlWriterSettings() { Encoding = Encoding.UTF8 });
            xmlWriter.WriteStartDocument();
            xmlWriter.WriteStartElement("DirectedGraph", "http://schemas.microsoft.com/vs/2009/dgml");
            WriteNodes(labelmap, colormap, xmlWriter);
            WriteEdges(xmlWriter);
            xmlWriter.WriteEndElement(); // DirectedGraphs
            xmlWriter.WriteEndDocument();
            xmlWriter.Close();
        }

        private void WriteEdges(XmlWriter xmlWriter)
        {
            xmlWriter.WriteStartElement("Links");
            foreach (var edge in Edges)
            {
                if (!interesting.Contains(edge.Source) || !interesting.Contains(edge.Target))
                    continue;
                xmlWriter.WriteStartElement("Link");
                xmlWriter.WriteAttributeString("Source", edge.Source.id.ToString()); // ID! of the source node
                xmlWriter.WriteAttributeString("Target", edge.Target.id.ToString()); // ID of the target node 

                //format edge
                if (edge is CallGraphEdge)
                {
                    xmlWriter.WriteAttributeString("Stroke", "White");
                    xmlWriter.WriteAttributeString("StrokeThickness", "2");
                }
                else if (edge is StateMachineEdge)
                {
                    xmlWriter.WriteAttributeString("Stroke", "Red");
                    xmlWriter.WriteAttributeString("StrokeThickness", "4");
                    xmlWriter.WriteAttributeString("StrokeDashArray", "2, 2");
                }
                else
                {
                    xmlWriter.WriteAttributeString("Stroke", "Yellow");
                    xmlWriter.WriteAttributeString("StrokeDashArray", "5");
                    xmlWriter.WriteAttributeString("StrokeThickness", "2");
                }

                xmlWriter.WriteAttributeString("Label", edge.GetDescription());
                xmlWriter.WriteEndElement();
            }
            xmlWriter.WriteEndElement(); // Links            
        }

        private void WriteNodes(Dictionary<WholeCGNode, string> labelmap, Dictionary<WholeCGNode, string> colormap, XmlWriter xmlWriter)
        {
            xmlWriter.WriteStartElement("Nodes");
            foreach (var v in Vertices)
            {
                if (!interesting.Contains(v))
                    continue;
                xmlWriter.WriteStartElement("Node");
                xmlWriter.WriteAttributeString("Id", v.GetHashCode().ToString()); // id is an unique identifier of the node 
                xmlWriter.WriteAttributeString("Label", labelmap[v]); // label is the text on the node you see in the graph
                xmlWriter.WriteAttributeString("Background", colormap[v]);
                string syn = String.Join(",", nodeToThreadSync[v]);
                string asyn = nodeToThreadAsync.ContainsKey(v) ? String.Join(",", nodeToThreadAsync[v]) : "";
                xmlWriter.WriteAttributeString("Description", v.ToString() + "[" + syn + "]" + "[" + asyn + "]");
                xmlWriter.WriteEndElement();
            }

            xmlWriter.WriteEndElement(); // Nodes
        }

        private bool IsRoot(string name)
        {
            return name.Contains("::Start/")
                || name.Contains("Async/")
                || name.Contains("[State")
                || name.Contains("::set_Result/")
                || name.Contains("Task`1::get_Result/")
                || name.Contains("Task::get_Result/")
                || name.Contains("::MoveNext/()void")
                || name.Contains("::Wait/")
                || name.Contains("::SetResult/")
                || name.Contains("Task::Delay/")
                || name.Contains("::WhenAny/")
                || name.Contains("::WhenAll/")
                || name.Contains("Task::Run/")
                || name.Contains("Factory::StartNew/")
                || name.Contains("Task`1::ctor/")
                ;
        }

        private HashSet<WholeCGNode> PopulateInteresting()
        {
            reachable = CalleeSummaryReader.wholecg.reachable;
            var roots = from v in Vertices where IsRoot(v.Name)
                        && reachable.Contains(v) 
                        select v;
            interesting = new HashSet<WholeCGNode>(roots);
            foreach (var e in Edges.OfType<StateMachineEdge>())
            {
                interesting.Add(e.Source);
                interesting.Add(e.Target);
            }

            foreach(var v in interesting.ToList())
            {
                foreach(var e in InEdges(v))
                {
                    PullInVertices(e.Source, interesting);
                }
            }    

            return interesting;
        }

        void PullInVertices(WholeCGNode root, HashSet<WholeCGNode> visited)
        {
            if (visited.Contains(root) || (!reachable.Contains(root) && !root.Name.Contains("DummyAsync")))
                return;
            visited.Add(root);
            foreach(var e in InEdges(root))
            {
                PullInVertices(e.Source, visited);
            }
        }

        // All the methods of this class assume that Purity.Summaries.CalleeSummaryReader.wholecg
        // and its fields have been populated, also the static fields of StateMachineAnalysis
        // Call this methods only after the top down phase has finished
        private void Construct()
        {
            var wholecg = Purity.Summaries.CalleeSummaryReader.wholecg;

            // 1. Add the call-graph edges
            foreach (var edge in wholecg.Edges)
            {
                this.AddCallGraphEdge(edge.Source, edge.Target, edge.call, edge.resolvingCallerID);
            }

            // 2. Inline state machines
            InlineStateMachines();

            asyncReturnVars = AliasQueries.GetReturnVariablesForAsync().ToList();
            
            // 3. Add Callback edges
            PopulateCallBackEdges();
           
            // Compute the nodes which are interesting to display to the user
            PopulateInteresting();
        }

        private void PopulateCallBackEdges()
        {            
            var wholecg = Purity.Summaries.CalleeSummaryReader.wholecg;
            HashSet<WholeCGNode> toDel = new HashSet<WholeCGNode>();
            foreach (var kv in moveNextTracker)
            {
                var states = kv.Value;
                foreach (var state in states)
                {
                    IEnumerable<CSGEdgeBase> inEdges;
                    this.TryGetInEdges(state, out inEdges);
                    if (inEdges != null)
                    {
                        foreach (var edge in inEdges.OfType<StateMachineEdge>().ToList())
                        {
                            FindAndTrackCallbacks(edge, kv.Key);
                        }
                    }
                }
            }
        }

        private void FindAndTrackCallbacks(StateMachineEdge e, WholeCGNode init)
        {
            uint varIndex = e.variableIndex;
            // 1.2 Obtain the variable associated with the incoming edge and find methods whose task aliases
            // 1.3 Find corresponding MoveNext method
            // 1.4 Get the final states of that MoveNext, and add edges to this one

            var callingBackMethods = GetMethodsWithAliasedReturn(varIndex, init);
            var WhenAnyOrWhenAllMethods = GetModeledSources(varIndex, init).Distinct();
            foreach (var representative in callingBackMethods.Union(WhenAnyOrWhenAllMethods))
            {
                if (finalStateTracker.ContainsKey(representative.Key))
                {
                    var finalStates = finalStateTracker[representative.Key];
                    foreach (var fs in finalStates)
                    {
                        this.AddCallBackEdge(fs, e.Target, varIndex, representative.Value);
                    }
                }
                else
                {
                    // This is a framework async method, and we did not expand it to a state machine
                    this.AddCallBackEdge(representative.Key, e.Target, varIndex, representative.Value);
                }
            }
        }

        private IEnumerable<Pair<WholeCGNode, VariableHeapVertex>> GetModeledSources(uint varIndex, WholeCGNode init)
        {
            Console.WriteLine("Callbacks for " + init);
            VariableHeapVertex v = AliasQueries.GetVertexForVariable(varIndex, init);
            var modeledEdges = AliasQueries.GetModeledCalls();
            foreach (var edge in modeledEdges)
            {
                var retVar = AliasQueries.GetReturnSourceOperandVertex(edge);
                if (AliasQueries.MayAlias(v, init, retVar, edge.Source).Key)
                {
                    var param = edge.call.GetAllParams().First();
                    var arraySources = AliasQueries.GetArraySources(param, edge.Source);
                    foreach (var rv in asyncReturnVars)
                    {
                        foreach (var p in arraySources)
                        {
                            if (AliasQueries.MayAlias(p, edge.Source, rv.Key, rv.Value.Source).Key)
                            {
                                yield return new Pair<WholeCGNode, VariableHeapVertex>(GetMoveNextFor(rv.Value), rv.Key);
                            }
                        }
                    }
                }
            }
        }

        // Replace MoveNext methods by their state machines
        // The state machine corresponding to a MoveNext method 'methodName' is obtained using
        // StateMachineAnalysis.transitions[methodName] for the transitions etc
        private void InlineStateMachines()
        {
            var wholecg = Purity.Summaries.CalleeSummaryReader.wholecg;
            HashSet<WholeCGNode> toDel = new HashSet<WholeCGNode>();
            foreach (var methodName in StateMachineAnalysis.transitions.Keys)
            {                
                if(PurityAnalysisPhase.EnableConsoleLogging)
                    Console.WriteLine("Inlining state machine for {0}", methodName);
                var transitionRelation = StateMachineAnalysis.transitions[methodName];
                var sM = StateMachineAnalysis.callIdToState[methodName];
                var finalStates = StateMachineAnalysis.finalStates[methodName];
                var vertices = GetVertexFor(methodName);
                foreach (var moveNextVertex in vertices)
                {
                    if (!wholecg.reachable.Contains(moveNextVertex))
                        continue;
                    toDel.Add(moveNextVertex);
                    var name = moveNextVertex.Name;
                    WholeCGNode init = null;

                    // add the states and transitions, fix up incoming and outgoing edges
                    var states = AddStateMachine(transitionRelation, moveNextVertex, finalStates, out init); 
                    AddIncomingEdges(moveNextVertex, init);                    
                    AddOutgoingEdges(sM, moveNextVertex, states, init);                    
                }
            }
           
            foreach (var d in toDel)
                this.RemoveVertex(d);            
        }
       
        private void AddOutgoingEdges(Dictionary<uint, HashSet<int>> instrToStates, WholeCGNode moveNextVertex, 
            Dictionary<int, WholeCGNode> states, WholeCGNode init)
        {            
            IEnumerable<CSGEdgeBase> outEdges;
            this.TryGetOutEdges(moveNextVertex, out outEdges);
            if (outEdges != null)
            {
                foreach (var edge in outEdges)
                {                    
                    var cEdge = edge as CallGraphEdge;
                    var origins = GetOriginVertices(cEdge, instrToStates, states);
                    foreach (var origin in origins)
                    {
                        // var tag = new CallGraphEdge(label, edge.Tag.Key, edge.Tag.Value);
                        var e = new CallGraphEdge(origin, cEdge.Target, cEdge.call, cEdge.resolvingCallerID);
                        this.AddEdge(e);
                        // Every state calling SetResult must be a final state
                        // Fix up discrepencies in the final states computed earlier, if necessary
                        if (edge.Target.Name.Contains("SetResult") && edge.Target.Name.Contains("Builder"))
                        {
                            if (!finalStateTracker[init].Contains(origin))
                            {
                                Console.WriteLine("FISHY!");
                                finalStateTracker[init].Add(origin);
                            }
                        }
                    }                  
                }
            }          
        }

        private static HashSet<WholeCGNode> GetOriginVertices(CallGraphEdge edge, Dictionary<uint, HashSet<int>> instrToStates, 
            Dictionary<int, WholeCGNode> states)
        {
            var origins = instrToStates[edge.call.directCallInstrID];  // Get states for this call instruction
            HashSet<WholeCGNode> oVertices = new HashSet<WholeCGNode>();            
            foreach (var v in origins)
                oVertices.Add(states[v]);   // Get the vertices corresponding to these states
            return oVertices;
        }

      
        private void AddIncomingEdges(WholeCGNode v, WholeCGNode init)
        {            
            IEnumerable<CSGEdgeBase> inEdges;
            this.TryGetInEdges(v, out inEdges);
            if (inEdges != null)
            {
                foreach (var edge in inEdges.ToList())
                {
                    var e = edge as CallGraphEdge;
                    this.AddCallGraphEdge(e.Source, init, e.call, e.resolvingCallerID);                    
                }
            }            
        }

        /* 
         * Returns a dictionary, that for the method passed in, maps the states of the state machine
         * to the CSG vertices added to represent them
         */
        private Dictionary<int, WholeCGNode> AddStateMachine(AdjacencyGraph<int, TaggedEdge<int, Pair<string, uint>>> transitionRelation, 
            WholeCGNode moveNextVertex, HashSet<int> finalStates, out WholeCGNode init)
        {
            var wholecg = Purity.Summaries.CalleeSummaryReader.wholecg;
            Dictionary<int, WholeCGNode> states = new Dictionary<int, WholeCGNode>();
         
            HashSet<WholeCGNode> stateIDs = new HashSet<WholeCGNode>();
            HashSet<WholeCGNode> finalStateNodes = new HashSet<WholeCGNode>();
            
            foreach (var smVertex in transitionRelation.Vertices)
            {
                var stateString = getStateString(moveNextVertex.Name, smVertex);
                WholeCGNode newVert = WholeCGNode.New(stateString, moveNextVertex.signature, moveNextVertex.declaringType, moveNextVertex.methodname);
                this.AddVertex(newVert);                
                states[smVertex] = newVert;                
                stateIDs.Add(newVert);    
            }            
                                   
            init = states[-1];
            wholecg.flowFact[init] = wholecg.flowFact[moveNextVertex];
            initialStateTracker[moveNextVertex.id] = init;
            finalStateTracker[init] = (from s in finalStates select states[s]).ToList();
            moveNextNodes.Add(moveNextVertex.id);
            moveNextTracker[init] = stateIDs;
            foreach (var edge in transitionRelation.Edges)
            {                
                var e = new StateMachineEdge(states[edge.Source], states[edge.Target], edge.Tag.Value, edge.Tag.Key);
                this.AddEdge(e);
            }
            return states;
        }

        private static string getStateString(string methodName, int smVertex)
        {
            return methodName + String.Format("[State::{0}]", smVertex);
        }

        private static IEnumerable<WholeCGNode> GetVertexFor(string methodName)
        {
            var wholecg = Purity.Summaries.CalleeSummaryReader.wholecg;
            var v = wholecg.Vertices.Where(x => x.Name.Contains(methodName));
            if (!v.Any())
            {                
                throw new ArgumentException("Vertex not found! " + methodName);
            }
            if (v.Count() > 1)
            {                
                Console.WriteLine(">1 vertex found for {0}" + methodName);
            }
            return v;
        }
                           
        private IEnumerable<Pair<WholeCGNode, VariableHeapVertex>> GetMethodsWithAliasedReturn(uint varIndex, WholeCGNode init)
        {
            //Console.WriteLine("Callbacks for " + init);
            VariableHeapVertex v = AliasQueries.GetVertexForVariable(varIndex, init);
           
            foreach (var retVar in asyncReturnVars)
            {
                var returnVariable = retVar.Key;
                var edge = retVar.Value;
                if(AliasQueries.MayAlias(v, init, returnVariable, edge.Source).Key)
                {
                    yield return new Pair<WholeCGNode, VariableHeapVertex>(GetMoveNextFor(edge), returnVariable);
                }                
            }
        }

        public IEnumerable<AliasingPair> GetModeledPairs()
        {
            var blockingCalls = AliasQueries.GetBlockingTaskReceivers();
            var modeledCalls = AliasQueries.GetModeledCalls();
            foreach (var a in blockingCalls)
            {
                foreach (var edge in modeledCalls)
                {
                    // the variable holding the return value of WhenAny/WhenAll
                    var retVar = AliasQueries.GetReturnSourceOperandVertex(edge);

                    // The blocking call is WhenAny().Result or WhenAll().Result
                    if (AliasQueries.MayAlias(a.Key, a.Value, retVar, edge.Source).Key)
                    {
                        // obtain the task[] passed into WhenAny/WhenAll 
                        var param = edge.call.GetAllParams().First();

                        // what variables point into this heap location?
                        var arraySources = AliasQueries.GetArraySources(param, edge.Source);

                        // do any of these variables alias with the value returned by an async method?
                        // if yes, consider it a candidate blocking/signaling pair
                        foreach (var rv in asyncReturnVars)
                        {
                            foreach (var p in arraySources)
                            {
                                var mayAlias = AliasQueries.MayAlias(p, edge.Source, rv.Key, rv.Value.Source);
                                if (mayAlias.Key)
                                {
                                    if(!AliasQueries.seen.Contains(new Pair<WholeCGNode, WholeCGNode>(a.Value, rv.Value.Target)))
                                        yield return new AliasingPair(a.Value, rv.Value.Target, mayAlias.Value);
                                }
                            }
                        }
                    }
                }
            }
        }

        private WholeCGNode GetMoveNextFor(WholeCGEdge edge)
        {
            // Case 1: This async method was analyzed      
            IEnumerable<CSGEdgeBase> outEdges;
            TryGetOutEdges(edge.Source, out outEdges);
            if (outEdges != null)
            {
                foreach (var e in outEdges)
                {
                    if (e.Target.Name.Contains("Start") && e.Target.Name.Contains("MethodBuilder"))
                    {
                        IEnumerable<CSGEdgeBase> edges;
                        TryGetOutEdges(e.Target, out edges);
                        if (edges != null)
                        {
                            var moveNextEdges = edges;
                            Trace.Assert(moveNextEdges.Count() == 1);
                            return moveNextEdges.First().Target;
                        }
                    }
                }
            }
            
            // Case 2: Framework async method - we won't see the generated MoveNext
            return edge.Target;
        }

        // Base cases for the node to thread mapping
        public IEnumerable<WholeCGNode> GetRoots()
        {
            return this.Vertices.Where(x => x.Name.Contains("::Main/") 
            || x.Name.Contains("::main/") || x.ToString().Contains("System.Threading.Thread::Start/"));
        }

        private void PopulateNodeToThreadMapping()
        {
            //Dictionary<WholeCGNode, HashSet<string>> threadMap = new Dictionary<WholeCGNode, HashSet<string>>();
            var worklist = new MRWorklist<WholeCGNode>();
            var roots = GetRoots();
            var tPool = new HashSet<string>();
            tPool.Add("tp");
            int i = 0;

            // 1. Set up roots
            foreach(var root in roots)
            {
                var rootThreads = new HashSet<string>();                
                if (root.Name.Contains("::Main/") || root.Name.Contains("::main/"))
                {
                    rootThreads.Add("tm");
                }
                else
                {
                    rootThreads.Add("t" + i++);
                }
                nodeToThreadSync[root] = rootThreads;
                worklist.Enqueue(root);     
            }
            
            // 2. Iterate till fixpoint            
            while (worklist.Any())
            {
                var current = worklist.Dequeue();

                // 2.1 Synchronous edges
                foreach (var edge in OutEdges(current).OfType<CallGraphEdge>())
                {
                    var target = edge.Target;
                    UpdateIfChanged(worklist, current, target);                    
                }

                // 2.2 State machine edges.
                foreach (var edge in OutEdges(current).OfType<StateMachineEdge>())
                {                    
                    var target = edge.Target;                    
                    // 2.2.1 Configured Awaiter
                    if (edge.configuration.CompareTo("d") == 0)
                    {
                        if (nodeToThreadAsync.ContainsKey(target))
                        {
                            // this node had another predecessor, possibly with an unconfigured transition
                            // So union to be conservative
                            nodeToThreadAsync[target].UnionWith(tPool);
                        }
                        else
                            nodeToThreadAsync[target] = tPool;
                        UpdateIfChanged(worklist, current, target);
                    }
                    else   // 2.2.2 Await(er) not configured                 
                    {
                        UpdateIfChanged(worklist, current, target);
                        nodeToThreadAsync[target] = nodeToThreadSync[target];
                    }
                }
            }
            //return threadMap;
        }

        // Will update only nodeToThreadSync for target
        // Current could either be running synchronously or asynchronously
        // In either case, the target should get all the associated threads
        private void UpdateIfChanged(MRWorklist<WholeCGNode> worklist, WholeCGNode current, WholeCGNode target)
        {
            var existing = new HashSet<string>(nodeToThreadSync[current]);
            if (nodeToThreadAsync.ContainsKey(current))
                existing.UnionWith(nodeToThreadAsync[current]);

            if (!nodeToThreadSync.ContainsKey(target))
            {
                nodeToThreadSync[target] = existing;
                worklist.Enqueue(target);
            }
            else if (!existing.IsSubsetOf(nodeToThreadSync[target]))
            {
                nodeToThreadSync[target].UnionWith(existing);
                worklist.Enqueue(target);
            }
        }

        public HashSet<WholeCGNode> GetSources(WholeCGNode v)
        {
            var vertexSet = new HashSet<WholeCGNode>();
            reverseDFS(v, vertexSet);
            return vertexSet;
        }

        public void reverseDFS(WholeCGNode root, HashSet<WholeCGNode> visited)
        {
            if (visited.Contains(root))
                return;
            visited.Add(root);
            foreach(var v in InEdges(root))
            {
                reverseDFS(v.Source, visited);
            }
        }

        public bool existsInterestingPath(WholeCGNode src, WholeCGNode dest, List<CSGEdgeBase> path, Dictionary<WholeCGNode, HashSet<string>> labels, Context signalingContext, HashSet<WholeCGNode> sources)
        {
            // path.Add(src);
            if (src.Equals(dest))
            {
                if (isInterestingPath(path, labels, signalingContext))
                    return true;                
            }

            foreach (var edge in this.OutEdges(src))
            {
                if (edge is StateMachineEdge)
                    continue;

                if (!path.Select(x => x.Target).Contains(edge.Target))
                {
                    if (edge is CallGraphEdge)
                    {
                        // Our source is not reachable from the target
                        // of the current edge, no point exploring this path further
                        if (!sources.Contains(edge.Target))
                            continue;
                        if (!ObeysContext(path, edge as CallGraphEdge))
                            continue;
                    }
                    path.Add(edge);
                    if (existsInterestingPath(edge.Target, dest, path, labels, signalingContext, sources))
                        return true;
                    path.Remove(edge);
                }
            }            
            return false;
        }

        private bool isInterestingPath(List<CSGEdgeBase> path, Dictionary<WholeCGNode, HashSet<string>> labels, Context signalingContext)
        {
            // see if the current path agrees with the signalingContext
            // the #deadlock reports we obtain are the same even without this,
            // but this gives us better error reports
            // TODO: define a notion of "async-valid" path, similar to interprocedurally valid paths
            // where a suspended call returns to the corresponding continuation - use that to get
            // still better reports
            if (!SignalingPathRespectsContext(path, signalingContext))
                return false;

            var continuations = from e in path.OfType<CallBackEdge>()
                                select e.Target;

            foreach (var n in continuations)
            {
                if (labels[n].Contains("tm"))
                {
                    Console.WriteLine("<* Interesting because of " + n + " ");
                    foreach (var t in labels[n])
                        Console.Write("\t " + t);
                    Console.WriteLine("*>");
                    return true;
                }
            }

            return false;
        }

        private bool SignalingPathRespectsContext(List<CSGEdgeBase> path, Context signalingContext)
        {
            var calls = from e in path.OfType<CallGraphEdge>() where e.call != null select e.call.instructionContext;

            // the call instruction ids seen on this path
            var ids = new HashSet<uint>(calls);

            foreach (uint curr in signalingContext.list)
            {
                if (AnalysisUtil.idToCall.ContainsKey(curr))
                {
                    if (IsTaskRelated(curr))
                    {
                        var origins = Edges.OfType<CallGraphEdge>().Where(x => x.call != null && x.call.instructionContext == curr).Select(e => e.Source.id);
                        // the calls to one of the methods in the if block must agree with what was seen in the signaling context,
                        // otherwise, this points-to context is inconsistent with our path
                        //if (origins.Any() && !path.Select(x => x.Source.id).Contains(origins.First().id))
                        if (origins.Any() && !path.Select(x => x.Source.id).Intersect(origins).Any())
                            return false;
                        continue;
                    }
                    //if (AnalysisUtil.idToCall[curr].Contains("&Invoke"))
                    //{
                    //    var origins = Edges.OfType<CallGraphEdge>().Where(x => x.call != null && x.call.instructionContext == curr).Select(e => e.Source.id);
                    //    if (origins.Any() && !path.Select(x => x.Source.id).Intersect(origins).Any())
                    //        return false;
                    //    continue;
                    //}
                    if (!ids.Contains(curr))
                    {
                        return false;
                    }
                }
            }
            return true;
        }

        private static bool IsTaskRelated(uint curr)
        {
            return AnalysisUtil.idToCall[curr].Contains("AwaitUnsafeOnCompleted") ||
                                    AnalysisUtil.idToCall[curr].Contains("MethodBuilder") ||
                                    AnalysisUtil.idToCall[curr].Contains("&get_Task") ||
                                    AnalysisUtil.idToCall[curr].Contains("&Invoke") ||
                                    AnalysisUtil.idToCall[curr].Contains("::get_Task");
        }


        // Checks if a path obeys context - for every indirectly resoved call, the "resolver"
        // must have occurred earlier on the path
        public bool ObeysContext(List<CSGEdgeBase> edges, CallGraphEdge currentEdge)
        {
            // If this is a direct call           
            if (currentEdge.call == null || currentEdge.resolvingCallerID == currentEdge.Source.id)
                return true;

            // This context refers to a MoveNext method. So we should look for a predecessor that is its state
            var resolvingContext = currentEdge.resolvingCallerID; // caller that resolved this edge
            var resolvingCallInstrId = currentEdge.call.resolvingCallInstrID; // call instr in that caller
            if (moveNextNodes.Contains(resolvingContext))
            {
                var states = moveNextTracker[initialStateTracker[resolvingContext]].Select(x => x.id).ToList();

                // This is a direct call out of a move next method
                // Therefore, the source should now be a state
                if (states.Contains(currentEdge.Source.id))
                    return true;

                // Case where an edge not out of MoveNext got resolved in the context of a moveNext method
                for (int i = edges.Count() - 1; i >= 0; i--)
                {
                    // the edge tag will still refer to the MoveNext method's id
                    var e = edges[i] as CallGraphEdge;
                    if(e != null)
                        if (states.Contains(e.Source.id)) // && e.call.directCallInstrID == resolvingCallInstrId))
                            return true;
                }
            }
            else {
                for (int i = edges.Count() - 1; i >= 0; i--)
                {
                    var e = edges[i] as CallGraphEdge;
                    if (e != null)
                        if (e.Source.id == resolvingContext) // && e.call.directCallInstrID == resolvingCallInstrId)
                            return true;
                }
            }            
            return false;
        }
    }           
}
