using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Discord;
using BepInEx;
using BepInEx.Logging;
using System.Reflection;
using System.IO;
using Receiver2;
using HarmonyLib;
using System;

namespace Receiver2RichPresence
{
	[BepInPlugin(GUID, NAME, VERSION)]
	public class DiscordController : BaseUnityPlugin
	{
		private const string GUID = "CiarenceW.Receiver2Presence";
		private const string NAME = "Receiver 2 Rich Presence";
		private const string VERSION = "1.0.0";

		private const long CLIENT_ID = 1356812717125668977L;

		public const string HARMONY_INSTANCE_ID = "DISCORD_CONTROLLER_HID";

		public Harmony Harmony
		{
			get;
			private set;
		}

		public static DiscordController Instance
		{
			get;
			private set;
		}

		public static Discord.Discord Discord
		{
			get;
			private set;
		}

		public new ManualLogSource Logger
		{
			get;
			private set;
		}

		public Coroutine updateActivityCoroutine;

		public string currentRank;

		public int tapeCount;
		public int tapeTarget;

		public Activity currentActivity = default(Activity); //I'm making this a field so you can just edit it without having to make a new variable and stuff

		private Activity previousActivity;

		private string currentGunString;

		private void Awake()
		{
			Logger = base.Logger;

			Instance = this;

			Assembly.LoadFrom(Path.Combine(Path.GetDirectoryName(this.Info.Location), "DiscordGameSDK.dll")); //probably good to do this

			Discord = new Discord.Discord(CLIENT_ID, (ulong)global::Discord.CreateFlags.NoRequireDiscord);

			Harmony = new Harmony(HARMONY_INSTANCE_ID);

			Harmony.PatchAll(typeof(Hooks));

			var activityManager = Discord.GetActivityManager();

			updateActivityCoroutine = StartCoroutine(UpdateActivityCoroutine());

			Receiver2ModdingKit.ModdingKitEvents.AddTaskAtCoreStartup(SetUpHooks);

			Logger.LogInfo($"{GUID} version {VERSION} loaded!");
		}

		public IEnumerator UpdateActivityCoroutine() //put it in a coroutine to prevent getting rate limited, ensure stuff always actually correctly updates, doesn't get lost anywhere
		{
			while (true)
			{
				if (!previousActivity.Equals(currentActivity))
				{
					previousActivity = currentActivity;
					Discord.GetActivityManager().UpdateActivity(currentActivity, RunActivityCallback);
				}
				yield return new WaitForSecondsRealtime(2f);
			}
		}

		private void Update()
		{
			if (LocalAimHandler.TryGetInstance(out var lah) && lah.TryGetGun(out var gun))
			{
				currentActivity.Assets.SmallText = Locale.GetGunName(gun.InternalName);

				string assetName;

				if (gun.InternalName.StartsWith("wolfire.") && gun.gun_model != GunModel.Debug)
				{
					assetName = gun.InternalName.Substring("wolfire.".Length);

					if (gun.InternalName.EndsWith("_gold"))
					{
						assetName = assetName.Substring(0, assetName.Length - "_gold".Length);
					}
				}
				else
				{
					assetName = "question_mark"; //for modded guns, and other stuff :)
				}

				currentGunString = assetName;

				currentActivity.Assets.SmallImage = assetName;
			}
			else
			{
				currentActivity.Assets.SmallText = string.Empty;
				currentActivity.Assets.SmallImage = string.Empty;
			}

			Discord.RunCallbacks();
		}

		public static void RunActivityCallbackStatic(global::Discord.Result res) => Instance.RunActivityCallback(res);

		public void RunActivityCallback(global::Discord.Result res)
		{
			if (res == global::Discord.Result.Ok) //Fucking C? what the fuck dude
			{
				Logger.LogMessage("Successfully updated activity");
			}
			else
			{
				Logger.LogError($"Unsuccesfully updated activity, oops: {res}");
			}
		}

		void SetUpHooks()
		{
			ReceiverEvents.StartListening(ReceiverEventTypeVoid.PlayerInitialized, Hooks.OnPlayerInitialized);
		}

		public static string GetDetailsForGameMode()
		{
			switch (ReceiverCoreScript.Instance().game_mode.GetGameMode())
			{
				case GameMode.Classic:
					ClassicGameMode classicGameMode = (ClassicGameMode)ReceiverCoreScript.Instance().game_mode;
					if (RuntimeTileLevelGenerator.TryGetInstance(out var rtlg))
					{

					}
					else
					{
						Instance.Logger.LogWarning("Player is in classic gamemode, but could not find RuntimeTileLevelGenerator");
					}
					break;
				default:
					break;
			}

			return "";
		}

		public static string[] ranks = new string[]
		{
		"Introduction",
		"Baseline",
		"Asleep",
		"Sleepwalker",
		"Liminal",
		"Awake"
		};

		//gets the target name and returns the proper gun name
		public static Dictionary<string, string> properGunName = new Dictionary<string, string>()
	{
		{ "1911_explosion_view(Clone)", "Colt M1911" },
		{ "colt_detective_explosion_view(Clone)", "Colt Detective Special" },
		{ "deagle_explosion view(Clone)", "Magnum Research Desert Eagle" },
		{ "glock17_explosion_view(Clone)", "Glock 17" },
		{ "hipoint_explosion_view(Clone)", "HiPoint C9" },
		{ "m9_explosion_view(Clone)", "Beretta M9" },
		{ "model10_explosion_view(Clone)", "Smith & Wesson Model 10" },
		{ "saa explosion view(Clone)", "Colt Single Action Army" },
		{ "sig226 explosion view(Clone)", "Sig Sauer P226" },
		{ "akm_explosion_view(Clone)", "AKM (lmao)" },
		{ "ruger_lc9_explosion_view(Clone)", "Ruger LC9 (lmao)" },
		{ "ruger_lcr_explosion_view(Clone)", "Ruger LCR (lmao)" },
		{ "ar15_explosion_view(Clone)", "AR15 (lmao)" },
		{ "mp_compact_explosion_view(Clone)", "Smith & Wesson M&P Compact (lmao)" },
		{ "ruger_mk1_explosion_view(Clone)", "Ruger MK1 (lmao)" }
	};

		public static string GetDetailsForRankingProgressionCampaign(string rank, int tapeCount, int tapeTarget)
		{
			return $"{rank}: {tapeCount} out of {tapeTarget} tapes";
		}

		public class Hooks
		{
			public static void OnPlayerInitialized(ReceiverEventTypeVoid ev)
			{
				string gameModeString;

				switch (ReceiverCoreScript.Instance().game_mode.GetGameMode())
				{
					case GameMode.Classic:
						gameModeString = "On a nostalgia trip";
						break;
					case GameMode.RankingCampaign:
						gameModeString = "In The Dreaming";
						var rpgm = (RankingProgressionGameMode)ReceiverCoreScript.Instance().game_mode;
						Instance.currentRank = Locale.GetUIString(rpgm.GetReceiverRankName(rpgm.progression_data.receiver_rank));
						break;
					case GameMode.ShootingRange:
						gameModeString = "In the shooting range";
						break;
					case GameMode.TestDrive:
						gameModeString = "Testing stuff";
						break;
					case GameMode.ReceiverMall:
						gameModeString = "In the compound";
						break;
					default:
						gameModeString = "Somewhere, far away";
						break;
				}

				Instance.currentActivity = new global::Discord.Activity()
				{
					State = gameModeString,
					Timestamps = new ActivityTimestamps()
					{
						Start = DateTimeOffset.Now.ToUnixTimeSeconds()
					}
				};

				Instance.Logger.LogInfo("OnPlayerInitialized: " + Instance.currentRank);
				Instance.currentActivity.Details = GetDetailsForRankingProgressionCampaign(Instance.currentRank, Instance.tapeCount, Instance.tapeTarget);
			}

			[HarmonyPatch(typeof(PlayerGUITapeCounter), nameof(PlayerGUITapeCounter.UpdateCounter))]
			[HarmonyPostfix]
			private static void OnTapeCounterUpdate(int picked_up, int target)
			{
				Instance.tapeCount = picked_up;
				Instance.tapeTarget = target;

				Instance.Logger.LogInfo("OnTapeCounterUpdate: " + Instance.currentRank);
				Instance.currentActivity.Details = GetDetailsForRankingProgressionCampaign(Instance.currentRank, picked_up, target);
			}

			[HarmonyPatch(typeof(RuntimeTileLevelGenerator), nameof(RuntimeTileLevelGenerator.ActivateRooms))]
			[HarmonyPostfix]
			private static void OnPlayerChangeTile(RuntimeTileLevelGenerator __instance)
			{
				var largeText = __instance.GetCurrentTile().tile_type_string == "M11_Victorian" ? "Victorian" : PlayerInfoMessages.GetTileName(__instance.GetCurrentTile().tile_type_string);

				Instance.currentActivity.Assets = new ActivityAssets()
				{
					LargeImage = __instance.GetCurrentTile().info.name,
					LargeText = largeText
				};
			}

			[HarmonyPatch(typeof(GunInspectionCamera), "Awake")]
			[HarmonyPostfix]
			private static void OnPlayerStartGunInspectionScene(GunInspectionCamera __instance)
			{
				Instance.currentActivity = new Activity()
				{
					State = "In the gun inspection scene",
					Details = $"Inspecting the {properGunName[__instance.target.name]}",
					Timestamps = new ActivityTimestamps()
					{
						Start = DateTimeOffset.Now.ToUnixTimeSeconds()
					}
				};
			}

			[HarmonyPatch(typeof(GunInspectionCamera), "CycleGun")]
			[HarmonyPostfix]
			private static void OnPlayerCycleGun(GunInspectionCamera __instance)
			{
				Instance.currentActivity.Details = $"Inspecting the {__instance.target.name}";
				Instance.currentActivity.Timestamps = new ActivityTimestamps()
				{
					Start = DateTimeOffset.Now.ToUnixTimeSeconds()
				};
			}
		}
	}
}
