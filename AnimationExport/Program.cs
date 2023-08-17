using CUE4Parse_Conversion;
using CUE4Parse_Conversion.Animations;
using CUE4Parse.Encryption.Aes;
using CUE4Parse.FileProvider;
using CUE4Parse.MappingsProvider;
using CUE4Parse.UE4.Assets;
using CUE4Parse.UE4.Assets.Exports.Animation;
using CUE4Parse.UE4.Objects.Core.Misc;
using CUE4Parse.UE4.Versions;
using AnimationExport.Utils;

using Constants = AnimationExport.Utils.Constants;

namespace AnimationExport
{
    public class Progam
    {
        static async Task Main(string[] args)
        {
            if(!Directory.Exists(Constants.DataPath))
                Directory.CreateDirectory(Constants.DataPath);

            if (!Directory.Exists(Constants.ExportPath))
                Directory.CreateDirectory(Constants.ExportPath);

            Console.Title = "Animation Exporter by 0xkaede";

            Console.Write("Please enter a ID: ");

            var eid = Console.ReadLine();

            Console.Clear();

            await FileProvider.Init();

            await FileProvider.Export(eid);

            for (int i = 3; i > 0; i--)
            {
                Logger.Log($"Closing in {i}");
                await Task.Delay(1000);
            }
        }
    }
}