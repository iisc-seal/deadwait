using SafetyAnalysis.Framework.Graphs;
using SafetyAnalysis.Purity.Summaries;
using SafetyAnalysis.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Phx;
using System.Diagnostics;

namespace SafetyAnalysis.Purity
{

    /* 
     * All the methods of this class assume that 
     * Purity.Summaries.CalleeSummaryReader.wholecg and its fields 
     * have been populated by the top down phase
     * Call the methods of this class only after the top down phase has finished 
     */

    public class AliasQueries
    {
        public static List<string> asyncMethod = new List<string>();
        public static WholeProgramCG wholecg = Purity.Summaries.CalleeSummaryReader.wholecg;
        public static HashSet<Pair<WholeCGNode, WholeCGNode>> seen;

        public static WholeCGNode GetNodeForFunction(int functionId)
        {            
            var node = from vertex in wholecg.Vertices where vertex.id == functionId select vertex;
            if (node.Any())
                return node.First();
            throw new System.ArgumentException("FunctionId " + functionId + " Not found in call graph");
        }

        /*
         * Get the vertex in the heap graph for local variable identified by variableIdex 
         * at function exit for the function identified by node
         */
        public static VariableHeapVertex GetVertexForVariable(uint variableIndex, WholeCGNode node)
        {            
            var g = wholecg.flowFact[node];
            var v = from vertex in g.OutHeapGraph.Vertices.OfType<VariableHeapVertex>()
                    where vertex.index == variableIndex && node.Name.Contains(vertex.functionName) && vertex.context.Count == 0
                    select vertex;
            if (!v.Any())
            {
                Console.WriteLine("Variable {0} not found in function {1}", variableIndex, node);
                throw new System.ArgumentException("Variable not found! " + variableIndex + " " + node);
            }
            return v.First();
        }

        /*
         * Return VariableHeapVertices that point to array locations 
         */
        public static IEnumerable<VariableHeapVertex> GetArraySources(VariableHeapVertex v, WholeCGNode node)
        {            
            if (!wholecg.flowFact.ContainsKey(node))
            {
                Console.WriteLine("Querying unreachable node " + node);
                return Enumerable.Empty<VariableHeapVertex>();
            }

            var abstractGraph = wholecg.flowFact[node];
            
            var targets = from edge in abstractGraph.OutHeapGraph.OutEdges(v)            
                          from fieldEdge in abstractGraph.OutHeapGraph.OutEdges(edge.Target)
                          where fieldEdge.Field is RangeField
                          select fieldEdge.Target as VertexWithContext;

            var inVertices = from t in targets 
                             from edge in abstractGraph.OutHeapGraph.InEdges(t)
                             where edge.Source is VariableHeapVertex
                             select edge.Source as VariableHeapVertex;

            return inVertices.Distinct();
        }

        // What are the targets for v in function node?
        public static IEnumerable<VertexWithContext> GetPointsTo(VariableHeapVertex v, WholeCGNode node)
        {            
            if(!wholecg.flowFact.ContainsKey(node))
            {
                Console.WriteLine("Querying unreachable node " + node);
                return Enumerable.Empty<VertexWithContext>();
            }
            var abstractGraph = wholecg.flowFact[node];
            return GetPointsTo(v, abstractGraph);
        }

        public static IEnumerable<VertexWithContext> GetPointsTo(VariableHeapVertex v, PurityAnalysisData abstractGraph)
        {                        
            var pointsTo = new HashSet<InternalHeapVertex>();
            var targets = from edges in abstractGraph.OutHeapGraph.Edges
                          where edges.Source.Equals(v) && edges.Target is InternalHeapVertex
                          select edges.Target as VertexWithContext;

            return targets;
        }
        
        // What vertices represent basevar->fieldName?
        public static IEnumerable<VertexWithContext> GetLabelledPointsTo(HeapVertexBase baseVar, String fieldName, WholeCGNode node)
        {            
            var abstractGraph = wholecg.flowFact[node];
            var fieldTargets = from edge in abstractGraph.OutHeapGraph.OutEdges(baseVar)                              
                               where edge.Field.ToString().Contains(fieldName) && edge.Target is InternalHeapVertex                               
                               select edge.Target as VertexWithContext;
            return fieldTargets.Distinct();
        }

        // Seal creates an extra level of indirection for certain struct fields - these become important for us
        // in the case of m_task (of AsyncTaskMethodBuilder) and m_builder
        public static IEnumerable<VertexWithContext> GetEncapsulatedTaskTargets(VariableHeapVertex baseVar, WholeCGNode node)
        {            
            var abstractGraph = wholecg.flowFact[node];

            // Case 1 - baseVar points to an AsyncTaskMethodBuilder<T> 
            var fieldTargets = from edge in abstractGraph.OutHeapGraph.OutEdges(baseVar)
                               where !abstractGraph.OutHeapGraph.IsOutEdgesEmpty(edge.Target)
                               from fieldEdge in abstractGraph.OutHeapGraph.OutEdges(edge.Target)
                               where fieldEdge.Field.ToString().Contains("::m_task") 
                               select fieldEdge.Target as VertexWithContext;

            // Case 2 - baseVar points to an AsyncTaskMethodBuilder<VoidTaskStruct>
            var pointsTo = abstractGraph.OutHeapGraph.OutEdges(baseVar).Select(x => x.Target);
            var mBuilderTargets = from x in pointsTo
                                  from edge in abstractGraph.OutHeapGraph.OutEdges(x)
                                  where edge.Field.ToString().Contains("m_builder")
                                  select edge.Target;

            var mTaskTargets = from x in mBuilderTargets
                               from edge in abstractGraph.OutHeapGraph.OutEdges(x)
                               where edge.Field.ToString().Contains("::m_task") 
                               select edge.Target as VertexWithContext;

            return (fieldTargets.Union(mTaskTargets)).Distinct();
        }

        public static IEnumerable<VertexWithContext> GetFieldTargets(VertexWithContext baseVar, String fieldName, WholeCGNode node)
        {            
            var abstractGraph = wholecg.flowFact[node];
            
            var fieldTargets = from edge in abstractGraph.OutHeapGraph.OutEdges(baseVar) where !abstractGraph.OutHeapGraph.IsOutEdgesEmpty(edge.Target) 
                               from fieldEdge in abstractGraph.OutHeapGraph.OutEdges(edge.Target)
                               where fieldEdge.Field.ToString().Contains(fieldName) && fieldEdge.Target is InternalHeapVertex
                               select fieldEdge.Target as VertexWithContext;
            return fieldTargets.Distinct();
        }

        public static IEnumerable<VertexWithContext> GetAllFieldTargets(VertexWithContext baseVar, String fieldName, WholeCGNode node)
        {            
            var abstractGraph = wholecg.flowFact[node];

            var fieldTargets = from edge in abstractGraph.OutHeapGraph.OutEdges(baseVar)
                               where !abstractGraph.OutHeapGraph.IsOutEdgesEmpty(edge.Target)
                               from fieldEdge in abstractGraph.OutHeapGraph.OutEdges(edge.Target)
                               where fieldEdge.Field.ToString().Contains(fieldName) // && fieldEdge.Target is InternalHeapVertex
                               select fieldEdge.Target as VertexWithContext;
            return fieldTargets.Distinct();
        }

        public static IEnumerable<Pair<VariableHeapVertex, WholeCGNode>> GetSignalingReceivers()
        {            
            var signalingMethods = from node in wholecg.Vertices 
                                   where node.Name.Contains("SetResult/") && node.Name.Contains("System.Runtime.CompilerServices.AsyncTaskMethodBuilder")
                                   select node;

            var signalingReceivers = from edge in wholecg.Edges
                                     where signalingMethods.Contains(edge.Target) && wholecg.interesting.Contains(edge.Source) && edge.call != null && edge.call.GetParamCount() > 0
                                     select new Pair<VariableHeapVertex, WholeCGNode>(edge.call.GetParam(0), edge.Source);

            return signalingReceivers.Distinct();
        }

        // Modeling WhenAny, WhenAll, TaskFactory::StartNew
        //public static IEnumerable<ModelData> GetFrameworkSignals()
        //{            
        //    var signalingMethods = from node in wholecg.Vertices
        //                           where node.Name.Contains("SetResult/") && node.Name.Contains("System.Runtime.CompilerServices.AsyncTaskMethodBuilder")
        //                           select node;

        //    var modeledMethods = from edge in wholecg.Edges
        //                         where signalingMethods.Contains(edge.Target) && wholecg.interesting.Contains(edge.Source) && edge.call == null
        //                         select edge.Source;

        //    var signalingReceivers = from method in modeledMethods
        //                             from edge in wholecg.InEdges(method) where edge.call.GetParamCount() > 0
        //                             select new ModelData(method, edge.Source, edge.call.GetReturnValue());

        //    return signalingReceivers.Distinct();
        //}

        public static IEnumerable<Pair<VariableHeapVertex, WholeCGNode>> GetBlockingTaskReceivers()
        {            
            var blockingMethods = from node in wholecg.Vertices 
                                  where (node.Name.Contains("System.Threading.Tasks.Task") && (node.Name.Contains("Wait/") || node.Name.Contains("get_Result"))) 
                                  select node;

            var blockingReceivers = from edge in wholecg.Edges
                                    where blockingMethods.Contains(edge.Target) && wholecg.interesting.Contains(edge.Source) // && edge.call.GetParamCount() > 0
                                    select new Pair<VariableHeapVertex, WholeCGNode>(edge.call.GetParam(0), edge.Source);

            return blockingReceivers.Distinct();
        }

        // This is a bit of a hack. What we're really looking for are Awaiter objects that are receivers of GetResult
        // we should then look up the m_task field of the targets of these awaiters
        // But we're instead pretending that every task that is the receiver of a GetAwaiter call will eventually have the
        // potentially blocking GetResult called on it - this is a (safe) overapproximation
        // We do this because seal cannot track returned points to for variables that hold returned structs, and consequently their field targets
        public static IEnumerable<Pair<VariableHeapVertex, WholeCGNode>> GetBlockingAwaiterReceivers()
        {            
            var blockingMethods = from node in wholecg.Vertices 
                                  where (node.Name.Contains("System.Runtime.CompilerServices.TaskAwaiter") && node.Name.Contains("GetAwaiter/")) ||
                                        (node.Name.Contains("ConfigureAwait/"))
                                  select node;

            var blockingReceivers = from edge in wholecg.Edges
                                    where blockingMethods.Contains(edge.Target) && wholecg.interesting.Contains(edge.Source) // && edge.call.GetParamCount() > 0
                                    select new Pair<VariableHeapVertex, WholeCGNode>(edge.call.GetParam(0), edge.Source);

            return blockingReceivers.Distinct();
        }

        public static Pair<bool, Context> MayAlias(VariableHeapVertex v1, WholeCGNode m1, VariableHeapVertex v2, WholeCGNode m2)
        {
            if (v1.Equals(v2) && m1.Equals(m2))
                return new Pair<bool, Context>(true, v1.context);
            var v1targets = GetPointsTo(v1, m1);
            var v2targets = GetPointsTo(v2, m2);
            return MayAlias(v1, v2, v1targets, v2targets);
        }

        public static Pair<bool, Context> MayAliasField(VariableHeapVertex v1, String f, WholeCGNode m1, VariableHeapVertex v2, WholeCGNode m2)
        {
            var v1targets = GetFieldTargets(v1, f, m1);
            var v2targets = GetPointsTo(v2, m2);
            return MayAlias(v1, v2, v1targets, v2targets);
        }

        public static Pair<bool, Context> MayAliasLabelledPointsTo(VertexWithContext v1, String f, WholeCGNode m1, VariableHeapVertex v2, WholeCGNode m2)
        {
            var v1targets = GetLabelledPointsTo(v1, f, m1);
            var v2targets = GetPointsTo(v2, m2);
            return MayAlias(v1, v2, v1targets, v2targets);
        }

        private static Pair<bool, Context> MayAlias(VertexWithContext v1, VertexWithContext v2, IEnumerable<VertexWithContext> v1targets, IEnumerable<VertexWithContext> v2targets)
        {
            foreach (var v1target in v1targets)
            {
                var v1tgt = v1target as VertexWithSiteId;
                int v1CtxLength = v1target.context.Count;
                foreach (var v2target in v2targets)
                {                    
                    var v2tgt = v2target as VertexWithSiteId;
                    if (v1tgt.GetSiteId() == v2tgt.GetSiteId())
                    {                       
                        int v2CtxLength = v2target.context.Count;
                        bool flag = true;
                        for (int i = 0; i < Math.Min(v1CtxLength, v2CtxLength); i++)
                        {
                            if (!v1target.context.list[i].Equals(v2target.context.list[i]))
                            {
                                flag = false;
                                break;
                            }
                        }
                        if (flag == false)
                            continue;

                        Console.WriteLine("\n{0} <--> {1}", v1target, v2target);
                        if (v1CtxLength > v2CtxLength)
                        {
                            foreach (var id in v1target.context.list)
                                if (AnalysisUtil.idToCall.ContainsKey((uint)id))
                                    Console.WriteLine("{0}-->{1}", id, AnalysisUtil.idToCall[(uint)id]);
                        }
                        else {
                            foreach (var id in v2target.context.list)
                                if (AnalysisUtil.idToCall.ContainsKey((uint)id))
                                    Console.WriteLine("{0}-->{1}", id, AnalysisUtil.idToCall[(uint)id]);
                        }
                        return new Pair<bool, Context>(true, v2target.context);
                    }
                }
            }
            return new Pair<bool, Context>(false, Context.EmptyContext);
        }

        public static bool isAsyncCandidate(string name)
        {
            return name.Contains("Async/") || name.Contains("Task::Delay")
                  || name.Contains("Task::Run") || name.Contains("Factory::StartNew")
                  || name.Contains("Task`1::ctor");                  
        }

        public static bool IsWhenAnyWhenAll(string name)
        {
            return name.Contains("::WhenAny/") || name.Contains("::WhenAll/");
        }

        public static IEnumerable<WholeCGEdge> GetModeledCalls()
        {            
            var retAsync = from node in wholecg.Vertices where IsWhenAnyWhenAll(node.Name) 
                           from edge in wholecg.InEdges(node)
                           where wholecg.interesting.Contains(edge.Source) && edge.call.HasReturnValue()
                           select edge;

            return retAsync.Distinct();
        }

        public static IEnumerable<Pair<VariableHeapVertex, WholeCGEdge>> GetReturnVariablesForAsync()
        {
            // 1. Find methods calling start                        
            var startMethods = from node in wholecg.Vertices
                                   where node.Name.Contains("System.Runtime.CompilerServices.AsyncTaskMethodBuilder`1::Start") 
                                         || node.Name.Contains("System.Runtime.CompilerServices.AsyncTaskMethodBuilder::Start")
                                         || node.Name.Contains("System.Runtime.CompilerServices.AsyncVoidMethodBuilder::Start")
                               select node;

            var interesting = 
                             from edge in wholecg.Edges
                             where startMethods.Contains(edge.Target) && wholecg.interesting.Contains(edge.Source) // && edge.callerFUnit != null
                             select new Pair<VariableHeapVertex, WholeCGEdge>(GetReturnSourceOperandVertex(edge.Source, edge.callerFUnit), edge);

            // 2. Async methods defined outside this dll, that might have been invoked
            // For now, nothing sophisticated, just look for methods containing async
            // leave out methods from step 1
            var retAsync = from node in wholecg.Vertices where isAsyncCandidate(node.Name) && 
                                !(wholecg.OutEdges(node).Select(x => x.Target)).Intersect(startMethods).Any()
                           from edge in wholecg.InEdges(node) where wholecg.interesting.Contains(edge.Source) && edge.call.HasReturnValue()
                           select new Pair<VariableHeapVertex, WholeCGEdge>(GetReturnSourceOperandVertex(edge), edge);

            var toReturn = (interesting.Union(retAsync)).Distinct();            
            return toReturn;                   
        }

        public static VariableHeapVertex GetReturnSourceOperandVertex(WholeCGEdge edge)
        {
            var call = edge.call;
            if(call.GetReturnValue() != null)
                return call.GetReturnValue();

            throw new System.Exception("Unable to process" + call);
        }

        public static VariableHeapVertex GetReturnSourceOperandVertex(WholeCGNode node, FunctionUnit callerFUnit)
        {
            var returnInstruction = callerFUnit.LastExitInstruction.Previous;
            Trace.Assert(returnInstruction.Opcode == Phx.Common.Opcode.Return);         
            var id = NodeEquivalenceRelation.GetVariableId(returnInstruction.SourceOperand);
            return GetVertexForVariable(id, node);
        }
      
        public static void TestForFieldAliasing(IEnumerable<Pair<VariableHeapVertex, WholeCGNode>> s1,
                                       IEnumerable<Pair<VariableHeapVertex, WholeCGNode>> s2)
        {

            foreach (var a in s1)
            {
                foreach (var b in s2)
                {                    
                    if (MayAliasField(b.Key, "m_task", b.Value, a.Key, a.Value).Key)
                    {
                        Console.WriteLine("May alias [{0}, {1}] and [{2}, {3}]", a.Key, a.Value, b.Key, b.Value);
                    }
                    var targets = GetAllFieldTargets(b.Key, "m_builder", b.Value);
                    foreach(var t in targets)
                    {
                        if (MayAliasLabelledPointsTo(t, "m_task", b.Value, a.Key, a.Value).Key)
                        {
                            Console.WriteLine("May alias [{0}, {1}] and [{2}, {3}]", a.Key, a.Value, b.Key, b.Value);
                        }
                    }
                }
            }
        }

        private static void TestForAliasing(IEnumerable<Pair<VariableHeapVertex, WholeCGNode>> s1,
                                       IEnumerable<Pair<VariableHeapVertex, WholeCGNode>> s2)
        {

            foreach (var a in s1)
            {
                foreach(var b in s2)
                {
                    if(MayAlias(a.Key, a.Value, b.Key, b.Value).Key)
                    {
                        Console.WriteLine("May alias [{0}, {1}] and [{2}, {3}]", a.Key, a.Value, b.Key, b.Value);
                    }
                }
            }
        }

        public static IEnumerable<AliasingPair> GetAliasingPairs()
        {
            var blockingReceivers = PurityAnalysisPhase.FilterMustHB? AliasQueries.GetBlockingTaskReceivers() : 
                AliasQueries.GetBlockingTaskReceivers().Union(GetBlockingAwaiterReceivers());
            var signals = AliasQueries.GetSignalingReceivers();
            
            foreach (var a in blockingReceivers)
            {                
                foreach (var b in signals)
                {
                    var res = MayAliasField(b.Key, "m_task", b.Value, a.Key, a.Value);
                    if (res.Key)
                    {                        
                        var ap = new AliasingPair(a.Value, b.Value, res.Value);
                        if (seen.Contains(new Pair<WholeCGNode, WholeCGNode>(a.Value, b.Value)))
                            continue;
                        yield return ap;
                    }
                    var targets = GetAllFieldTargets(b.Key, "m_builder", b.Value);
                    foreach (var t in targets)
                    {
                        res = MayAliasLabelledPointsTo(t, "m_task", b.Value, a.Key, a.Value);
                        if (res.Key)
                        {                            
                            var ap = new AliasingPair(a.Value, b.Value, res.Value);
                            if (seen.Contains(new Pair<WholeCGNode, WholeCGNode>(a.Value, b.Value)))
                                continue;
                            yield return ap;
                        }
                    }
                }                                                       
            }
        }

        public static void TestAliasMethods()
        {
            foreach(var s in asyncMethod)
            {
                Console.WriteLine(s);
            }

            var blockingReceivers = AliasQueries.GetBlockingTaskReceivers();//.Union(AliasQueries.GetBlockingAwaiterReceivers());
            Console.WriteLine("Blocking receivers: ");
            foreach (var p in blockingReceivers)
            {
                var variable = p.Key;
                var method = p.Value;
                Console.WriteLine("----------------------");
                Console.WriteLine(p.Key + " in " + method.Name);
                var ptsTo = AliasQueries.GetPointsTo(variable, method);
                foreach (var target in ptsTo)
                    Console.WriteLine(target);
                Console.WriteLine("----------------------\n");
            }

            var signals = AliasQueries.GetSignalingReceivers();
            Console.WriteLine("Signaling receivers: ");
            foreach (var p in signals)
            {
                var variable = p.Key;
                var method = p.Value;
                Console.WriteLine("----------------------");
                Console.WriteLine(p.Key + " in " + method.Name);
                var fldPtsTo = AliasQueries.GetEncapsulatedTaskTargets(variable, method);
                foreach (var target in fldPtsTo)
                    Console.WriteLine(target);
                Console.WriteLine("----------------------\n");
            }

            var asyncRcvrs = AliasQueries.GetReturnVariablesForAsync();

            Console.WriteLine("*************************************************************");
            Console.WriteLine("Aliasing between blocking recievers and t_builder objects:");
            Console.WriteLine("*************************************************************\n");
            TestForFieldAliasing(blockingReceivers, signals);

            var awaiterRcvrs = AliasQueries.GetBlockingAwaiterReceivers();
            Console.WriteLine("*************************************************************");
            Console.WriteLine("Aliasing between awaiters and t_builder objects:");
            Console.WriteLine("*************************************************************\n");
            // TestForFieldAliasing(awaiterRcvrs, signals);

            var asyncVars = asyncRcvrs.Select(x => new Pair<VariableHeapVertex, WholeCGNode>(x.Key, x.Value.Source));
            Console.WriteLine("*************************************************************");
            Console.WriteLine("Aliasing between blocking receivers and returns of async:");
            Console.WriteLine("*************************************************************\n");
            // TestForAliasing(blockingReceivers, asyncVars);

            Console.WriteLine("*************************************************************");
            Console.WriteLine("Aliasing between awaiters and returns of async:");
            Console.WriteLine("*************************************************************\n");
            // TestForAliasing(awaiterRcvrs, asyncVars);

            // TestForAliasing(signals, asyncRcvrs);
        }

    }

    public class AliasingPair
    {
        public WholeCGNode blockingNode;
        public WholeCGNode signalingNode;
        public Context signalingContext;

        public AliasingPair(WholeCGNode blockingNode, WholeCGNode signalingNode, Context ctx)
        {
            this.blockingNode = blockingNode;
            this.signalingNode = signalingNode;
            this.signalingContext = ctx;
        }

        public override bool Equals(object obj)
        {
            if (!(obj is AliasingPair))
                return false;
            var other = obj as AliasingPair;
            return this.blockingNode.Equals(other.blockingNode) &&
                this.signalingNode.Equals(other.signalingNode) && this.signalingContext.Equals(other.signalingContext);
                
        }

        public override int GetHashCode()
        {
            return blockingNode.GetHashCode() << 7 ^ signalingNode.GetHashCode() << 3 ^ signalingContext.GetHashCode();
        }

    }

    //public class ModelData
    //{
    //    public WholeCGNode SMFinal;
    //    public WholeCGNode Caller;
    //    public VariableHeapVertex returnVal;

    //    public ModelData(WholeCGNode smf, WholeCGNode caller, VariableHeapVertex retval)
    //    {
    //        this.SMFinal = smf;
    //        this.Caller = caller;
    //        this.returnVal = retval;
    //    }
    //}

    //class VertexWithContextComparer : IEqualityComparer<VertexWithContext>
    //{
    //    public bool Equals(VertexWithContext x, VertexWithContext y)
    //    {
    //        return
    //            x.Id == y.Id && x.context.list.Equals(y.context.list);
    //    }

    //    public int GetHashCode(VertexWithContext obj)
    //    {
    //        return obj.GetHashCode() << 7 ^ (obj.context.list.GetHashCode());
    //    }
    //}   
}
