/** @file
  Copyright (c) 2023, Cory Bennett. All rights reserved.
  SPDX-License-Identifier: Apache-2.0
**/

using System.Collections;

using MTGOSDK.API.Users;
using MTGOSDK.Core.Reflection;

using WotC.MtGO.Client.Model.Play.Tournaments;


namespace MTGOSDK.API.Play.Tournaments;

public sealed class MatchStandingRecord(dynamic matchStandingRecord)
    : DLRWrapper<IMatchStandingRecord>
{
  /// <summary>
  /// Stores an internal reference to the IMatchStandingRecord object.
  /// </summary>
  internal override dynamic obj => matchStandingRecord;

  //
  // IMatchStandingRecord wrapper properties
  //

  /// <summary>
  /// The ID of the match.
  /// </summary>
  [Default(-1)]
  public int Id => @base.HasBye ? -1 : @base.Id;

  /// <summary>
  /// The round number of the match.
  /// </summary>
  public int Round => @base.Round;

  /// <summary>
  /// The state of the match (i.e. "Joined", "GameStarted", "Sideboarding", etc.)
  /// </summary>
  public MatchState State => Cast<MatchState>(Unbind(@base).Status);

  /// <summary>
  /// Whether the player has been assigned a bye.
  /// </summary>
  public bool HasBye => Try(() => @base.HasBye, this.Players.Count == 1);

  /// <summary>
  /// The user objects of both players.
  /// </summary>
  [NonSerializable]
  public IList<User> Players =>
    Map<IList, User>(Unbind(@base).Users, Lambda(p => new User(p.Name)));

  /// <summary>
  /// The IDs of the winning player(s).
  /// </summary>
  public IList<int> WinningPlayerIds => Map<IList, int>(@base.WinningPlayerIds);

  /// <summary>
  /// The IDs of the losing player(s).
  /// </summary>
  public IList<int> LosingPlayerIds => Map<IList, int>(@base.LosingPlayerIds);

  /// <summary>
  /// The results of each game in the match.
  /// </summary>
  public IList<GameStandingRecord> GameStandingRecords =>
    Map<IList, GameStandingRecord?>(@base.GameStandingRecords);
}
