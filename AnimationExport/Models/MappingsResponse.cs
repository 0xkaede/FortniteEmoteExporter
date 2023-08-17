using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AnimationExport.Models
{
    public class MappingsResponse
    {
        [JsonProperty("url")]
        public string Url { get; set; }

        [JsonProperty("fileName")]
        public string FileName { get; set; }

        [JsonProperty("hash")]
        public string Hash { get; set; }

        [JsonProperty("length")]
        public int Length { get; set; }

        [JsonProperty("uploaded")]
        public DateTime Uploaded { get; set; }

        [JsonProperty("meta")]
        public Meta Meta { get; set; }
    }

    public class Meta
    {
        [JsonProperty("version")]
        public string Version { get; set; }

        [JsonProperty("compressionMethod")]
        public string CompressionMethod { get; set; }

        [JsonProperty("platform")]
        public string Platform { get; set; }
    }
}
