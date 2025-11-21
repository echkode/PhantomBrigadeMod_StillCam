// Copyright (c) 2025 EchKode
// SPDX-License-Identifier: BSD-3-Clause

using System.Collections.Generic;
using System.Reflection.Emit;

using HarmonyLib;

using PhantomBrigade;
using PhantomBrigade.Combat.Systems;
using PhantomBrigade.Data;

namespace EchKode.PBMods.StillCam
{
	[HarmonyPatch]
	static class Patch
	{
		[HarmonyPatch(typeof(GameCameraSystem), nameof(GameCameraSystem.MoveToUnit))]
		[HarmonyTranspiler]
		static IEnumerable<CodeInstruction> Gcs_MoveToUnitTranspiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
		{
			// Check auto camera setting before shifting camera to unit.

			var cm = new CodeMatcher(instructions, generator);
			var clearTargetMethodInfo = AccessTools.DeclaredMethod(typeof(GameCameraSystem), nameof(GameCameraSystem.ClearTarget));
			var autofocusFieldInfo = AccessTools.DeclaredField(typeof(SettingUtility), nameof(SettingUtility.combatCameraAutoFocusAllowed));
			var clearTargetMatch = new CodeMatch(OpCodes.Call, clearTargetMethodInfo);
			var loadAutoFocus = new CodeInstruction(OpCodes.Ldsfld, autofocusFieldInfo);

			cm.Start();
			cm.MatchStartForward(clearTargetMatch)
				.Advance(-1);
			var retJump = cm.Instruction.Clone();
			cm.Advance(1)
				.InsertAndAdvance(loadAutoFocus)
				.InsertAndAdvance(retJump);

			return cm.InstructionEnumeration();
		}

		[HarmonyPatch(typeof(CombatUIUtility), nameof(CombatUIUtility.SelectNextUnit))]
		[HarmonyTranspiler]
		static IEnumerable<CodeInstruction> Cuiu_SelectNextUnitTranspiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
		{
			// Check auto camera setting before shifting camera to selected unit.
			// This happens in three places near the end of the method. Two of the call sites are
			// swapped out with calls to MoveToUnit which has been patched above.
			// The code path for tab.

			var cm = new CodeMatcher(instructions, generator);
			var clearTargetMethodInfo = AccessTools.DeclaredMethod(typeof(GameCameraSystem), nameof(GameCameraSystem.ClearTarget));
			var moveCameraMethodInfo = AccessTools.DeclaredMethod(typeof(GameCameraSystem), nameof(GameCameraSystem.MoveToLocation));
			var autofocusFieldInfo = AccessTools.DeclaredField(typeof(SettingUtility), nameof(SettingUtility.combatCameraAutoFocusAllowed));
			var clearTargetMatch = new CodeMatch(OpCodes.Call, clearTargetMethodInfo);
			var moveCameraMatch = new CodeMatch(OpCodes.Call, moveCameraMethodInfo);
			var branchMatch = new CodeMatch(OpCodes.Bne_Un_S);
			var nop = new CodeInstruction(OpCodes.Nop);
			var moveToUnit = CodeInstruction.Call(typeof(GameCameraSystem), nameof(GameCameraSystem.MoveToUnit));
            var clearTarget = CodeInstruction.Call(typeof(GameCameraSystem), nameof(GameCameraSystem.ClearTarget));
			var loadAutoFocus = new CodeInstruction(OpCodes.Ldsfld, autofocusFieldInfo);

			cm.End();
			cm.MatchStartBackwards(clearTargetMatch);
			var labels = new List<Label>(cm.Labels);
			cm.SetInstruction(nop)
				.AddLabels(labels)
				.MatchStartBackwards(moveCameraMatch)
				.RemoveInstructionsWithOffsets(-2, -3)
				.End()
				.MatchStartBackwards(moveCameraMatch)
				.SetInstruction(moveToUnit)
				.MatchEndBackwards(moveCameraMatch)
				.Advance(1)
				.CreateLabel(out var skipLabel);
			var skip = new CodeInstruction(OpCodes.Brfalse_S, skipLabel);
			cm.InsertAndAdvance(clearTarget)
				.MatchEndBackwards(branchMatch)
				.Advance(1)
				.InsertAndAdvance(loadAutoFocus)
				.InsertAndAdvance(skip)
				.Advance(-1)
				.MatchStartBackwards(moveCameraMatch)
				.RemoveInstructionsWithOffsets(-2, -3)
				.MatchStartBackwards(moveCameraMatch)
				.SetInstruction(moveToUnit);

			return cm.InstructionEnumeration();
		}

		[HarmonyPatch(typeof(InputCombatShared), nameof(InputCombatShared.Execute))]
		[HarmonyTranspiler]
		static IEnumerable<CodeInstruction> Ics_ExecuteTranspiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
		{
			// Check auto camera setting before setting the unit to follow.

			var cm = new CodeMatcher(instructions, generator);
			var replaceFollowUnitMethodInfo = AccessTools.DeclaredMethod(typeof(InputContext), nameof(InputContext.ReplaceUnitToFollow));
			var autofocusFieldInfo = AccessTools.DeclaredField(typeof(SettingUtility), nameof(SettingUtility.combatCameraAutoFocusAllowed));
			var replaceFollowUnitMatch = new CodeMatch(OpCodes.Callvirt, replaceFollowUnitMethodInfo);
			var branchMatch = new CodeMatch(OpCodes.Beq_S);
			var loadAutoFocus = new CodeInstruction(OpCodes.Ldsfld, autofocusFieldInfo);

			cm.Start();
			cm.MatchStartForward(replaceFollowUnitMatch)
				.MatchEndBackwards(branchMatch);
			var skip = new CodeInstruction(OpCodes.Brfalse_S, cm.Operand);
			cm.Advance(1);
			var labels = new List<Label>(cm.Labels);
			cm.Labels.Clear();
			cm.Insert(loadAutoFocus)
				.AddLabels(labels)
				.Advance(1)
				.InsertAndAdvance(skip);

			return cm.InstructionEnumeration();
		}
	}
}
