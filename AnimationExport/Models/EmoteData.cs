using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AnimationExport.Models
{
    public class EmoteData
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public Dictionary<string, int> Blend { get; set; }
        public List<string> FloatCurves { get; set; }
        public bool IsAddictive { get; set; }
    }
}
