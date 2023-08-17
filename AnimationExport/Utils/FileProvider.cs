using CUE4Parse.Encryption.Aes;
using CUE4Parse.FileProvider;
using CUE4Parse.MappingsProvider;
using CUE4Parse.UE4.Assets.Exports.Animation;
using CUE4Parse.UE4.Assets;
using CUE4Parse.UE4.Objects.Core.Misc;
using CUE4Parse.UE4.Versions;
using CUE4Parse.UE4.Assets.Exports.Texture;
using CUE4Parse_Conversion.Animations;
using CUE4Parse_Conversion;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using Newtonsoft.Json;
using AnimationExport.Models;
using CUE4Parse.UE4.Assets.Exports;
using System.Diagnostics;
using static AnimationExport.Utils.Globals;
using SkiaSharp;
using CUE4Parse_Conversion.Textures;
using CUE4Parse.UE4.Objects.UObject;
using System.Runtime.Serialization;
using CUE4Parse.GameTypes.FN.Assets.Exports.Sound;
using CUE4Parse.UE4.Assets.Exports.Sound;
using CUE4Parse.UE4.Assets.Exports.Sound.Node;
using CUE4Parse_Conversion.Sounds;
using CUE4Parse.UE4.Objects.Core.i18N;
using CUE4Parse.UE4.Objects.Engine.Animation;
using CUE4Parse.UE4.Assets.Exports.Material;
using FFMpegCore;
using FFMpegCore.Pipes;

namespace AnimationExport.Utils
{
    public class FileProvider
    {
        public static DefaultFileProvider Provider { get; set; }

        public static async Task Init()
        {
            try
            {
                var aes = JsonConvert.DeserializeObject<FortniteAPIResponse<AES>>(await new HttpClient().GetStringAsync("https://fortnite-api.com/v2/aes")).Data;

                Provider = new DefaultFileProvider(FortniteUtils.PaksPath, SearchOption.TopDirectoryOnly, false, new VersionContainer(EGame.GAME_UE5_3));
                Provider.Initialize();

                var keys = new List<KeyValuePair<FGuid, FAesKey>>
                {
                    new KeyValuePair<FGuid, FAesKey>(new FGuid(), new FAesKey(aes.MainKey))
                };

                keys.AddRange(aes.DynamicKeys.Select(x => new KeyValuePair<FGuid, FAesKey>(new Guid(x.PakGuid), new FAesKey(x.Key))));
                await Provider.SubmitKeysAsync(keys);
                Logger.Log($"File provider initalized with {Provider.Keys.Count} keys", LogLevel.Cue4);

                var mappings = await Mappings();

                Provider.MappingsContainer = mappings;
            }
            catch (Exception ex)
            {
                Logger.Log(ex.ToString(), LogLevel.Cue4);
            }
        }

        private static async Task<FileUsmapTypeMappingsProvider> Mappings()
        {
            var mappingsData = JsonConvert.DeserializeObject<List<MappingsResponse>>(await new HttpClient().GetStringAsync("https://fortnitecentral.genxgames.gg/api/v1/mappings")).FirstOrDefault();

            var path = Path.Combine(Constants.DataPath, mappingsData.FileName);
            if (!File.Exists(path))
            {
                Logger.Log($"Cant find latest mappings, Downloading {mappingsData.Url}", LogLevel.Cue4);
                var wc = new WebClient();
                wc.DownloadFileAsync(new Uri(mappingsData.Url), path);
                while (wc.IsBusy)
                    Thread.Sleep(500);
            }

            var latestUsmapInfo = new DirectoryInfo(Constants.DataPath).GetFiles("*_oo.usmap").FirstOrDefault();
            Logger.Log($"Mappings Pulled from file: {latestUsmapInfo.Name}", LogLevel.Cue4);
            return new FileUsmapTypeMappingsProvider(latestUsmapInfo.FullName);
        }

        public static async Task Export(string EID)
        {
            var exportDataJson = new EmoteData();
            var exporterOptions = new ExporterOptions();
            CurrentId = EID;

            #region EID
            UObject eidObj;
            try
            {
                eidObj = await Provider.LoadObjectAsync($"FortniteGame/Plugins/GameFeatures/BRCosmetics/Content/Athena/Items/Cosmetics/Dances/{EID}");
            }
            catch(Exception ex)
            {
                try
                {
                    eidObj = await Provider.LoadObjectAsync($"FortniteGame/Content/Athena/Items/Cosmetics/Dances/{EID}");
                }
                catch
                {
                    Logger.Log($"The ID \"{EID}\" Doesnt exits.");
                    return;
                }
            }

            var femaleAnimation = string.Empty;
            var maleAnimation = string.Empty;

            if(!eidObj.TryGetValue(out UObject CMM_Montage, "Animation"))
            {
                Logger.Log("Error Getting Animation", LogLevel.Error);
                return;
            }

            if (!eidObj.TryGetValue(out UObject CMF_Montage, "AnimationFemaleOverride"))
            {
                Logger.Log("Error Getting AnimationFemaleOverride", LogLevel.Error);
                return;
            }

            if (!eidObj.TryGetValue(out FText displayName, "DisplayName"))
                Logger.Log("Error Getting DisplayName", LogLevel.Error);

            if (!eidObj.TryGetValue(out FText description, "Description"))
                Logger.Log("Error Getting Description", LogLevel.Error);

            exportDataJson.Description = description.Text ?? "FAILED";
            exportDataJson.Name = displayName.Text ?? "FAILED";

            #endregion

            #region Male
            Logger.Log("Exporting Male Anims", LogLevel.Cue4);

            var CMMObject = await Provider.LoadObjectAsync(CMM_Montage.GetPathName());
            var maleAnimPath = string.Empty;

            if(CMMObject.TryGetValue(out FSlotAnimationTrack[] slotTracksMale, "SlotAnimTracks"))
            {
                var nameeee = slotTracksMale[0].SlotName;

                var maleFullBody = slotTracksMale.FirstOrDefault(x => x.SlotName.PlainText == "FullBody");

                if (maleFullBody == null)
                {
                    Logger.Log("Cant find full body in male montage", LogLevel.Cue4);
                    Console.Read();
                    return;
                }

                var maleReference = await maleFullBody.AnimTrack.AnimSegments[0].AnimReference.LoadAsync();
                maleAnimPath = maleReference.GetPathName();
                var maleAnimSequence = Provider.LoadObject<UAnimSequence>(maleAnimPath);

                var maleexporter = new AnimExporter(maleAnimSequence, exporterOptions);
                maleexporter.TryWriteToDir(new DirectoryInfo(Constants.ExportPath), out var maleLabel, out var maleFileName);

                Logger.Log($"Exported {Path.GetFileNameWithoutExtension(maleFileName)}", LogLevel.Cue4);

                if (!Directory.Exists(AnimationsPath()))
                    Directory.CreateDirectory(AnimationsPath());

                maleFileName.MoveAnimations();

                var maleListJson = new List<UAnimSequence>();
                maleListJson.Add(maleAnimSequence);

                var maleJson = JsonConvert.SerializeObject(maleListJson, Formatting.Indented);
                Directory.CreateDirectory($"{JsonPath()}");
                await File.WriteAllTextAsync(JsonPath() + $"\\{Path.GetFileNameWithoutExtension(maleFileName)}.json", maleJson);
            }
            else
                Logger.Log("Error Getting FSlotAnimationTrack", LogLevel.Error);

            var maleCMMMJson = JsonConvert.SerializeObject(CMMObject, Formatting.Indented);

            #endregion

            #region Female
            Logger.Log("Exporting Female Anims", LogLevel.Cue4);
            var CMFObject = await Provider.LoadObjectAsync(CMF_Montage.GetPathName());

            if(CMFObject.TryGetValue(out FSlotAnimationTrack[] SlotTracksFemale, "SlotAnimTracks"))
            {
                bool isAddicive = false;

                var addictive = SlotTracksFemale.FirstOrDefault(x => x.SlotName.PlainText == "AdditiveCorrective");

                if (addictive != null)
                {
                    Logger.Log("isAdditive true");
                    exportDataJson.IsAddictive = true;
                    var femaleReference = await addictive.AnimTrack.AnimSegments[0].AnimReference.LoadAsync();

                    femaleAnimation = femaleReference.GetPathName();
                    var addUAnimSequence = Provider.LoadObject<UAnimSequence>(femaleReference.GetPathName());
                    var refUAnimSequence = Provider.LoadObject<UAnimSequence>(maleAnimPath);
                    addUAnimSequence.RefPoseSeq = new ResolvedLoadedObject(refUAnimSequence);
                    var exporter = new AnimExporter(addUAnimSequence, exporterOptions);
                    exporter.TryWriteToDir(new DirectoryInfo(Constants.ExportPath), out var label, out var fileName);
                    Logger.Log($"Exported {Path.GetFileNameWithoutExtension(fileName)}", LogLevel.Cue4);
                    fileName.MoveAnimations();
                }
                else
                {
                    Logger.Log("isAdditive false");
                    exportDataJson.IsAddictive = false;

                    var femaleFullBody = SlotTracksFemale.FirstOrDefault(x => x.SlotName.PlainText == "FullBody");
                    if (femaleFullBody == null)
                    {
                        Logger.Log("Cant find full body in male montage", LogLevel.Error);
                        Console.Read();
                        return;
                    }

                    var femaleReference = await femaleFullBody.AnimTrack.AnimSegments[0].AnimReference.LoadAsync();

                    var femaleAnimSequence = Provider.LoadObject<UAnimSequence>(femaleReference.GetPathName());
                    var femaleexporter = new AnimExporter(femaleAnimSequence, exporterOptions);
                    femaleexporter.TryWriteToDir(new DirectoryInfo(Constants.ExportPath), out var femaleLabel, out var femaleFileName);
                    Logger.Log($"Exported {Path.GetFileNameWithoutExtension(femaleFileName)}", LogLevel.Cue4);
                    femaleFileName.MoveAnimations();

                    var femaleListJson = new List<UAnimSequence>();
                    femaleListJson.Add(femaleAnimSequence);

                    var femaleJson = JsonConvert.SerializeObject(femaleListJson, Formatting.Indented);
                    Directory.CreateDirectory($"{JsonPath()}");
                    await File.WriteAllTextAsync(JsonPath() + $"\\{Path.GetFileNameWithoutExtension(femaleFileName)}.json", femaleJson);
                }
            }
            else
                Logger.Log("Error Getting FSlotAnimationTrack", LogLevel.Error);
            #endregion

            #region Icons

            Logger.Log("Exporting Icons", LogLevel.Cue4);

            eidObj.TryGetValue(out UTexture2D iconSmall, "SmallPreviewImage");

            Directory.CreateDirectory(IconsPath());

            await File.WriteAllBytesAsync(IconsPath() + $"\\{iconSmall.Name}.png", iconSmall.Decode()!.Encode(SKEncodedImageFormat.Png, 256).ToArray());

            eidObj.TryGetValue(out UTexture2D largeSmall, "LargePreviewImage");

            await File.WriteAllBytesAsync(IconsPath() + $"\\{largeSmall.Name}.png", largeSmall.Decode()!.Encode(SKEncodedImageFormat.Png, 512).ToArray());
            Logger.Log("Exported Icons", LogLevel.Cue4);
            #endregion

            #region Music
            Logger.Log("Exporting Music", LogLevel.Cue4);

            if(CMMObject.TryGetValue(out FAnimNotifyEvent[] events, "Notifies"))
            {
                foreach(var sound in events)
                {
                    if(sound.NotifyName.PlainText.Contains("FortEmoteSound"))
                    {
                        var musicClass = await sound.NotifyStateClass.LoadAsync();

                        if (musicClass.TryGetValue(out UEmoteMusic emoteSound1P, "EmoteSound1P"))
                        {
                            try
                            {
                                var licenceNode = await emoteSound1P!.FirstNode.LoadAsync<UFortSoundNodeLicensedContentSwitcher>();

                                var randomNode = await licenceNode!.ChildNodes[1].LoadAsync<USoundNodeRandom>();

                                var soundNode2 = await randomNode.ChildNodes.FirstOrDefault().TryLoadAsync<USoundNodeWavePlayer>();

                                var musicBoom = await soundNode2.SoundWave.LoadAsync();

                                musicBoom.Decode(false, out var audioFormat, out var data);

                                Directory.CreateDirectory(MiscPath());
                                await File.WriteAllBytesAsync($"{MiscPath()}\\{musicBoom.Name}.ogg", data);

                                var audioInputStream = File.Open($"{MiscPath()}\\{musicBoom.Name}.ogg", FileMode.Open);
                                await using var audioOutputStream = File.Open($"{MiscPath()}\\{musicBoom.Name}_FIXED.wav", FileMode.OpenOrCreate);

                                FFMpegArguments
                                    .FromPipeInput(new StreamPipeSource(audioInputStream))
                                    .OutputToPipe(new StreamPipeSink(audioOutputStream), options =>
                                        options.ForceFormat("wav"))
                                    .ProcessSynchronously();

                                Logger.Log($"Exported {musicBoom.Name}", LogLevel.Cue4);
                            }
                            catch (Exception ex)
                            {
                                var firstNode = await emoteSound1P!.FirstNode.LoadAsync<USoundNodeRandom>();
                                var soundNode = await firstNode.ChildNodes.FirstOrDefault().TryLoadAsync<USoundNodeWavePlayer>(); //might be copy writed

                                var musicBoom = await soundNode.SoundWave.LoadAsync();

                                musicBoom.Decode(false, out var audioFormat, out var data);

                                Directory.CreateDirectory(MiscPath());
                                await File.WriteAllBytesAsync($"{MiscPath()}\\{musicBoom.Name}.ogg", data);

                                var audioInputStream = File.Open($"{MiscPath()}\\{musicBoom.Name}.ogg", FileMode.Open);
                                await using var audioOutputStream = File.Open($"{MiscPath()}\\{musicBoom.Name}_FIXED.wav", FileMode.OpenOrCreate);

                                FFMpegArguments
                                    .FromPipeInput(new StreamPipeSource(audioInputStream))
                                    .OutputToPipe(new StreamPipeSink(audioOutputStream), options =>
                                        options.ForceFormat("wav"))
                                    .ProcessSynchronously();

                                Logger.Log($"Exported {musicBoom.Name}", LogLevel.Cue4);
                            }
                        }
                        else
                            Logger.Log("Error Getting UEmoteMusic", LogLevel.Error);
                    }
                }
            }
            else
                Logger.Log("Error Getting FAnimNotifyEvent", LogLevel.Error);
            #endregion

            await JsonEmoteDataSave(exportDataJson);
            await JsonDataSave(maleCMMMJson);

            return;
        }
    }
}
