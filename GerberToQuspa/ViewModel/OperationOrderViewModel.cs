using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GerberToQuspa.ViewModel
{
    public class OperationOrderViewModel : BindableBase
    {
        public RenderContext RenderContext { get; set; }        

        public OperationOrderViewModel()
        {           
        }

        public void MoveRootNode(Cluster source, Cluster target)
        {
            if (source == null || target == null || source == target)
                return;

            int srcIndex = RenderContext.ClusterList.IndexOf(source);
            int dstIndex = RenderContext.ClusterList.IndexOf(target);

            if (srcIndex < 0 || dstIndex < 0)
                return;

            RenderContext.ClusterList.Move(srcIndex, dstIndex);
        }     
    }
}