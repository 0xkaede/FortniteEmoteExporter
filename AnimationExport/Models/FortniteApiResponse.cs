using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AnimationExport.Models
{
    public class FortniteAPIResponse<T>
    {
        [JsonProperty("status")]
        public int Status { get; set; }

        [JsonProperty("data")]
        public T Data { get; set; }
    }

    public class AES
    {
        [JsonProperty("build")]
        public string Build { get; set; }

        [JsonProperty("mainKey")]
        public string MainKey { get; set; }

        [JsonProperty("dynamicKeys")]
        public List<DynamicKey> DynamicKeys { get; set; }
    }

    public class DynamicKey
    {
        [JsonProperty("pakFilename")]
        public string PakFilename { get; set; }

        [JsonProperty("pakGuid")]
        public string PakGuid { get; set; }

        [JsonProperty("key")]
        public string Key { get; set; }
    }
}
