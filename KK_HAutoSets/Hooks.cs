﻿using System.Collections.Generic;
using Harmony;
using UnityEngine;
using Manager;
using System;
using static KK_HAutoSets.HAutoSets;

namespace KK_HAutoSets
{
	public static class Hooks
	{
		private static float maleGaugeOld = -1;
		private static bool houshiRestoreGauge;

		//This should hook to a method that loads as late as possible in the loading phase
		//Hooking method "MapSameObjectDisable" because: "Something that happens at the end of H scene loading, good enough place to hook" - DeathWeasel1337/Anon11
		//https://github.com/DeathWeasel1337/KK_Plugins/blob/master/KK_EyeShaking/KK.EyeShaking.Hooks.cs#L20
		[HarmonyPostfix]
		[HarmonyPatch(typeof(HSceneProc), "MapSameObjectDisable")]
		public static void HSceneProcLoadPostfix(HSceneProc __instance)
		{
			var females = (List<ChaControl>)Traverse.Create(__instance).Field("lstFemale").GetValue();
			sprites.Clear();
			sprites.Add(__instance.sprite);
			List<ChaControl> males = new List<ChaControl>
			{
				(ChaControl)Traverse.Create(__instance).Field("male").GetValue(),
				(ChaControl)Traverse.Create(__instance).Field("male1").GetValue()
			};

			lstProc = (List<HActionBase>)Traverse.Create(__instance).Field("lstProc").GetValue();
			flags = __instance.flags;
			lstFemale = females;
			voice = __instance.voice;
			hands[0] = __instance.hand;
			hands[1] = __instance.hand1;

			EquipAllAccessories(females);
			foreach (HSprite sprite in sprites)
				LockGaugesAction(sprite);

			HideShadow(males, females);

			if (AutoVoice.Value == SpeechMode.Timer)
				SetVoiceTimer(2f);

			animationToggle = __instance.gameObject.AddComponent<AnimationToggle>();
		}

		[HarmonyPostfix]
		[HarmonyPatch(typeof(HSceneProc), "LateUpdate")]
		public static void HSceneLateUpdatePostfix()
		{
			GaugeLimiter();
		}

		[HarmonyPrefix]
		[HarmonyPatch(typeof(HActionBase), "IsBodyTouch")]
		public static bool IsBodyTouchPre(bool __result)
		{
			if (DisableHideBody.Value)
			{
				__result = false;
				return false;
			}
			return true;
		}

		/// <summary>
		/// The vanilla game does not have any moan or breath sounds available for the precum (OLoop) animation.
		/// This patch makes the game play sound effects as if it's in strong loop when the game is in fact playing OLoop without entering cum,
		/// such as when forced by this plugin.
		/// </summary>
		[HarmonyPrefix]
		[HarmonyPatch(typeof(HVoiceCtrl), "BreathProc")]
		public static void BreathProcPre(ref AnimatorStateInfo _ai)
		{
			if (animationToggle.forceOLoop && flags.nowAnimStateName.Contains("OLoop"))
				_ai = AnimationToggle.sLoopInfo;
		}

		/// <summary>
		/// When the stop voice flag is set, this patch makes calls to check currently playing speech to return false,
		/// effectively allowing the game to interrupt the current playing speech.
		/// </summary>
		[HarmonyPrefix]
		[HarmonyPatch(typeof(Voice), "IsVoiceCheck", new Type[] { typeof(Transform), typeof(bool) })]
		public static bool IsVoiceCheckPre(ref bool __result)
		{
			if (animationToggle.forceStopVoice)
			{
				__result = false;
				return false;
			}
			else if (PrecumExtend.Value && animationToggle.orgasmTimer > 0)
			{
				__result = true;
				return false;
			}
			else
			{
				return true;
			}			
		}

		/// <summary>
		/// Resets OLoop flag when switching animation, to account for leaving OLoop.
		/// </summary>
		[HarmonyPostfix]
		[HarmonyPatch(typeof(HActionBase), "SetPlay")]
		public static void SetPlayPost()
		{
			if (animationToggle?.forceOLoop ?? false)			
				animationToggle.forceOLoop = false;
		}

		//In sex modes, force the game to play idle voice line while forceIdleVoice is true
		[HarmonyPrefix]
		[HarmonyPatch(typeof(HFlag.VoiceFlag), "IsSonyuIdleTime")]
		public static bool IsSonyuIdleTimePre(ref bool __result)
		{
			if (forceIdleVoice)
			{
				__result = true;
				return false;
			}
			//If speech control is not disabled and timer has a positive value, 
			//make this method return false so that default idle speech will not trigger during countdown or mute idle mode.
			//The timer would have a positive value if it's currently counting down in timer mode, or at its default positive value if in mute idle mode.
			else if (voiceTimer > 0 && AutoVoice.Value != SpeechMode.Disabled)
			{
				__result = false;
				return false;
			}

			return true;
		}

		//In service modes, force the game to play idle voice line while forceIdleVoice is true
		[HarmonyPrefix]
		[HarmonyPatch(typeof(HFlag.VoiceFlag), "IsHoushiIdleTime")]
		public static bool IsHoushiIdleTimePre(ref bool __result)
		{
			if (forceIdleVoice)
			{
				__result = true;
				return false;
			}
			//If speech control is not disabled and timer has a positive value, 
			//make this method return false so that default idle speech will not trigger during countdown or mute idle mode.
			//The timer would have a positive value if it's currently counting down in timer mode, or at its default positive value if in mute idle mode.
			else if (voiceTimer > 0 && AutoVoice.Value != SpeechMode.Disabled)
			{
				__result = false;
				return false;
			}

			return true;
		}

		//In caress modes, force the game to play idle voice line while forceIdleVoice is true
		[HarmonyPrefix]
		[HarmonyPatch(typeof(HFlag.VoiceFlag), "IsAibuIdleTime")]
		public static bool IsAibuIdleTimePre(ref bool __result)
		{
			if (forceIdleVoice)
			{
				__result = true;
				return false;
			}
			//If speech control is not disabled and timer has a positive value, 
			//make this method return false so that default idle speech will not trigger during countdown or mute idle mode.
			//The timer would have a positive value if it's currently counting down in timer mode, or at its default positive value if in mute idle mode.
			else if (voiceTimer > 0 && AutoVoice.Value != SpeechMode.Disabled)
			{
				__result = false;
				return false;
			}

			return true;
		}

		[HarmonyPrefix]
		[HarmonyPatch(typeof(HVoiceCtrl), "VoiceProc")]
		public static bool VoiceProcPre(ref bool __result)
		{
			if (AutoVoice.Value == SpeechMode.MuteAll && !forceIdleVoice)
			{
				__result = false;
				return false;
			}
			else
				return true;
		}


		[HarmonyPostfix]
		[HarmonyPatch(typeof(HVoiceCtrl), "VoiceProc")]
		public static void VoiceProcPost(bool __result)
		{
			if (__result && AutoVoice.Value == SpeechMode.Timer)
				SetVoiceTimer(2f);
		}

		[HarmonyPostfix]
		[HarmonyPatch(typeof(HSprite), "OnInsideClick")]
		public static void OnInsideClickPost()
		{
			if (PrecumTimer.Value > 0)
				animationToggle.ManualOrgasm(inside: true);
		}

		[HarmonyPostfix]
		[HarmonyPatch(typeof(HSprite), "OnOutsideClick")]
		public static void OnOutsideClickPost()
		{
			if (PrecumTimer.Value > 0)
				animationToggle.ManualOrgasm(inside: false);
		}

		[HarmonyPostfix]
		[HarmonyPatch(typeof(HSprite), "OnSpeedUpClick")]
		public static void OnSpeedUpClickPost()
		{
			if (flags.click == HFlag.ClickKind.motionchange && PrecumToggle.Value && flags.nowAnimStateName.Contains("SLoop"))
			{
				flags.click = HFlag.ClickKind.none;
				animationToggle.ManualOLoop();
			}
			else if (animationToggle.forceOLoop && (flags.click == HFlag.ClickKind.modeChange || flags.click == HFlag.ClickKind.speedup))
			{
				flags.click = HFlag.ClickKind.none;
			}
		}

		[HarmonyPrefix]
		[HarmonyPatch(typeof(HFlag), "SpeedUpClick")]
		public static bool SpeedUpClickPre()
		{
			if (animationToggle.forceOLoop)
				return false;
			else
				return true;
		}

		[HarmonyPrefix]
		[HarmonyPatch(typeof(HSprite), "SonyuProc")]
		public static bool HSpriteSonyuPre(HSprite __instance)
		{
			if (animationToggle.forceOLoop)
			{
				int index = ((flags.selectAnimationListInfo != null) ? (flags.selectAnimationListInfo.isFemaleInitiative ? 1 : 0) : (flags.nowAnimationInfo.isFemaleInitiative ? 1 : 0)) * 7;
				HSceneSpriteCategorySetActive(__instance.sonyu.categoryActionButton.lstButton, __instance.sonyu.tglAutoFinish.isOn, 4 + index);

				return false;
			}	
			else
			{
				return true;
			}				
		}

		[HarmonyPrefix]
		[HarmonyPatch(typeof(HSprite), "Sonyu3PProc")]
		public static bool HSpriteSonyu3PProcPre(HSprite __instance)
		{
			if (animationToggle.forceOLoop)
			{
				int index = ((flags.selectAnimationListInfo != null) ? (flags.selectAnimationListInfo.isFemaleInitiative ? 1 : 0) : (flags.nowAnimationInfo.isFemaleInitiative ? 1 : 0)) * 7;
				HSceneSpriteCategorySetActive(__instance.sonyu3P.categoryActionButton.lstButton, __instance.sonyu3P.tglAutoFinish.isOn, 4 + index);

				return false;
			}
			else
			{
				return true;
			}
		}

		[HarmonyPrefix]
		[HarmonyPatch(typeof(HSprite), "Sonyu3PDarkProc")]
		public static bool HSpriteSonyu3PDarkProcPre(HSprite __instance)
		{
			if (animationToggle.forceOLoop)
			{
				HSceneSpriteCategorySetActive(__instance.sonyu3PDark.categoryActionButton.lstButton, __instance.sonyu3PDark.tglAutoFinish.isOn, 18);

				return false;
			}
			else
			{
				return true;
			}
		}

		private static void HSceneSpriteCategorySetActive(List<UnityEngine.UI.Button> lstButton, bool autoFinish, int array)
		{
			bool active = flags.gaugeMale >= 70f && !autoFinish;
			if (lstButton.Count > array && (lstButton[array].isActiveAndEnabled != active))
				lstButton[array].gameObject.SetActive(active);

			array++;
			active = flags.gaugeMale >= 70f && autoFinish;
			if (lstButton.Count > array && (lstButton[array].isActiveAndEnabled != active))
				lstButton[array].gameObject.SetActive(active);
		}

		[HarmonyPostfix]
		[HarmonyPatch(typeof(HFlag), "MaleGaugeUp")]
		public static void HoushiOLoopGaugePre()
		{
			if (houshiRestoreGauge && flags.gaugeMale >= 70f)
			{
				maleGaugeOld = flags.gaugeMale;
				flags.gaugeMale = 65f;
			}
			else
				houshiRestoreGauge = false;
		}

		[HarmonyPrefix]
		[HarmonyPatch(typeof(HHoushi), "LoopProc")]
		public static void HoushiOLoopInit()
		{
			if (DisableAutoPrecum.Value)
				houshiRestoreGauge = true;
		}

		[HarmonyPostfix]
		[HarmonyPatch(typeof(HHoushi), "LoopProc")]
		public static void HoushiOLoopGaugePost()
		{
			if (houshiRestoreGauge)
			{
				flags.gaugeMale = maleGaugeOld;
				maleGaugeOld = -1;

				foreach (HSprite sprite in sprites)
					sprite.SetHoushiAutoFinish(_force: true);

				houshiRestoreGauge = false;
			}		
		}

		[HarmonyPrefix]
		[HarmonyPatch(typeof(H3PHoushi), "LoopProc")]
		public static void Houshi3POLoopInit()
		{
			if (DisableAutoPrecum.Value)
				houshiRestoreGauge = true;
		}

		[HarmonyPostfix]
		[HarmonyPatch(typeof(H3PHoushi), "LoopProc")]
		public static void Houshi3POLoopGaugePost()
		{
			if (houshiRestoreGauge)
			{
				flags.gaugeMale = maleGaugeOld;
				maleGaugeOld = -1;

				foreach (HSprite sprite in sprites)
					sprite.SetHoushi3PAutoFinish(_force: true);

				houshiRestoreGauge = false;
			}
		}

		[HarmonyPrefix]
		[HarmonyPatch(typeof(H3PDarkHoushi), "LoopProc")]
		public static void Houshi3PDarkOLoopInit()
		{
			if (DisableAutoPrecum.Value)
				houshiRestoreGauge = true;
		}

		[HarmonyPostfix]
		[HarmonyPatch(typeof(H3PDarkHoushi), "LoopProc")]
		public static void Houshi3PDarkOLoopGaugePost()
		{
			if (houshiRestoreGauge)
			{
				flags.gaugeMale = maleGaugeOld;
				maleGaugeOld = -1;

				foreach (HSprite sprite in sprites)
					sprite.SetHoushi3PDarkAutoFinish(_force: true);

				houshiRestoreGauge = false;
			}
		}
	}
}
