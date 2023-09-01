using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AnimationExport.Models
{
    //most skunk way but i cba
    public class MontageModel
    {
        public Property Properties { get; set; }
    }

    public class Property
    {
        public Blend BlendIn { get; set; }
        public Blend BlendOut { get; set; }
        public RawCurveData RawCurveData { get; set; }
    }

    public class Blend
    {
        public double BlendTime { get; set; }
    }

    public class RawCurveData
    {
        public List<FloatCurves> FloatCurves { get; set; }
    }

    public class FloatCurves
    {
        public string CurveName { get; set; }
    }

    public class DataName
    {
        public string DisplayName { get; set; }
    }
}
