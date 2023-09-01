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
        public Dictionary<string, double> Blend { get; set; } = new Dictionary<string, double>();
        public List<string> FloatCurves { get; set; } = new List<string>();
        public bool IsAddictive { get; set; }
        public bool IsMovingEmote { get; set; } = false;
        public bool IsMoveForwardOnly { get; set; } = false;
        public float WalkForwardSpeed { get; set; } = 0;
    }
}
