using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics.Contracts;
using System.Diagnostics;

using SafetyAnalysis.Util;
using SafetyAnalysis.Framework.Graphs;
using SafetyAnalysis.Purity;
using SafetyAnalysis.TypeUtil;
using System.IO;
using System.Xml;
using Phx;

namespace SafetyAnalysis.Purity.Summaries
{
    using Fact = Pair<PurityAnalysisData, HashSet<Call>>;

    

    public class CalleeSummaryReader
    {
        private Phx.FunctionUnit callerUnit;
        private Phx.PEModuleUnit moduleUnit;
        private ExtendedMap<Call,string> processedCallTargets = new ExtendedMap<Call,string>();

        public bool trackCallGraph;

        public CalleeSummaryReader(Phx.FunctionUnit cunit, Phx.PEModuleUnit munit, bool trackCallGraph)
        {
            callerUnit = cunit;
            this.trackCallGraph = trackCallGraph;
            if (munit == null)
                throw new NotSupportedException("module unit null");
            moduleUnit = munit;
        }

        public ExtendedMap<Call, string> GetFoundTargets()
        {
            return processedCallTargets;
        }

        /// <summary>        
        /// Determines if any of the targets of the call is resolvable if yes returns its summary. 
        /// Ignores calls that have already been resolved (in the case of skipped calls)
        /// a virtual call can be resolved in 3 ways 
        /// (a) using stubs (that is applicable to all overriden implementations)
        /// (b) using the type hierarchy, assuming it is complete
        /// (c) lazily by storing it in the skipped calls list
        /// This code checks how to resolve a virtual call and suitably constructs its summary
        /// </summary>
        /// <param name="instruction"></param>
        /// <param name="callerData"></param>
        /// <returns></returns>
        public PurityAnalysisData
            GetCalleeData(            
            Call call,
            PurityAnalysisData callerData)
        {              
            var calltype = CallUtil.GetCallType(call, callerData);
            if (calltype.stubbed == true)
            {
                CallStubManager summan;
                if (CallStubManager.TryGetCallStubManager(call, out summan))
                {
                    var th = CombinedTypeHierarchy.GetInstance(moduleUnit);
                    //if(summan is SyntheticCallStub)
                    //{
                        var mCall = call as CallWithMethodName;
                        // add stubbed methods to the call graph provided they are framework async methods
                        // or they are modeled
                        if (mCall != null && (mCall.GetMethodName().Contains("Async") || IsModeled(mCall.GetMethodName())))
                        {
                            var funcSym = PhxUtil.NormalizedFunctionSymbol(this.callerUnit.FunctionSymbol);
                            var enclType = PhxUtil.NormalizedAggregateType(funcSym.EnclosingAggregateType);
                            var callerName = PhxUtil.GetFunctionName(funcSym);
                            var callerTypeName = PhxUtil.GetTypeName(enclType);
                            var callerSignature = PhxUtil.GetFunctionTypeSignature(funcSym.FunctionType);
                            var callerNode = WholeCGNode.New(PhxUtil.GetQualifiedFunctionName(this.callerUnit.FunctionSymbol), callerSignature, callerTypeName, callerName);
                            
                            var decTypename = mCall.GetDeclaringType();
                            var methodname = mCall.GetMethodName();
                            var sig = mCall.GetSignature();
                            var qualifiedName = decTypename + "::" + methodname + "/" + sig;
                            var calleeNode = WholeCGNode.New(qualifiedName, sig, decTypename, methodname);
                            // only add the edge in the bottom-up phase
                            if (trackCallGraph)
                                wholecg.AddEdge(callerNode, calleeNode, call, callerNode.id, this.callerUnit);
                        }
                    //}
                    return summan.GetSummary(call, callerData, th);
                }
                else
                    throw new System.ArgumentException("Cannot find stub managaer for call: " + call);
            }

            List<PurityAnalysisData> calleeSummaries = null;
            if(call is StaticCall)
            {
                calleeSummaries = GetTargetSummaries(call as StaticCall);                
            }
            else if (call is VirtualCall)
            {
                if (calltype.typehierarchy == true)
                    calleeSummaries = GetTargetSummariesUsingTypeHierarchy(call as VirtualCall, callerData);
                else
                    calleeSummaries = GetTargetSummaries(call as VirtualCall, callerData);
            }            
            else if (call is DelegateCall)
            {
                calleeSummaries = GetTargetSummaries(call as DelegateCall, callerData);
            }
            else
                throw new NotSupportedException("Unknown call type encountered in InternalSummaryManager");                    

            if (!calleeSummaries.Any())
            {
                //better to drop this call (this is very likely infeasible)
                return null;                       
            }

            //collapse all data
            //Console.WriteLine("===In function {0}====", callerUnit.FunctionSymbol.QualifiedName);
            //Console.WriteLine("===Before Collapsing Purity Data====");
            //foreach (var cs in calleeSummaries)
            //    Console.WriteLine(cs);
            var v = AnalysisUtil.CollapsePurityData(calleeSummaries);
            //Console.WriteLine("===After Collapsing Purity Data====");
            //Console.WriteLine(v);
            return v;
        }

        private bool IsModeled(string v)
        {
            return v.Contains("WhenAny") || v.Contains("WhenAll") || v.Contains("StartNew") || v.Equals("Run") || v.Equals("Delay");
        }

        public List<PurityAnalysisData> GetTargetSummaries(StaticCall scall)
        {
            var decTypename = scall.GetDeclaringType();
            var methodname = scall.GetMethodName();
            var sig = scall.GetSignature();
            var qualifiedName = decTypename + "::" + methodname + "/" + sig;
            var summaries = new List<PurityAnalysisData>();

            if (PurityAnalysisPhase.TraceSummaryApplication)
                Trace.TraceInformation("Getting summary for static call : " + qualifiedName);

            //update the wholeprogram call graph
            if (this.callerUnit != null)
            {
                var funcSym = PhxUtil.NormalizedFunctionSymbol(this.callerUnit.FunctionSymbol);
                var enclType = PhxUtil.NormalizedAggregateType(funcSym.EnclosingAggregateType);
                var callerName = PhxUtil.GetFunctionName(funcSym);
                var callerTypeName = PhxUtil.GetTypeName(enclType);
                var callerSignature = PhxUtil.GetFunctionTypeSignature(funcSym.FunctionType);
                var callerNode = WholeCGNode.New(PhxUtil.GetQualifiedFunctionName(this.callerUnit.FunctionSymbol), callerSignature, callerTypeName, callerName);
                var calleeNode = WholeCGNode.New(qualifiedName, sig, decTypename, methodname);
                if(trackCallGraph)
                    wholecg.AddEdge(callerNode, calleeNode, scall, callerNode.id, this.callerUnit);
            }

            var typeinfo = CombinedTypeHierarchy.GetInstance(moduleUnit).LookupTypeInfo(decTypename);
            //if (typeinfo == null)
            //{
            //    //type hierarhcy is not downward closed here.
            //    //log error msg
            //    Trace.TraceWarning("Cannot find the receiver type of " + qualifiedName);
            //    MethodLevelAnalysis.unknownTargetCalls.Add(qualifiedName);           
  
            //    //use unanalyzable call summary here
            //    var calleeSum = SummaryTemplates.GetUnanalyzableCallSummary(methodname, decTypename);
            //    summaries.Add(calleeSum);
            //    return summaries;
            //}

            var methodInfos = typeinfo.GetMethodInfos(methodname, sig);
            foreach (var methodinfo in methodInfos)
            {
                //on the fly call graph construction
                //add edges from caller function to the callee node
                if (methodinfo is InternalMethodInfo && this.callerUnit != null)
                {
                    var callGraph = this.callerUnit.ParentPEModuleUnit.CallGraph;
                    var callerNode = this.callerUnit.FunctionSymbol.CallNode;
                    var calleeNode = (methodinfo as InternalMethodInfo).GetFunctionSymbol().CallNode;
                    if (callGraph.FindCallEdge(callerNode, calleeNode) == null && trackCallGraph)
                    {
                        callGraph.CreateUniqueCallEdge(callerNode, calleeNode.FunctionSymbol);
                    }
                }                

                var calleeSum = methodinfo.GetSummary();
                if (calleeSum == null)
                {
                    //callees are not downward closed here and we do not have stubs
                    //log error msg 
                    Trace.TraceWarning("Cannot find the summary for: " + qualifiedName);
                    if(qualifiedName.Contains("Async/"))
                        Console.WriteLine("Warning: Cannot find the summary for: " + qualifiedName);
                    MethodLevelAnalysis.unknownTargetCalls.Add(qualifiedName);

                    //use unanalyzable call summary here
                    var calleeData = SummaryTemplates.GetUnanalyzableCallSummary(methodname, decTypename);
                    summaries.Add(calleeData);
                    continue;
                }

                summaries.Add(calleeSum);                
            }            
            //if (!summaries.Any())
            //{                
            //    //type hierarhcy is not downward closed here.
            //    //log error msg
            //    Trace.TraceWarning("Cannot find the receiver type of " + qualifiedName);
            //    MethodLevelAnalysis.unknownTargetCalls.Add(qualifiedName);

            //    //use unanalyzable call summary here
            //    var calleeData = SummaryTemplates.GetUnanalyzableCallSummary(methodname, decTypename);
            //    summaries.Add(calleeData);
            //}
            return summaries;
        }

        public List<PurityAnalysisData> GetTargetSummaries(            
            DelegateCall dcall,
            PurityAnalysisData callerData)
        {
            var calleeSummaries = new List<PurityAnalysisData>();

            var targetVertices = AnalysisUtil.GetTargetVertices(dcall, callerData);
            var targetMethods = AnalysisUtil.GetMethodVertices(callerData, targetVertices);
            IEnumerable<string> recvrTypenames = null;

            if (targetMethods.Any())
            {
                foreach (var m in targetMethods)
                {
                    if (m.IsVirtual)
                    {
                        if (recvrTypenames == null)
                        {
                            var recvrVertices = AnalysisUtil.GetReceiverVertices(callerData, targetVertices).OfType<InternalHeapVertex>();
                            recvrTypenames = from r in recvrVertices
                                             from typeName in callerData.GetTypes(r)
                                             select typeName;
                        }
                        foreach (var typename in recvrTypenames)
                        {
                            ResolveIfNotProcessed(callerData, dcall, typename, m.methodname, m.signature, calleeSummaries);
                        }
                    }
                    else
                    {
                        ResolveIfNotProcessed(callerData, dcall, m.typename, m.methodname, m.signature, calleeSummaries);
                    }
                }
            }
            else
            {
                throw new NotImplementedException("No targets for delegate call: " + dcall);                
            }
            return calleeSummaries;
        }               

        private List<PurityAnalysisData> GetTargetSummaries(
            VirtualCall vcall,
            PurityAnalysisData callerData)
        {
            var summaries = new List<PurityAnalysisData>();
            //try getting the receiver vertices
            IEnumerable<HeapVertexBase> receiverVertices = null;
            receiverVertices = AnalysisUtil.GetReceiverVertices(vcall, callerData);

            //check if this is a resolvable call.
            if (receiverVertices != null &&
                receiverVertices.Any((HeapVertexBase v) => (v is InternalHeapVertex)))
            {
                //known receiver types
                IEnumerable<string> concreteTypenames =
                    from r in receiverVertices.OfType<InternalHeapVertex>()
                    from typeName in callerData.GetTypes(r as InternalHeapVertex)
                    select typeName;

                string methodname = vcall.GetMethodName();
                string sig = vcall.GetSignature();

                foreach (var typename in concreteTypenames)
                {
                    ResolveIfNotProcessed(callerData, vcall, typename, methodname, sig, summaries);
                }
                return summaries;
            }
            else
                throw new NotSupportedException(
                    "Cannot resolve virtual call: " + vcall + " no internal receiver vertices");           
        }

        private List<PurityAnalysisData> GetTargetSummariesUsingTypeHierarchy(VirtualCall vcall, 
            PurityAnalysisData callerData)
        {
            var summaries = new List<PurityAnalysisData>();

            var th = CombinedTypeHierarchy.GetInstance(moduleUnit);
            var methodinfos = new HashSet<TypeUtil.MethodInfo>();

            //get the names of all the subtypes and supertypes
            //handle virtual calls here                                
            var decTypeinfo = th.LookupTypeInfo(vcall.declaringtype);

            //find all the inherited methods with the same signature                                            
            var inheritedMethods = th.GetInheritedMethods(decTypeinfo, vcall.methodname, vcall.signature);
            methodinfos.UnionWith(inheritedMethods);

            //find all the sub-types of the receiver type.                                                     
            var subTypeMethods = new List<TypeUtil.MethodInfo>();
            foreach (var subtypeinfo in th.GetSubTypesFromTypeHierarchy(decTypeinfo))
            {
                subTypeMethods.AddRange(subtypeinfo.GetMethodInfos(vcall.methodname, vcall.signature));
            }
            methodinfos.UnionWith(subTypeMethods);
            
            foreach (var minfo in methodinfos)
            {                
                var summary = minfo.GetSummary();
                if (summary != null)
                {                    
                    summaries.Add(summary);
                }
            }
            return summaries;
        }

        private void ResolveIfNotProcessed(
            PurityAnalysisData callerData,
            Call call,
            string typename,
            string methodname,
            string sig,
            List<PurityAnalysisData> calleeSummaries)
        {            
            string qualifiedName = typename + "::" + methodname + "/" + sig;
            if (callerData.GetProcessedTargets(call).Contains(qualifiedName))
                return;
            
            processedCallTargets.Add(call, qualifiedName);

            if (PurityAnalysisPhase.TraceSummaryApplication)
                Trace.TraceInformation("Getting summary for virtual call : " + qualifiedName);
            
            //Get all the possible method infos
            var th = CombinedTypeHierarchy.GetInstance(moduleUnit);
            var typeinfo = th.LookupTypeInfo(typename);
            //if (!th.IsHierarhcyKnown(typeinfo) == null)
            //{
            //    //type hierarhcy is not downward close here.
            //    //log error msg
            //    Trace.TraceWarning("Cannot find the receiver type of " + qualifiedName);
            //    MethodLevelAnalysis.unknownTargetCalls.Add(qualifiedName);

            //    //use unanalyzable call summary here
            //    var calleeSum = SummaryTemplates.GetUnanalyzableCallSummary(methodname, typename);
            //    calleeSummaries.Add(calleeSum);

            //    //update the wholeprogram call graph                
            //    if (call.callingMethodnames != null)
            //    {
            //        foreach(var mname in call.callingMethodnames)
            //            wholecg.AddEdge(mname, qualifiedName);
            //    }
            //    return;            
            //}

            var inheritedMethods = th.GetInheritedMethods(typeinfo, methodname, sig);
            if (inheritedMethods.Any())
            {
                foreach (var methodinfo in inheritedMethods)
                { 
                    //on the fly call graph construction
                    //add edges from caller function to the callee node
                    if (methodinfo is InternalMethodInfo && this.callerUnit != null)
                    {
                        var callGraph = this.callerUnit.ParentPEModuleUnit.CallGraph;
                        var callerNode = this.callerUnit.FunctionSymbol.CallNode;
                        var calleeNode = (methodinfo as InternalMethodInfo).GetFunctionSymbol().CallNode;
                        if (callGraph.FindCallEdge(callerNode, calleeNode) == null && trackCallGraph)
                        {
                            callGraph.CreateUniqueCallEdge(callerNode, calleeNode.FunctionSymbol);                            
                        }
                    }

                    //Call edgeTagCall = null;
                    //if(call is CallWithMethodName)
                    //{
                    //    edgeTagCall = call;
                    //}
                    //else
                    //{
                    //    edgeTagCall = new VirtualCall(call.instructionContext, call.resolvingCallerInstrID, call.directCallInstrID,
                    //        methodname, sig, typename, call.callingMethodnames);
                    //}

                    //update the wholeprogram call graph
                    // Here, resolvingCaller is the function where the call got resolved
                    // mname is the function making the call
                    // qualifiedName is the name of the target of the call
                    // Edges are tagged with 3 ids - instruction that resolved the call, the instruction that made the call, source that resolved
                    var resolvingCaller = PhxUtil.GetQualifiedFunctionName(this.callerUnit.FunctionSymbol);
                    var funcSym = PhxUtil.NormalizedFunctionSymbol(this.callerUnit.FunctionSymbol);
                    var enclType = PhxUtil.NormalizedAggregateType(funcSym.EnclosingAggregateType);
                    var callerName = PhxUtil.GetFunctionName(funcSym);
                    var callerTypeName = PhxUtil.GetTypeName(enclType);
                    var callerSignature = PhxUtil.GetFunctionTypeSignature(funcSym.FunctionType);
                    var resolvingCallerNode = WholeCGNode.New(PhxUtil.GetQualifiedFunctionName(this.callerUnit.FunctionSymbol), callerSignature, callerTypeName, callerName);
                    if (call.callingMethodnames != null && trackCallGraph)
                    {
                        foreach (var mname in call.callingMethodnames)
                        {
                            // A direct call - targets are known to the method making the call
                            if (mname.CompareTo(resolvingCaller) == 0) {
                               
                                var calleeNode = WholeCGNode.New(qualifiedName, sig, typename, methodname);
                                if (trackCallGraph)
                                    wholecg.AddEdge(resolvingCallerNode, calleeNode, call, resolvingCallerNode.id, this.callerUnit);
                            }
                            // A target is determined only when some caller of the method making the call is processed
                            else {
                               
                                // We need to add the FUnit for the method that actually made the call,
                                // not the one where it was resolved.
                                FunctionUnit cUnit = null;
                                foreach (Unit unit in moduleUnit.ChildUnits)
                                {
                                    if (unit is FunctionUnit)
                                    {
                                        var funit = unit.AsFunctionUnit;
                                        if (PhxUtil.GetQualifiedFunctionName(funit.FunctionSymbol).Equals(mname))
                                        {
                                            cUnit = funit;
                                            break;
                                        }
                                    }
                                }
                                WholeCGNode callerNode = GetCallerNode(cUnit, mname);

                                var calleeNode = WholeCGNode.New(qualifiedName, sig, typename, methodname);
                              
                                // revert to using the resolving caller if we could not recover the direct one
                                if (cUnit == null)
                                    cUnit = callerUnit;
                                if (trackCallGraph)
                                    wholecg.AddEdge(callerNode, calleeNode, call, resolvingCallerNode.id, cUnit);
                            }
                        }
                    }

                    var calleeSum = methodinfo.GetSummary();
                    if (calleeSum == null)
                    {                        
                        //The callees are not downward closed here and we do not have stubs
                        //log error msg
                        Trace.TraceWarning("Cannot find summary for the method: " + qualifiedName);
                        MethodLevelAnalysis.unknownTargetCalls.Add(qualifiedName);

                        //use unanalyzable call summary here
                        calleeSum = SummaryTemplates.GetUnanalyzableCallSummary(methodname, typename);
                        calleeSummaries.Add(calleeSum);
                        continue;
                    }
                    //if (call is VirtualCall && (call as VirtualCall).methodname.Equals("MoveNext"))
                    //{
                    //    Console.WriteLine("\t >> Getting summary of method: " + methodinfo.GetQualifiedMethodName());
                    //    Console.WriteLine("[{0},{1},{2}]", calleeSum.OutHeapGraph.VertexCount,
                    //            calleeSum.OutHeapGraph.EdgeCount,
                    //            calleeSum.skippedCalls.Count);
                    //}
                    calleeSummaries.Add(calleeSum);                    
                }
            }
            //There are no methods here
            //else
            //{
            //    if (!typeinfo.HasInfo())
            //    {
            //        //The type hierarhcy is not downward closed here.
            //        //log error msg
            //        Trace.TraceWarning("Cannot find the receiver type of " + qualifiedName);
            //        MethodLevelAnalysis.unknownTargetCalls.Add(qualifiedName);

            //        //use unanalyzable call summary here
            //        var calleeSum = SummaryTemplates.GetUnanalyzableCallSummary(methodname, typename);
            //        calleeSummaries.Add(calleeSum);
            //        return; 
            //    }                               
            //}            
        }

        private static WholeCGNode GetCallerNode(FunctionUnit cUnit, string mname)
        {
            if(cUnit != null)
            {
                var fSym = PhxUtil.NormalizedFunctionSymbol(cUnit.FunctionSymbol);
                var eType = PhxUtil.NormalizedAggregateType(fSym.EnclosingAggregateType);
                var cName = PhxUtil.GetFunctionName(fSym);
                var cTypeName = PhxUtil.GetTypeName(eType);
                var cSignature = PhxUtil.GetFunctionTypeSignature(fSym.FunctionType);
                var callerNode = WholeCGNode.New(PhxUtil.GetQualifiedFunctionName(cUnit.FunctionSymbol), cSignature, cTypeName, cName);
                return callerNode;
            }
            //Console.WriteLine(mname);
            var n = mname.LastIndexOf("/");
            var sig = mname.Substring(n+1);
            var preamble = mname.Substring(0, n);
            var m = preamble.LastIndexOf("::");
            var name = preamble.Substring(m+2);
            var type = preamble.Substring(0, m);
            var qualifiedName = type + "::" + name + "/" + sig;
            //Console.WriteLine(type);
            //Console.WriteLine(name);
            //Console.WriteLine(sig);
            return WholeCGNode.New(qualifiedName, sig, type, name);            
        }

        //static whole program call graph
        public static WholeProgramCG wholecg = new WholeProgramCG();
    }

    #region wholeprogramCG

    

    public class WholeProgramCG : QuickGraph.BidirectionalGraph<WholeCGNode, WholeCGEdge>
    {
        public Dictionary<WholeCGNode, PurityAnalysisData> flowFact = new Dictionary<WholeCGNode, PurityAnalysisData>();

        //public Dictionary<WholeCGNode, PurityAnalysisData> finalFact = new Dictionary<WholeCGNode, PurityAnalysisData>();

        public HashSet<WholeCGNode> reachable = new HashSet<WholeCGNode>();

        public HashSet<WholeCGNode> interesting;

        public void AddEdge(WholeCGNode srcnode, WholeCGNode destnode, Call call, int resolvingCallerID, FunctionUnit callerFUnit)
        {
            //var srcnode = WholeCGNode.New(src);
            //var destnode = WholeCGNode.New(dest); 
            
            if (!this.ContainsVertex(srcnode))
                this.AddVertex(srcnode);
            if (!this.ContainsVertex(destnode))
                this.AddVertex(destnode);
            var edge = new WholeCGEdge(srcnode, destnode, call, resolvingCallerID, callerFUnit);
            if(!this.ContainsEdge(edge))
                this.AddEdge(edge);
            //else
            //{
            //    WholeCGEdge e;
            //    this.TryGetEdge(edge.Source, edge.Target, out e);
            //    Console.Write("Already exists: ");
            //    Console.WriteLine("Edge {0} --({1})--> {2}", edge.Source.Name, edge.callId, edge.Target.Name);
            //}
        }

        public void DumpToText(StreamWriter writer)
        {
            GraphUtil.DumpAsText<WholeCGNode, WholeCGEdge>(writer, this, (WholeCGNode v) => (v.Name));
            writer.Flush();
        }

        private static bool FindStart(WholeCGNode p)
        {
            return p.Name.Contains("System.Runtime.CompilerServices.AsyncTaskMethodBuilder`1::Start") ||
                p.Name.Contains("System.Runtime.CompilerServices.AsyncTaskMethodBuilder::Start") ||
                p.Name.Contains("System.Runtime.CompilerServices.AsyncVoidMethodBuilder::Start");
        }

        private static bool FindExec(WholeCGNode p)
        {
            return p.Name.Contains("ExecuteSync");
        }

        public void ShortCircuitStart()
        {
            var interestingVertices = this.Vertices.Where(x => FindStart(x));

            var edgesToAdd = new List<WholeCGEdge>();

            this.EdgeAdded += e => Console.WriteLine("Added edge " + e.Source.Name + "(-->)" + e.Target.Name);
            this.EdgeRemoved += e => Console.WriteLine("Removed edge " + e.Source.Name + "(-->)" + e.Target.Name);

            foreach (var v in interestingVertices.ToList())
            {
                IEnumerable<WholeCGEdge> inEdges;
                this.TryGetInEdges(v, out inEdges);
                IEnumerable<WholeCGEdge> outEdges;
                this.TryGetOutEdges(v, out outEdges);
                foreach (var inEdge in inEdges.ToList())
                {
                    var sourceId = inEdge.Source.id;
                    var filtered = outEdges.Where(x => x.resolvingCallerID == sourceId);
                    foreach (var t in filtered.ToList())
                    {
                        var startVertexClone = WholeCGNode.New(v.Name + "@" + t.resolvingCallerID, v.signature, v.declaringType, v.methodname);
                        var newInEdge = new WholeCGEdge(inEdge.Source, startVertexClone, inEdge.call, inEdge.resolvingCallerID, inEdge.callerFUnit);
                        var newOutEdge = new WholeCGEdge(startVertexClone, t.Target, t.call, t.resolvingCallerID, t.callerFUnit);
                        this.AddVertex(startVertexClone);
                        this.AddEdge(newInEdge);
                        this.AddEdge(newOutEdge);
                    }

                }
                this.RemoveVertex(v);
            }            
        }

        public void ShortCircuitExec()
        {
            var interestingVertices = this.Vertices.Where(x => FindExec(x));

            var edgesToAdd = new List<WholeCGEdge>();

            this.EdgeAdded += e => Console.WriteLine("Added edge " + e.Source.Name + "(-->)" + e.Target.Name);
            this.EdgeRemoved += e => Console.WriteLine("Removed edge " + e.Source.Name + "(-->)" + e.Target.Name);

            foreach (var v in interestingVertices.ToList())
            {
                IEnumerable<WholeCGEdge> inEdges;
                this.TryGetInEdges(v, out inEdges);
                IEnumerable<WholeCGEdge> outEdges;
                this.TryGetOutEdges(v, out outEdges);
                foreach (var inEdge in inEdges.ToList())
                {
                    var sourceId = inEdge.Source.id;
                    //var filtered = outEdges.Where(x => x.resolvingCallerID == sourceId);
                    var startVertexClone = WholeCGNode.New(v.Name + "@" + sourceId, v.signature, v.declaringType, v.methodname);
                    foreach (var outEdge in outEdges.ToList())
                    {                        
                        var newInEdge = new WholeCGEdge(inEdge.Source, startVertexClone, inEdge.call, inEdge.resolvingCallerID, inEdge.callerFUnit);
                        var newOutEdge = new WholeCGEdge(startVertexClone, outEdge.Target, outEdge.call, outEdge.resolvingCallerID, outEdge.callerFUnit);
                        this.AddVertex(startVertexClone);
                        this.AddEdge(newInEdge);

                        if (this.ContainsEdge(inEdge))
                            this.RemoveEdge(inEdge);
                        if (outEdge.resolvingCallerID == sourceId || outEdge.resolvingCallerID == v.id)
                        {
                            this.AddEdge(newOutEdge);                            
                        }
                    }

                    //if (InDegree(v) == 0 && OutDegree(v) == 0)
                    this.RemoveVertex(v);
                }
                //this.RemoveVertex(v);
            }
        }

        public void ModelTaskAsyncMethods()
        {
            this.EdgeAdded += e => Console.WriteLine("Added edge " + e);
            var vertices = Vertices.Where(x => (x.methodname.Equals("WhenAll")
                                          || x.methodname.Equals("WhenAny")
                                          || x.Name.Contains("TaskFactory::StartNew")
                                          || x.Name.Contains("Task::Run")
                                         ));

            var signalingMethods = from node in Vertices
                                   where node.Name.Contains("SetResult/") && node.Name.Contains("System.Runtime.CompilerServices.AsyncTaskMethodBuilder")
                                   select node;

            if (!signalingMethods.Any())
                return;

            foreach (var v in vertices)
            {
                var newEdge = new WholeCGEdge(v, signalingMethods.First(), null, -1, null);
                this.AddEdge(newEdge);
            }
        }

        public void tryShortCircuit()
        {
            this.EdgeAdded += e => Console.WriteLine("Added edge " + e);
            this.EdgeRemoved += e => Console.WriteLine("Removed edge " + e);
            this.VertexRemoved += v => Console.WriteLine("Removed vertex " + v);
            this.VertexAdded += v => Console.WriteLine("Added vertex " + v);

            foreach (var v in Vertices.ToList())
            {
                Console.WriteLine("Handling vertex " + v.Name);
                IEnumerable<WholeCGEdge> inEdges;
                this.TryGetInEdges(v, out inEdges);
                IEnumerable<WholeCGEdge> outEdges;
                this.TryGetOutEdges(v, out outEdges);
                bool flag = false;
                foreach (var inEdge in inEdges.ToList())
                {
                    var sourceId = inEdge.Source.id;
                    var filtered = outEdges.Where(x => x.resolvingCallerID == sourceId);
                    foreach (var outEdge in filtered.ToList())
                    {
                        var vertexClone = WholeCGNode.New(v.Name + "@" + outEdge.resolvingCallerID, v.signature, v.declaringType, v.methodname);
                        var newInEdge = new WholeCGEdge(inEdge.Source, vertexClone, inEdge.call, inEdge.resolvingCallerID, inEdge.callerFUnit);
                        var newOutEdge = new WholeCGEdge(vertexClone, outEdge.Target, outEdge.call, outEdge.resolvingCallerID, outEdge.callerFUnit);
                        this.AddVertex(vertexClone);
                        this.AddEdge(newInEdge);
                        if(this.ContainsEdge(inEdge))
                            this.RemoveEdge(inEdge);
                        this.AddEdge(newOutEdge);
                        this.RemoveEdge(outEdge);
                        flag = true;
                    }
                    
                }

                if (flag)
                {
                    Console.WriteLine("Shorting " + v);
                }

                if(InDegree(v) == 0 && OutDegree(v) == 0)
                    this.RemoveVertex(v);
                Console.WriteLine("------------------\n");
            }
        }

        private static bool FindBlocking(WholeCGNode p)
        {
            return
                (p.declaringType.Contains("System.Threading.Tasks.Task") && (p.methodname.Contains("Wait") || p.methodname.Contains("get_Result"))) ||
                (p.declaringType.Contains("System.Runtime.CompilerServices.TaskAwaiter") && p.methodname.Contains("GetResult"));
        }

        private static bool FindSignaling(WholeCGNode p)
        {
            return (p.declaringType.Contains("System.Runtime.CompilerServices.AsyncTaskMethodBuilder") &&
                p.methodname.Contains("SetResult")) || (p.Name.Contains("StartNew/") 
                || p.Name.Contains("WhenAny/") || p.Name.Contains("WhenAll/"));
        }

        private static bool FindAsync(WholeCGNode p)
        {
            return p.Name.Contains("Async/");
        }

        private void backwardDFS(WholeCGNode root, HashSet<WholeCGNode> visited)
        {
            if (visited.Contains(root))
                return;
            visited.Add(root);
            foreach(var incomingEdge in InEdges(root))
            {
                backwardDFS(incomingEdge.Source, visited);
            }
        }

        public HashSet<WholeCGNode> GetInteresting()
        {
            var roots = from v in this.Vertices
                        where (FindBlocking(v) || FindSignaling(v) || FindAsync(v))
                        from i in this.InEdges(v)
                        select i.Source;
            interesting = new HashSet<WholeCGNode>();
            foreach (var v in roots)
            {
                //var visited = new HashSet<WholeCGNode>();
                backwardDFS(v, interesting);
                //interesting.UnionWith(visited);
            }
            return interesting;
        }

        public void PrintSCCs()
        {
            QuickGraph.Algorithms.ConnectedComponents.StronglyConnectedComponentsAlgorithm<WholeCGNode, WholeCGEdge> algo =
               new QuickGraph.Algorithms.ConnectedComponents.StronglyConnectedComponentsAlgorithm<WholeCGNode, WholeCGEdge>(this);
            algo.Compute();

            //map component ids to a set of nodes.
            Dictionary<int, HashSet<WholeCGNode>> componentMap = new Dictionary<int, HashSet<WholeCGNode>>();
            foreach (var node in Vertices)
            {
                int componentId;
                if (algo.Components.TryGetValue(node, out componentId))
                {
                    HashSet<WholeCGNode> sameComponentNodes;
                    if (componentMap.TryGetValue(componentId, out sameComponentNodes))
                        sameComponentNodes.Add(node);
                    else
                    {
                        sameComponentNodes = new HashSet<WholeCGNode>();
                        sameComponentNodes.Add(node);
                        componentMap.Add(componentId, sameComponentNodes);
                    }
                }
            }

            foreach(var kv in componentMap)
            {
                if(kv.Value.Count > 1)
                {
                    Console.WriteLine("Component ID " + kv.Key);
                    foreach(var m in kv.Value)
                    {
                        Console.WriteLine("\t " + m.Name);
                    }
                }
            }
        }

    }

    public class WholeCGNode
    {
        private static Dictionary<string,WholeCGNode> NodeTable = new Dictionary<string,WholeCGNode>();
        public static int GUID = 37;
        public string Name { get; private set; }

        public string signature;
        public string declaringType;
        public string methodname;

        public int id;

        private WholeCGNode(string name, string signature, string declaringType, string methodname)
        {            
            this.Name = name;
            this.signature = signature;
            this.declaringType = declaringType;
            this.methodname = methodname;
            id = GUID++;
        }

        public static WholeCGNode New(string name, string signature, string declaringType, string methodname)
        {
            //name = name.Replace("[System.Threading.Tasks]System.Threading.Tasks.Task", "[mscorlib]System.Threading.Tasks.Task");
            if (NodeTable.ContainsKey(name))
                return NodeTable[name];
            else
            {                
                var node = new WholeCGNode(name, signature, declaringType, methodname);
                NodeTable.Add(name, node);
                return node;
            }
        }

        public override int GetHashCode()
        {
            return id;
        }

        public override bool Equals(object obj)
        {
            if(obj is WholeCGNode)
                return this.id == (obj as WholeCGNode).id;
            return false;
        }

        public override string ToString()
        {
            return Name+"[" + id+"]";
        }
    }

    public class WholeCGEdge : QuickGraph.Edge<WholeCGNode>
    {
        public Call call;

        // We need to track this to pick up valid paths according to context sensitivity later
        public int resolvingCallerID;
        // We need to track this for top down composition
        public FunctionUnit callerFUnit;

        public WholeCGEdge(WholeCGNode srcnode, WholeCGNode destnode, Call c, int resolvingCallerID, FunctionUnit callerFUnit)
            : base(srcnode,destnode)
        {
            this.call = c;
            this.resolvingCallerID = resolvingCallerID;
            this.callerFUnit = callerFUnit;
        }
       
        public override bool Equals(object obj)
        {
            if (obj is WholeCGEdge)
            {
                var edge = obj as WholeCGEdge;
                return (edge.Source.Equals(this.Source) && edge.Target.Equals(this.Target) 
                    && edge.call.Equals(this.call) && edge.resolvingCallerID.Equals(this.resolvingCallerID));
            }
            return false;
        }

        public override int GetHashCode()
        {
            return (this.Source.GetHashCode() << 7) ^ (this.Target.GetHashCode() << 5) ^ (this.call.GetHashCode() << 3) ^ this.resolvingCallerID.GetHashCode();
        }

        public override string ToString()
        {
            return this.Source + "(-->)" + this.Target;
        }

    }

    #endregion wholeprogramCG
}
