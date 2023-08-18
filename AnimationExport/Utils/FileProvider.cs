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
using CUE4Parse.UE4.Assets.Exports.Rig;

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

        private static EmoteData exportDataJson = new EmoteData();

        public static async Task Export(string EID)
        {
            
            CurrentId = EID;

            #region Ready
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

            #region Stop
            Logger.Log("Exporting Male Animations", LogLevel.Cue4);
            var CMMObject = await Provider.LoadObjectAsync(CMM_Montage.GetPathName());
            await ExportAnimations(CMMObject);

            Logger.Log("Exporting Female Animations", LogLevel.Cue4);
            var CMFObject = await Provider.LoadObjectAsync(CMF_Montage.GetPathName());
            await ExportAnimations(CMFObject, EGender.Female);

            await ExportIcons(eidObj);

            Logger.Log("Exporting Audio", LogLevel.Cue4);
            await ExportAudio(CMMObject);

            await JsonDataSave(JsonConvert.SerializeObject(CMMObject, Formatting.Indented));
            await GetLastData();
            await JsonEmoteDataSave(exportDataJson);
            #endregion
        }

        private static async Task ExportAnimations(UObject uObject, EGender gender = EGender.Male)
        {
            bool isAdictive = false;

            if (uObject.TryGetValue(out FSlotAnimationTrack[] slotAnimTracks, "SlotAnimTracks"))
            {
                if(gender is EGender.Female)
                    isAdictive = slotAnimTracks.FirstOrDefault(x => x.SlotName.PlainText == "AdditiveCorrective") is null ? false : true;

                exportDataJson.IsAddictive = isAdictive;

                var currentSlotName = isAdictive ? "FullBody" : "AdditiveCorrective";

                var fullBodyAnimTrack = slotAnimTracks.FirstOrDefault(x => x.SlotName.PlainText == "FullBody");
                
                if (fullBodyAnimTrack is null)
                {
                    Logger.Log($"Cant find FullBody", LogLevel.Error);
                    return;
                }

                var animReference = await fullBodyAnimTrack.AnimTrack.AnimSegments[0].AnimReference.LoadAsync();

                if (isAdictive)
                {
                    var additiveCorrectiveAnimTrack = slotAnimTracks.FirstOrDefault(x => x.SlotName.PlainText == "AdditiveCorrective");
                    var additiveCorrectiveanimReference = await additiveCorrectiveAnimTrack.AnimTrack.AnimSegments[0].AnimReference.LoadAsync();
                    var refUAnimSequence = await Provider.LoadObjectAsync<UAnimSequence>(animReference.GetPathName());
                    var addUAnimSequence = await Provider.LoadObjectAsync<UAnimSequence>(additiveCorrectiveanimReference.GetPathName());
                    addUAnimSequence.RefPoseSeq = new ResolvedLoadedObject(refUAnimSequence);
                    await ExportAnimations(addUAnimSequence);
                }
                else
                {
                    var animSequence = await Provider.LoadObjectAsync<UAnimSequence>(animReference.GetPathName());
                    await ExportAnimations(animSequence);
                }
                
            }
            else
                Logger.Log("Error Getting FSlotAnimationTrack", LogLevel.Error);
        }

        private static async Task ExportAnimations(UAnimSequence animSequence)
        {
            var animExporter = new AnimExporter(animSequence, new ExporterOptions());
            animExporter.TryWriteToDir(new DirectoryInfo(Constants.ExportPath), out var label, out var fileName);

            Logger.Log($"Exported {Path.GetFileNameWithoutExtension(fileName)}", LogLevel.Cue4);

            if (!Directory.Exists(AnimationsPath()))
                Directory.CreateDirectory(AnimationsPath());

            fileName.MoveAnimations();

            Directory.CreateDirectory($"{JsonPath()}");
            await File.WriteAllTextAsync(JsonPath() + $"\\{Path.GetFileNameWithoutExtension(fileName)}.json",
                JsonConvert.SerializeObject(new List<UAnimSequence>() { animSequence }, Formatting.Indented));
        }

        private static async Task ExportIcons(UObject uObject)
        {
            Logger.Log("Exporting Icons", LogLevel.Cue4);

            if (!uObject.TryGetValue(out UTexture2D iconSmall, "SmallPreviewImage"))
                Logger.Log("Cant find SmallPreviewImage");

            Directory.CreateDirectory(IconsPath());

            await File.WriteAllBytesAsync(IconsPath() + $"\\{iconSmall.Name}.png", iconSmall.Decode()!.Encode(SKEncodedImageFormat.Png, 256).ToArray());

            if(!uObject.TryGetValue(out UTexture2D largeSmall, "LargePreviewImage"))
                Logger.Log("Cant find SmallPreviewImage");

            await File.WriteAllBytesAsync(IconsPath() + $"\\{largeSmall.Name}.png", largeSmall.Decode()!.Encode(SKEncodedImageFormat.Png, 512).ToArray());
            Logger.Log("Exported Icons", LogLevel.Cue4);
        }

        private static async Task ExportAudio(UObject uObject)
        {
            if (uObject.TryGetValue(out FAnimNotifyEvent[] events, "Notifies"))
            {
                foreach (var sound in events)
                    if (sound.NotifyName.PlainText.Contains("FortEmoteSound"))
                    {
                        var musicClass = await sound.NotifyStateClass.LoadAsync();

                        if (musicClass.TryGetValue(out UEmoteMusic emoteSound1P, "EmoteSound1P"))
                        {
                            var soundRandom = await TryGetSoundRandom(emoteSound1P);

                            var soundNode = await soundRandom.ChildNodes.FirstOrDefault().TryLoadAsync<USoundNodeWavePlayer>();

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
                        else
                            Logger.Log("Error Getting UEmoteMusic", LogLevel.Error);
                    }
            }
            else
                Logger.Log("Error Getting FAnimNotifyEvent", LogLevel.Error);
        }

        private static async Task<USoundNodeRandom> TryGetSoundRandom(UEmoteMusic uEmoteMusic)
        {
            try
            {
                var data = await uEmoteMusic.FirstNode.LoadAsync<UFortSoundNodeLicensedContentSwitcher>();
                return await data.ChildNodes[1].LoadAsync<USoundNodeRandom>();
            }
            catch
            {
                return await uEmoteMusic!.FirstNode.LoadAsync<USoundNodeRandom>();
            }
        }

        private static async Task GetLastData()
        {
            var data = await GetMontageData();

            exportDataJson.Blend.Add("BlendIn", data.Properties.BlendIn.BlendTime);
            exportDataJson.Blend.Add("BlendOut", data.Properties.BlendOut.BlendTime);

            foreach (var curv in data.Properties.RawCurveData.FloatCurves)
                exportDataJson.FloatCurves.Add(curv.Name.DisplayName);
        }
    }
}
