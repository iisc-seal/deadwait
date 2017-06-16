using SafetyAnalysis.Purity.Summaries;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SafetyAnalysis.Purity.ControlFlowAnalysisPhase
{
    // Corresponds to the edge set E1 of CSGs in the paper
    public class CallGraphEdge : CSGEdgeBase
    {
        public Call call;
        public int resolvingCallerID;

        public CallGraphEdge(WholeCGNode srcnode, WholeCGNode destnode, Call c, int resolvingCallerID)
            : base(srcnode, destnode)
        {
            this.call = c;
            this.resolvingCallerID = resolvingCallerID;
        }

        public override string GetDescription()
        {
            if (call == null)
                return "Modeled Call";
            return call.directCallInstrID + "::" + call.resolvingCallInstrID + "::" + call.instructionContext;
        }

        public override bool Equals(object obj)
        {
            if (obj is CallGraphEdge)
            {
                var edge = obj as CallGraphEdge;
                return (edge.Source.Equals(this.Source) && edge.Target.Equals(this.Target) &&
                    (
                      (edge.call == null && this.call == null) ||
                      (edge.call.Equals(this.call) && edge.resolvingCallerID.Equals(this.resolvingCallerID))
                    )
                );
            }
            return false;
        }

        public override int GetHashCode()
        {
            var value = this.call != null ? this.call.GetHashCode() << 3 : 0;
            return (this.Source.GetHashCode() << 7) ^ (this.Target.GetHashCode() << 5) ^ value ^ this.resolvingCallerID.GetHashCode();
        }

    }
}
