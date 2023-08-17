using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AnimationExport.Utils
{
    public class FortniteUtils
    {
        public static string PaksPath => Path.Combine(GetFortntieInstallation().InstallLocation, "FortniteGame\\Content\\Paks");

        public static EpicInstallation GetFortntieInstallation()
        {
            var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "Epic\\UnrealEngineLauncher\\LauncherInstalled.dat");

            return !File.Exists(path)
                ? null
                : JsonConvert.DeserializeObject<EpicInstalled>(File.ReadAllText(path)).InstallationList.FirstOrDefault(x => x.AppName == "Fortnite");
        }
    }

    public class EpicInstalled
    {
        [JsonProperty("InstallationList")]
        public List<EpicInstallation> InstallationList { get; set; }
    }

    public class EpicInstallation
    {
        [JsonProperty("InstallLocation")]
        public string InstallLocation { get; set; }

        [JsonProperty("AppVersion")]
        public string AppVersion { get; set; }

        [JsonProperty("AppName")]
        public string AppName { get; set; }
    }
}
