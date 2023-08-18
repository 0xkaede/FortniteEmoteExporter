using AnimationExport.Models;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AnimationExport.Utils
{
    public static class Globals
    {
        public static string CurrentId { get; set; }

        public static string AnimationsPath() => $"{Constants.ExportPath}\\{CurrentId}\\Animations";
        public static string JsonPath() => $"{AnimationsPath()}\\Json";
        public static string IconsPath() => $"{Constants.ExportPath}\\{CurrentId}\\Icons";
        public static string MiscPath() => $"{Constants.ExportPath}\\{CurrentId}\\Misc";

        public static async Task JsonEmoteDataSave(EmoteData data) 
            => await File.WriteAllTextAsync($"{Constants.ExportPath}\\{CurrentId}\\Misc\\Data.json",
                JsonConvert.SerializeObject(data, Formatting.Indented));

        public static async Task JsonDataSave(string data)
            => await File.WriteAllTextAsync($"{Constants.ExportPath}\\{CurrentId}\\Misc\\Montage.json",
                data);

        public static async Task<MontageModel> GetMontageData() => JsonConvert.DeserializeObject<MontageModel>(await File.ReadAllTextAsync($"{Constants.ExportPath}\\{CurrentId}\\Misc\\Montage.json"));

        public static void MoveAnimations(this string filePath) => File.Move(filePath, $"{AnimationsPath()}\\{Path.GetFileName(filePath)}");
    }
}
