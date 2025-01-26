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
using CUE4Parse.Compression;
using CUE4Parse.UE4.Assets.Objects;
using static System.Runtime.InteropServices.JavaScript.JSType;

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

                Provider = new DefaultFileProvider(FortniteUtils.PaksPath, SearchOption.AllDirectories, false, new VersionContainer(EGame.GAME_UE5_5));
                Provider.Initialize();

                var keys = new List<KeyValuePair<FGuid, FAesKey>>
                {
                    new KeyValuePair<FGuid, FAesKey>(new FGuid(), new FAesKey(aes.MainKey))
                };

                keys.AddRange(aes.DynamicKeys.Select(x => new KeyValuePair<FGuid, FAesKey>(new Guid(x.PakGuid), new FAesKey(x.Key))));
                await Provider.SubmitKeysAsync(keys);
                Logger.Log($"File provider initalized with {Provider.Keys.Count} keys", LogLevel.Cue4);

                var oodlePath = Path.Combine(Constants.DataPath, OodleHelper.OODLE_DLL_NAME);
                if (File.Exists(OodleHelper.OODLE_DLL_NAME))
                {
                    File.Move(OodleHelper.OODLE_DLL_NAME, oodlePath, true);
                }
                else if (!File.Exists(oodlePath))
                {
                    await OodleHelper.DownloadOodleDllAsync(oodlePath);
                }

                OodleHelper.Initialize(oodlePath);

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

            var latestUsmapInfo = new DirectoryInfo(Constants.DataPath).GetFiles("*_oo.usmap").FirstOrDefault(x => x.Name == mappingsData.FileName);
            Logger.Log($"Mappings Pulled from file: {latestUsmapInfo.Name}", LogLevel.Cue4);
            return new FileUsmapTypeMappingsProvider(latestUsmapInfo.FullName);
        }

        private static EmoteData exportDataJson = new EmoteData();

        public static async Task Export(string EID)
        {
            CurrentId = EID;

            #region Ready
            UObject eidObj;

            eidObj = await Provider.LoadObjectAsync($"FortniteGame/Plugins/GameFeatures/BRCosmetics/Content/Athena/Items/Cosmetics/Dances/{EID}");

            try
            {
                eidObj = await Provider.LoadObjectAsync($"FortniteGame/Plugins/GameFeatures/BRCosmetics/Content/Athena/Items/Cosmetics/Dances/{EID}"); //FortniteGame/Plugins/GameFeatures/BRCosmetics/Content/Athena/Items/Cosmetics/Dances/EID_BerryTart.uasset
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

            if(eidObj is null)
            {
                Logger.Log("Failed Getting object", LogLevel.Error);
                return;
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

            if (!eidObj.TryGetValue(out FText displayName, "ItemName"))
                Logger.Log("Error Getting DisplayName", LogLevel.Error);

            if (!eidObj.TryGetValue(out FText description, "ItemDescription"))
                Logger.Log("Error Getting Description", LogLevel.Error);

            if(eidObj.TryGetValue(out bool bMovingEmote, "bMovingEmote"))
            {
                exportDataJson.IsMovingEmote = bMovingEmote;
                
                if(eidObj.TryGetValue(out bool bMoveForwardOnly, "bMoveForwardOnly"))
                    exportDataJson.IsMoveForwardOnly = bMoveForwardOnly;

                if(eidObj.TryGetValue(out float walkForwardSpeed, "WalkForwardSpeed"))
                    exportDataJson.WalkForwardSpeed = walkForwardSpeed;
            }

            exportDataJson.Description = description.Text ?? "FAILED";
            exportDataJson.Name = displayName.Text ?? "FAILED";

            #endregion

            #region Stop
            Logger.Log($"Exporting Male Animations {CMM_Montage.Name}", LogLevel.Cue4);
            var CMMObject = await Provider.LoadObjectAsync(CMM_Montage.GetPathName());

            if (CMMObject.TryGetValue(out FSlotAnimationTrack[] slotAnimTracks, "SlotAnimTracks"))
            {
                await ExportAnimations(CMMObject, EGender.Male, slotAnimTracks);
            }

            Logger.Log($"Exporting Female Animations {CMF_Montage.Name}", LogLevel.Cue4);
            var CMFObject = await Provider.LoadObjectAsync(CMF_Montage.GetPathName());

            if (CMFObject.TryGetValue(out FSlotAnimationTrack[] slotAnimTracksF, "SlotAnimTracks"))
            {
                await ExportAnimations(CMFObject, EGender.Female, slotAnimTracksF);
            }

            Directory.CreateDirectory(MiscPath());

            await ExportIcons(eidObj);

            Logger.Log("Exporting Audio", LogLevel.Cue4);
            await ExportAudio(CMMObject);

            await JsonDataSave(JsonConvert.SerializeObject(CMMObject, Formatting.Indented));
            await GetLastData();
            await JsonEmoteDataSave(exportDataJson);
            #endregion
        }

        private static async Task ExportAnimations(UObject uObject, EGender gender = EGender.Male, FSlotAnimationTrack[] slotAnimTracks = null)
        {
            bool isAdictive = false;

            if (gender is EGender.Female)
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
                await ExportAnimation(addUAnimSequence);
            }
            else
            {
                var refUAnimSequence = await Provider.LoadObjectAsync<UAnimSequence>(animReference.GetPathName());
                await ExportAnimation(refUAnimSequence);
            }

            if (exportDataJson.IsMovingEmote)
            {
                var bodyMotion = slotAnimTracks.FirstOrDefault(x => x.SlotName.PlainText is "FullBodyInMotion");
                if (bodyMotion != null)
                {
                    var animReferenceMot = await bodyMotion.AnimTrack.AnimSegments[0].AnimReference.LoadAsync();
                    var animSequence = await Provider.LoadObjectAsync<UAnimSequence>(animReferenceMot.GetPathName());
                    await ExportAnimation(animSequence);
                }
            }
        }

        private static async Task ExportAnimation(UAnimSequence animSequence)
        {
            var exporterOptions = new ExporterOptions()
            {
                AnimFormat = EAnimFormat.ActorX
            };

            var animExporter = new AnimExporter(animSequence, exporterOptions);
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

            Directory.CreateDirectory(IconsPath());

            if (uObject.TryGetValue(out FInstancedStruct[] dataList, "DataList"))
            {
                foreach(var f in dataList)
                {
                    if(f.NonConstStruct!.TryGetValue(out UTexture2D iconSmall, "Icon"))
                    {
                        await File.WriteAllBytesAsync(IconsPath() + $"\\{iconSmall.Name}.png", iconSmall.Decode()!.Encode(SKEncodedImageFormat.Png, 256).ToArray());
                        continue;
                    }

                    if (f.NonConstStruct!.TryGetValue(out UTexture2D largeSmall, "LargeIcon"))
                    {
                        await File.WriteAllBytesAsync(IconsPath() + $"\\{largeSmall.Name}.png", largeSmall.Decode()!.Encode(SKEncodedImageFormat.Png, 512).ToArray());
                        continue;
                    }
                }
            }

            Logger.Log("Exported Icons", LogLevel.Cue4);
        }

        private static async Task ExportAudio(UObject uObject)
        {
            if (uObject.TryGetValue(out FAnimNotifyEvent[] events, "Notifies"))
            {
                foreach (var sound in events)
                    if (sound.NotifyName.PlainText.Contains("FortEmoteSound") || sound.NotifyName.PlainText.Contains("Fort Anim Notify State Emote Sound"))
                    {
                        var musicClass = await sound.NotifyStateClass.LoadAsync();

                        if (musicClass.TryGetValue(out UEmoteMusic emoteSound1P, "EmoteSound1P"))
                        {
                            var soundRandom = await TryGetSoundRandom(emoteSound1P);

                            var soundNode = await soundRandom.ChildNodes.FirstOrDefault().TryLoadAsync<USoundNodeWavePlayer>();

                            var musicBoom = await soundNode.SoundWave.LoadAsync();

                            musicBoom.Decode(false, out var audioFormat, out var data);

                            Directory.CreateDirectory(MiscPath());

                            await File.WriteAllBytesAsync($"{MiscPath()}\\{musicBoom.Name}.blinka", data);

                            await FixSound($"{MiscPath()}\\{musicBoom.Name}.blinka");

                            Logger.Log($"Exported {musicBoom.Name}", LogLevel.Cue4);
                        }
                        else
                            Logger.Log("Error Getting UEmoteMusic", LogLevel.Error);
                    }

            }
            else
                Logger.Log("Error Getting FAnimNotifyEvent", LogLevel.Error);
        }

        private static async Task FixSound(string path)
        {
            if(!File.Exists(Constants.BlinkaExe))
            {
                var bytes = await new HttpClient().GetByteArrayAsync($"https://cdn.0xkaede.xyz/binkadec.exe");

                if(bytes is null)
                    Logger.Log("Decode exe is null");
                else
                    await File.WriteAllBytesAsync(Constants.BlinkaExe, bytes);
            }

            var binkadecProcess = Process.Start(new ProcessStartInfo
            {
                FileName = Constants.BlinkaExe,
                Arguments = $"-i \"{path}\" -o \"{path.Replace("blinka", "wav")}\"",
                UseShellExecute = false,
                CreateNoWindow = true
            });
            binkadecProcess?.WaitForExit(5000);
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
                exportDataJson.FloatCurves.Add(curv.CurveName);
        }
    }
}
