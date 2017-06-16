using SafetyAnalysis.Purity.Summaries;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SafetyAnalysis.Purity.ControlFlowAnalysisPhase
{
    // Corresponds to the edge set E2 of CSGs in the paper
    public class StateMachineEdge : CSGEdgeBase
    {
        public uint variableIndex;
        public string configuration;

        public StateMachineEdge(WholeCGNode srcnode, WholeCGNode destnode, uint variableIndex, string configuration)
            : base(srcnode, destnode)
        {
            this.variableIndex = variableIndex;
            this.configuration = configuration;
        }

        public override string GetDescription()
        {
            return "(" + variableIndex + ", " + configuration + ")";
        }

        public override bool Equals(object obj)
        {
            if (obj is StateMachineEdge)
            {
                var edge = obj as StateMachineEdge;
                return (edge.Source.Equals(this.Source) && edge.Target.Equals(this.Target)
                    && edge.variableIndex == this.variableIndex && edge.configuration.CompareTo(this.configuration) == 0);
            }
            return false;
        }

        public override int GetHashCode()
        {
            return (this.Source.GetHashCode() << 7) ^ (this.Target.GetHashCode() << 5) ^ ((int)(this.variableIndex << 3)) ^ this.configuration.GetHashCode();
        }
    }
}
