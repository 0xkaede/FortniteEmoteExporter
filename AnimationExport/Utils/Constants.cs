using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AnimationExport.Utils
{
    public static class Constants
    {
        public static readonly string BasePath = Directory.GetCurrentDirectory();

        public static readonly string DataPath = $"{BasePath}\\.data";
        public static readonly string ExportPath = $"{BasePath}\\Export";
    }
}
