/** @file
  Copyright (c) 2025, Cory Bennett. All rights reserved.
  SPDX-License-Identifier: Apache-2.0
**/


namespace MTGOSDK.API.Play.Games;

public enum GamePhase
{
  Invalid,
  Untap,
  Upkeep,
  Draw,
  PreCombatMain,
  BeginCombat,
  DeclareAttackers,
  DeclareBlockers,
  CombatDamage,
  EndOfCombat,
  PostCombatMain,
  EndOfTurn,
  Cleanup
}
