using SafetyAnalysis.Framework.Graphs;
using SafetyAnalysis.Purity.Summaries;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SafetyAnalysis.Purity.ControlFlowAnalysisPhase
{
    // Corresponds to the edge set E3 of CSGs in the paper
    public class CallBackEdge : CSGEdgeBase
    {
        public uint stateMachineVariable;
        public VariableHeapVertex returnVariable;

        public CallBackEdge(WholeCGNode srcnode, WholeCGNode destnode, uint smVariable, VariableHeapVertex retVar)
            : base(srcnode, destnode)
        {
            this.stateMachineVariable = smVariable;
            this.returnVariable = retVar;
        }

        public override string GetDescription()
        {
            return "(" + stateMachineVariable + ", " + returnVariable + ")";
        }

        public override bool Equals(object obj)
        {
            if (obj is CallBackEdge)
            {
                var edge = obj as CallBackEdge;
                return (edge.Source.Equals(this.Source) && edge.Target.Equals(this.Target)
                    && edge.stateMachineVariable == this.stateMachineVariable &&
                     ((edge.returnVariable == null && this.returnVariable == null) || edge.returnVariable.Equals(this.returnVariable))
                       );
            }
            return false;
        }

        public override int GetHashCode()
        {
            return (this.Source.GetHashCode() << 7) ^ (this.Target.GetHashCode() << 5) ^ ((int)(this.stateMachineVariable << 3)) ^ this.returnVariable.GetHashCode();
        }

    }
}
