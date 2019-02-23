﻿using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using CompatApiClient;
using CompatApiClient.POCOs;
using CompatApiClient.Utils;
using CompatBot.Database.Providers;
using CompatBot.EventHandlers;
using CompatBot.EventHandlers.LogParsing.POCOs;
using DSharpPlus;
using DSharpPlus.Entities;
using IrdLibraryClient;
using IrdLibraryClient.IrdFormat;

namespace CompatBot.Utils.ResultFormatters
{
    internal static partial class LogParserResult
    {
        private static readonly Client compatClient = new Client();
        private static readonly IrdClient irdClient = new IrdClient();

        private static readonly RegexOptions DefaultSingleLine = RegexOptions.Compiled | RegexOptions.ExplicitCapture | RegexOptions.IgnoreCase | RegexOptions.Singleline;
        // RPCS3 v0.0.3-3-3499d08 Alpha | HEAD
        // RPCS3 v0.0.4-6422-95c6ac699 Alpha | HEAD
        // RPCS3 v0.0.5-7104-a19113025 Alpha | HEAD
        // RPCS3 v0.0.5-42b4ce13a Alpha | minor
        private static readonly Regex BuildInfoInLog = new Regex(
            @"RPCS3 v(?<version_string>(?<version>(\d|\.)+)(-(?<build>\d+))?-(?<commit>[0-9a-f]+)) (?<stage>\w+) \| (?<branch>[^|]+)( \| Firmware version: (?<fw_version_installed>[^|\r\n]+)( \| (?<unknown>.*))?)?\r?\n" +
            @"(?<cpu_model>[^|@]+)(@\s*(?<cpu_speed>.+)\s*GHz\s*)? \| (?<thread_count>\d+) Threads \| (?<memory_amount>[0-9\.\,]+) GiB RAM( \| (?<cpu_extensions>.*?))?\r?$",
            RegexOptions.Compiled | RegexOptions.ExplicitCapture | RegexOptions.IgnoreCase | RegexOptions.Multiline);

        // rpcs3-v0.0.5-7105-064d0619_win64.7z
        // rpcs3-v0.0.5-7105-064d0619_linux64.AppImage
        private static readonly Regex BuildInfoInUpdate = new Regex(@"rpcs3-v(?<version>(\d|\.)+)(-(?<build>\d+))?-(?<commit>[0-9a-f]+)_", DefaultSingleLine);
        private static readonly Regex VulkanDeviceInfo = new Regex(@"'(?<device_name>.+)' running on driver (?<version>.+)\r?$", DefaultSingleLine);
        private static readonly Regex IntelGpuModel = new Regex(@"Intel\s?(®|\(R\))? (?<gpu_model>(?<gpu_family>(\w| )+Graphics)( (?<gpu_model_number>P?\d+))?)(\s+\(|$)", DefaultSingleLine);

        private static readonly Version MinimumOpenGLVersion = new Version(4, 3);
        private static readonly Version RecommendedOpenGLVersion = new Version(4, 5);
        private static readonly Version MinimumFirmwareVersion = new Version(4, 80);
        private static readonly Version NvidiaFullscreenBugMinVersion = new Version(400, 0);
        private static readonly Version NvidiaFullscreenBugMaxVersion = new Version(499, 99);
        private static readonly Version NvidiaRecommendedOldWindowsVersion = new Version(399, 41);

        private static readonly Dictionary<string, string> KnownDiscOnPsnIds = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase)
        {
            {"BLES00932", "NPEB01202"},
            {"BLUS30443", "NPUB30910"},
            //{"BCJS30022", "NPJA00102"},
            {"BCJS70013", "NPJA00102"},
        };

        private static readonly string[] KnownDisableVertexCacheIds = { "NPEB00258", "NPUB30162", "NPJB00068" };

        private static readonly HashSet<string> KnownBogusLicenses = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase)
        {
            "UP0700-NPUB30932_00-NNKDLFULLGAMEPTB.rap",
            "EP0700-NPEB01158_00-NNKDLFULLGAMEPTB.rap",
        };

        private static readonly TimeSpan OldBuild = TimeSpan.FromDays(30);
        private static readonly TimeSpan VeryOldBuild = TimeSpan.FromDays(60);
        //private static readonly TimeSpan VeryVeryOldBuild = TimeSpan.FromDays(90);
        private static readonly TimeSpan AncientBuild = TimeSpan.FromDays(180);
        private static readonly TimeSpan PrehistoricBuild = TimeSpan.FromDays(365);

        private static readonly char[] PrioritySeparator = {' '};
        private static readonly string[] EmojiPriority = { "😱", "💢", "‼", "❗",  "❌", "⁉", "⚠", "❔", "✅", "ℹ" };
        private const string EnabledMark = "[x]";
        private const string DisabledMark = "[ ]";

        public static async Task<DiscordEmbedBuilder> AsEmbedAsync(this LogParseState state, DiscordClient client, DiscordMessage message)
        {
            DiscordEmbedBuilder builder;
            var collection = state.CompleteCollection ?? state.WipCollection;
            if (collection?.Count > 0)
            {
                if (collection["serial"] is string serial
                    && KnownDiscOnPsnIds.TryGetValue(serial, out var psnSerial)
                    && collection["ldr_game_serial"] is string ldrGameSerial
                    && ldrGameSerial.StartsWith("NP", StringComparison.InvariantCultureIgnoreCase)
                    && ldrGameSerial.Equals(psnSerial, StringComparison.InvariantCultureIgnoreCase))
                {
                    collection["serial"] = psnSerial;
                    collection["game_category"] = "HG";
                }
                var gameInfo = await client.LookupGameInfoAsync(collection["serial"], collection["game_title"], true).ConfigureAwait(false);
                builder = new DiscordEmbedBuilder(gameInfo) {ThumbnailUrl = null}; // or this will fuck up all formatting
                if (state.Error == LogParseState.ErrorCode.PiracyDetected)
                {
                    state.PiracyContext = state.PiracyContext.Sanitize();
                    var msg = "__You are being denied further support until you legally dump the game__.\n" +
                              "Please note that the RPCS3 community and its developers do not support piracy.\n" +
                              "Most of the issues with pirated dumps occur due to them having been tampered with in some way " +
                              "and therefore act unpredictably on RPCS3.\n" +
                              "If you need help obtaining legal dumps, please read [the quickstart guide](https://rpcs3.net/quickstart).";
                    builder.WithColor(Config.Colors.LogAlert)
                        .WithTitle("Pirated release detected")
                        .WithDescription(msg);
                }
                else
                {
                    CleanupValues(collection);
                    BuildInfoSection(builder, collection);
                    var colA = BuildCpuSection(collection);
                    var colB = BuildGpuSection(collection);
                    BuildSettingsSections(builder, collection, colA, colB);
                    BuildLibsSection(builder, collection);
                    await BuildNotesSectionAsync(builder, state, collection, client).ConfigureAwait(false);
                }
            }
            else
            {
                builder = new DiscordEmbedBuilder
                {
                    Description = "Log analysis failed, most likely cause is an empty log. Please try again.",
                    Color = Config.Colors.LogResultFailed,
                };
            }
            builder.AddAuthor(client, message);
            return builder;
        }

        private static void CleanupValues(NameValueCollection items)
        {
            if (items["strict_rendering_mode"] == "true")
                items["resolution_scale"] = "Strict Mode";
            if (items["spu_threads"] == "0")
                items["spu_threads"] = "Auto";
            if (items["spu_secondary_cores"] != null)
                items["thread_scheduler"] = items["spu_secondary_cores"];
            if (items["vulkan_initialized_device"] != null)
                items["gpu_info"] = items["vulkan_initialized_device"];
            else if (items["driver_manuf_new"] != null)
                items["gpu_info"] = items["driver_manuf_new"];
            else if (items["vulkan_gpu"] != "\"\"")
                items["gpu_info"] = items["vulkan_gpu"];
            else if (items["d3d_gpu"] != "\"\"")
                items["gpu_info"] = items["d3d_gpu"];
            else if (items["driver_manuf"] != null)
                items["gpu_info"] = items["driver_manuf"];
            if (!string.IsNullOrEmpty(items["gpu_info"]))
                items["driver_version_info"] = GetOpenglDriverVersion(items["gpu_info"], (items["driver_version_new"] ?? items["driver_version"])) ??
                                               GetVulkanDriverVersion(items["vulkan_initialized_device"], items["vulkan_found_device"]) ??
                                               GetVulkanDriverVersionRaw(items["gpu_info"], items["vulkan_driver_version_raw"]);
            if (items["driver_version_info"] != null)
                items["gpu_info"] += $" ({items["driver_version_info"]})";

            if (items["vulkan_compatible_device_name"] is string vulkanDevices)
            {
                var deviceNames = vulkanDevices.Split(Environment.NewLine)
                    .Distinct()
                    .Select(n => $"{n} ({GetVulkanDriverVersion(n, items["vulkan_found_device"])})");
                items["gpu_available_info"] = string.Join(Environment.NewLine, deviceNames);
            }

            if (items["af_override"] is string af)
            {
                if (af == "0")
                    items["af_override"] = "Auto";
                else if (af == "1")
                    items["af_override"] = "Disabled";
            }
            if (items["lib_loader"] is string libLoader)
            {
                var auto = libLoader.Contains("auto", StringComparison.InvariantCultureIgnoreCase);
                var manual = libLoader.Contains("manual", StringComparison.InvariantCultureIgnoreCase);
                if (auto && manual)
                    items["lib_loader"] = "Auto & manual select";
                else if (auto)
                    items["lib_loader"] = "Auto";
                else if (manual)
                    items["lib_loader"] = "Manual selection";
            }
            if (items["win_path"] != null)
                items["os_path"] = "Windows";
            else if (items["lin_path"] != null)
                items["os_path"] = "Linux";
            if (items["library_list"] is string libs)
            {
                var libList = libs.Split('\n').Select(l => l.Trim(' ', '\t', '-', '\r', '[', ']')).Where(s => !string.IsNullOrEmpty(s)).ToList();
                items["library_list"] = libList.Count > 0 ? string.Join(", ", libList) : "None";
            }
            else
                items["library_list"] = "None";

            foreach (var key in items.AllKeys)
            {
                var value = items[key];
                if ("true".Equals(value, StringComparison.CurrentCultureIgnoreCase))
                    value = EnabledMark;
                else if ("false".Equals(value, StringComparison.CurrentCultureIgnoreCase))
                    value = DisabledMark;
                items[key] = value.Sanitize(false);
            }
        }

        private static void PageSection(DiscordEmbedBuilder builder, string notesContent, string sectionName)
        {
            if (!string.IsNullOrEmpty(notesContent))
            {
                var fields = new EmbedPager().BreakInFieldContent(notesContent.Split(Environment.NewLine), 100).ToList();
                if (fields.Count > 1)
                    for (var idx = 0; idx < fields.Count; idx++)
                        builder.AddField($"{sectionName} #{idx + 1} of {fields.Count}", fields[idx].content);
                else
                    builder.AddField(sectionName, fields[0].content);
            }
        }

        private static async Task<UpdateInfo> CheckForUpdateAsync(NameValueCollection items)
        {
            if (!(items["build_and_specs"] is string buildAndSpecs))
                return null;

            var buildInfo = BuildInfoInLog.Match(buildAndSpecs.ToLowerInvariant());
            if (!buildInfo.Success)
                return null;

            var currentBuildCommit = items["build_commit"];
            if (string.IsNullOrEmpty(currentBuildCommit))
                currentBuildCommit = null;
            var updateInfo = await compatClient.GetUpdateAsync(Config.Cts.Token, currentBuildCommit).ConfigureAwait(false);
            if (updateInfo?.ReturnCode != 1 && currentBuildCommit != null)
                updateInfo = await compatClient.GetUpdateAsync(Config.Cts.Token).ConfigureAwait(false);
            var link = updateInfo?.LatestBuild?.Windows?.Download ?? updateInfo?.LatestBuild?.Linux?.Download;
            if (string.IsNullOrEmpty(link))
                return null;

            var latestBuildInfo = BuildInfoInUpdate.Match(link.ToLowerInvariant());
            if (latestBuildInfo.Success && VersionIsTooOld(buildInfo, latestBuildInfo, updateInfo))
                return updateInfo;

            return null;

        }

        private static bool VersionIsTooOld(Match log, Match update, UpdateInfo updateInfo)
        {
            if ((updateInfo.GetUpdateDelta() is TimeSpan updateTimeDelta) && (updateTimeDelta < Config.BuildTimeDifferenceForOutdatedBuilds))
                return false;

            if (Version.TryParse(log.Groups["version"].Value, out var logVersion) && Version.TryParse(update.Groups["version"].Value, out var updateVersion))
            {
                if (logVersion < updateVersion)
                    return true;

                if (int.TryParse(log.Groups["build"].Value, out var logBuild) && int.TryParse(update.Groups["build"].Value, out var updateBuild))
                {
                    if (logBuild + Config.BuildNumberDifferenceForOutdatedBuilds < updateBuild)
                        return true;
                }
                return false;
            }
            return !SameCommits(log.Groups["commit"].Value, update.Groups["commit"].Value);
        }

        private static bool SameCommits(string commitA, string commitB)
        {
            if (string.IsNullOrEmpty(commitA) && string.IsNullOrEmpty(commitB))
                return true;

            if (string.IsNullOrEmpty(commitA) || string.IsNullOrEmpty(commitB))
                return false;

            var len = Math.Min(commitA.Length, commitB.Length);
            return commitA.Substring(0, len) == commitB.Substring(0, len);
        }

        private static string GetOpenglDriverVersion(string gpuInfo, string version)
        {
            if (string.IsNullOrEmpty(version))
                return null;

            if (gpuInfo.Contains("Radeon", StringComparison.InvariantCultureIgnoreCase) ||
                gpuInfo.Contains("AMD", StringComparison.InvariantCultureIgnoreCase) ||
                gpuInfo.Contains("ATI ", StringComparison.InvariantCultureIgnoreCase))
                return AmdDriverVersionProvider.GetFromOpenglAsync(version).GetAwaiter().GetResult();

            return version;
        }

        private static string GetVulkanDriverVersion(string gpu, string foundDevices)
        {
            if (string.IsNullOrEmpty(gpu) || string.IsNullOrEmpty(foundDevices))
                return null;

            var info = (from line in foundDevices.Split(Environment.NewLine)
                    let m = VulkanDeviceInfo.Match(line)
                    where m.Success
                    select m
                ).FirstOrDefault(m => m.Groups["device_name"].Value == gpu);
            var result = info?.Groups["version"].Value;
            if (string.IsNullOrEmpty(result))
                return null;

            if (gpu.Contains("Radeon", StringComparison.InvariantCultureIgnoreCase) ||
                gpu.Contains("AMD", StringComparison.InvariantCultureIgnoreCase) ||
                gpu.Contains("ATI ", StringComparison.InvariantCultureIgnoreCase))
            {
                if (gpu.Contains("RADV", StringComparison.InvariantCultureIgnoreCase))
                    return result;

                return AmdDriverVersionProvider.GetFromVulkanAsync(result).GetAwaiter().GetResult();
            }

            if (result.EndsWith(".0.0"))
                result = result.Substring(0, result.Length - 4);
            if (result.Length > 3 && result[result.Length - 2] == '.')
                result = result.Substring(0, result.Length - 1) + "0" + result[result.Length - 1];
            return result;
        }

        private static string GetVulkanDriverVersionRaw(string gpuInfo, string version)
        {
            if (string.IsNullOrEmpty(version))
                return null;

            var ver = int.Parse(version);
            if (IsAmd(gpuInfo))
            {
                var major = (ver >> 22) & 0x3ff;
                var minor = (ver >> 12) & 0x3ff;
                var patch = ver & 0xfff;
                var result = $"{major}.{minor}.{patch}";
                if (gpuInfo.Contains("RADV", StringComparison.InvariantCultureIgnoreCase))
                    return result;

                return AmdDriverVersionProvider.GetFromVulkanAsync(result).GetAwaiter().GetResult();
            }
            else
            {
                var major = (ver >> 22) & 0x3ff;
                var minor = (ver >> 14) & 0xff;
                var patch = ver & 0x3fff;
                if (major == 0 && gpuInfo.Contains("Intel", StringComparison.InvariantCultureIgnoreCase))
                    return $"{minor}.{patch}";

                if (IsNvidia(gpuInfo))
                {
                    if (patch == 0)
                        return $"{major}.{minor}";
                    return $"{major}.{minor:00}.{(patch >> 6) & 0xff}.{patch & 0x3f}";
                }

                return $"{major}.{minor}.{patch}";
            }
        }

        private static bool IsAmd(string gpuInfo)
        {
            return gpuInfo.Contains("Radeon", StringComparison.InvariantCultureIgnoreCase) ||
                   gpuInfo.Contains("AMD", StringComparison.InvariantCultureIgnoreCase) ||
                   gpuInfo.Contains("ATI ", StringComparison.InvariantCultureIgnoreCase);
        }

        private static bool IsNvidia(string gpuInfo)
        {
            return gpuInfo.Contains("GeForce", StringComparison.InvariantCultureIgnoreCase) ||
                   gpuInfo.Contains("nVidia", StringComparison.InvariantCultureIgnoreCase) ||
                   gpuInfo.Contains("Quadro", StringComparison.InvariantCultureIgnoreCase);
        }

        private static string GetTimeFormat(long microseconds)
        {
            if (microseconds < 1000)
                return $"{microseconds} µs";
            if (microseconds < 1_000_000)
                return $"{microseconds / 1000.0:0.##} ms";
            return $"{microseconds / 1_000_000.0:0.##} s";
        }

        private static List<string> SortLines(List<string> notes, DiscordEmoji piracyEmoji = null)
        {
            if (notes == null || notes.Count < 2)
                return notes;

            var priorityList = new List<string>(EmojiPriority);
            if (piracyEmoji != null)
                priorityList.Insert(0, piracyEmoji.ToString());
            return notes
                .Select(s =>
                        {
                            var prioritySymbol = s.Split(PrioritySeparator, 2)[0];
                            var priority = priorityList.IndexOf(prioritySymbol);
                            return new
                            {
                                priority = priority == -1 ? 69 : priority,
                                line = s
                            };
                        })
                .OrderBy(i => i.priority)
                .Select(i => i.line)
                .ToList();
        }

        internal static DiscordEmbedBuilder AddAuthor(this DiscordEmbedBuilder builder, DiscordClient client, DiscordMessage message)
        {
            if (message != null)
            {
                var author = message.Author;
                var member = client.GetMember(message.Channel?.Guild, author);
                string msg;
                if (member == null)
                    msg = $"Log from {author.Username.Sanitize()} | {author.Id}";
                else
                    msg = $"Log from {member.DisplayName.Sanitize()} | {member.Id}";
#if DEBUG
                msg += " | Test Bot Instance";
#endif
                builder.WithFooter(msg);
            }
            return builder;
        }
    }
}
