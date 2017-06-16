using SafetyAnalysis.Purity.Summaries;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SafetyAnalysis.Purity.ControlFlowAnalysisPhase
{
    // The base class for CSG edges
    public class CSGEdgeBase : QuickGraph.Edge<WholeCGNode>
    {
        public CSGEdgeBase(WholeCGNode srcnode, WholeCGNode destnode)
            : base(srcnode, destnode)
        {

        }

        public virtual String GetDescription()
        {
            return "";
        }


        public override bool Equals(object obj)
        {
            if (obj is CSGEdgeBase)
            {
                var edge = obj as CSGEdgeBase;
                return (edge.Source.Equals(this.Source) && edge.Target.Equals(this.Target));
            }
            return false;
        }

        public override int GetHashCode()
        {
            return (this.Source.GetHashCode() << 7) ^ (this.Target.GetHashCode() << 5);
        }
    }
}
