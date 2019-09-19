﻿using Discord;
using Discord.Commands;
using MoonSharp.Interpreter;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace OurFoodChain.Gotchi {

    public class GotchiBattleState {

        public const ulong WILD_GOTCHI_USER_ID = 0;
        public const long WILD_GOTCHI_ID = -1;

        private static ConcurrentDictionary<ulong, GotchiBattleState> _battle_states = new ConcurrentDictionary<ulong, GotchiBattleState>();

        public class PlayerState {
            public BattleGotchi Gotchi { get; set; } = null;
            public GotchiMove SelectedMove { get; set; } = null;
        }

        public PlayerState player1;
        public PlayerState player2;

        public bool accepted = false;
        public int turnCount = 0;
        public string battleText = "";

        public async Task<IUser> GetPlayer1UserAsync(ICommandContext context) {

            return await context.Guild.GetUserAsync(player1.Gotchi.Gotchi.OwnerId);

        }
        public async Task<IUser> GetPlayer2UserAsync(ICommandContext context) {

            return await context.Guild.GetUserAsync(player2.Gotchi.Gotchi.OwnerId);

        }
        public async Task<string> GetPlayer1UsernameAsync(ICommandContext context) {

            if (player1.Gotchi.Gotchi.OwnerId == WILD_GOTCHI_USER_ID)
                return player1.Gotchi.Gotchi.Name;

            return (await GetPlayer1UserAsync(context)).Username;

        }
        public async Task<string> GetPlayer2UsernameAsync(ICommandContext context) {

            if (player2.Gotchi.Gotchi.OwnerId == WILD_GOTCHI_USER_ID)
                return player2.Gotchi.Gotchi.Name;

            return (await GetPlayer2UserAsync(context)).Username;

        }
        public async Task<IUser> GetOtherPlayerAsync(ICommandContext context, ulong userId) {

            ulong other_user_id = GetOtherPlayerUserId(userId);
            IUser other_user = await context.Guild.GetUserAsync(other_user_id);

            return other_user;

        }
        public ulong GetOtherPlayerUserId(ulong userId) {

            ulong other_user_id = player1.Gotchi.Gotchi.OwnerId == userId ? player2.Gotchi.Gotchi.OwnerId : player1.Gotchi.Gotchi.OwnerId;

            return other_user_id;

        }

        public Gotchi GetPlayersGotchi(ulong userId) {

            return userId == player1.Gotchi.Gotchi.OwnerId ? player1.Gotchi.Gotchi : player2.Gotchi.Gotchi;

        }
        public GotchiStats GetGotchiStats(Gotchi gotchi) {

            if (gotchi.Id == player1.Gotchi.Gotchi.Id)
                return player1.Gotchi.Stats;
            else if (gotchi.Id == player2.Gotchi.Gotchi.Id)
                return player2.Gotchi.Stats;

            return null;

        }
        public GotchiMoveSet GetGotchiMoveset(Gotchi gotchi) {

            if (gotchi.Id == player1.Gotchi.Gotchi.Id)
                return player1.Gotchi.Moves;
            else if (gotchi.Id == player2.Gotchi.Gotchi.Id)
                return player2.Gotchi.Moves;

            return null;

        }

        public async Task SelectMoveAsync(ICommandContext context, string moveIdentifier) {

            PlayerState player = context.User.Id == player1.Gotchi.Gotchi.OwnerId ? player1 : player2;
            PlayerState other_player = context.User.Id == player1.Gotchi.Gotchi.OwnerId ? player2 : player1;

            if (player.SelectedMove != null) {

                // If the player has already selected a move, don't allow them to change it.

                await BotUtils.ReplyAsync_Info(context, string.Format("You have already selected a move for this turn. Awaiting **{0}**'s move.",
                    (await GetOtherPlayerAsync(context, context.User.Id)).Username));

            }
            else {

                GotchiMove move = player.Gotchi.Moves.GetMove(moveIdentifier);

                if (move is null) {

                    // Warn the player if they select an invalid move.
                    await BotUtils.ReplyAsync_Error(context, "The move you have selected is invalid. Please select a valid move.");

                }
                else if (move.PP <= 0 && player.Gotchi.Moves.HasPPLeft) {

                    // The selected move cannot be used because it is out of PP.
                    await BotUtils.ReplyAsync_Error(context, "The selected move has no PP left. Please select a different move.");

                }
                else {

                    // Lock in the selected move.
                    player.SelectedMove = move;

                    // If the selected move does not have any PP, silently select the "struggle" move (used when no moves have any PP).
                    if (player.SelectedMove.PP <= 0)
                        player.SelectedMove = await GotchiMoveRegistry.GetMoveByNameAsync("desperation");

                    if (!IsBattlingCpu() && other_player.SelectedMove is null) {

                        // If the other user hasn't locked in a move yet, await their move.

                        await BotUtils.ReplyAsync_Info(context, string.Format("Move locked in! Awaiting **{0}**'s move.",
                            (await GetOtherPlayerAsync(context, context.User.Id)).Username));

                    }
                    else {

                        // If the player is battling a CPU, select a move for them now.

                        if (IsBattlingCpu())
                            await _pickCpuMoveAsync(player2);

                        // Update the battle state.
                        await ExecuteTurnAsync(context);

                    }

                }

            }

        }
        public async Task ExecuteTurnAsync(ICommandContext context) {

            battleText = string.Empty;

            if (turnCount == 0)
                turnCount = 1;

            // The faster gotchi goes first if their selected moves have the priority, otherwise the higher priority move goes first.
            // If both gotchis have the same speed, the first attacker is randomly selected.

            PlayerState first, second;

            if (player1.SelectedMove.Priority > player2.SelectedMove.Priority) {
                first = player1;
                second = player2;
            }
            else if (player2.SelectedMove.Priority > player1.SelectedMove.Priority) {
                first = player2;
                second = player1;
            }
            else if (player1.Gotchi.Stats.Spd > player2.Gotchi.Stats.Spd) {
                first = player1;
                second = player2;
            }
            else if (player2.Gotchi.Stats.Spd > player1.Gotchi.Stats.Spd) {
                first = player2;
                second = player1;
            }
            else {

                if (BotUtils.RandomInteger(0, 2) == 0) {
                    first = player1;
                    second = player2;
                }
                else {
                    first = player2;
                    second = player1;
                }

            }

            // Execute the first player's move.
            await _useMoveOnAsync(context, first, second);

            if (IsBattleOver())
                await _endBattle(context);
            else {

                // Execute the second player's move.
                await _useMoveOnAsync(context, second, first);

                if (IsBattleOver())
                    await _endBattle(context);

            }

            // Apply status problems for both users.

            if (!IsBattleOver()) {

                _applyStatusProblems(context, first);

                if (IsBattleOver())
                    await _endBattle(context);

                else {

                    _applyStatusProblems(context, second);

                    if (IsBattleOver())
                        await _endBattle(context);

                }

            }

            // Show the battle state.
            await ShowBattleStateAsync(context, this);

            // Reset the battle text and each user's selected moves.

            battleText = string.Empty;
            player1.SelectedMove.PP -= 1;
            player2.SelectedMove.PP -= 1;
            player1.SelectedMove = null;
            player2.SelectedMove = null;

            ++turnCount;

        }

        public bool IsBattleOver() {

            return player1.Gotchi.Stats.Hp <= 0.0 || player2.Gotchi.Stats.Hp <= 0.0;

        }
        public bool IsBattlingCpu() {

            if (!(player2 is null))
                return player2.Gotchi.Gotchi.OwnerId == WILD_GOTCHI_USER_ID;

            return false;

        }
        public bool IsCpuGotchi(Gotchi gotchi) {

            return gotchi.OwnerId == WILD_GOTCHI_USER_ID;

        }
        public static bool IsUserCurrentlyBattling(ulong userId) {

            if (_battle_states.ContainsKey(userId))
                return true;

            return false;


        }

        public static async Task RegisterBattleAsync(ICommandContext context, Gotchi gotchi1, Gotchi gotchi2) {

            // Initialize the battle state.

            GotchiBattleState state = new GotchiBattleState {

                // Initialize Player 1 (which must be a human player).

                player1 = new PlayerState {
                    Gotchi = new BattleGotchi {
                        Gotchi = gotchi1,
                        Moves = await GotchiMoveSet.GetMovesetAsync(gotchi1),
                        Stats = await new GotchiStatsCalculator(Global.GotchiTypeRegistry).GetStatsAsync(gotchi1)
                    }
                }

            };

            if (gotchi2 != null) {

                // Initialize Player 2 (which may be a human player, or a CPU).

                state.player2 = new PlayerState {
                    Gotchi = new BattleGotchi {
                        Gotchi = gotchi2,
                        Moves = await GotchiMoveSet.GetMovesetAsync(gotchi2),
                        Stats = await new GotchiStatsCalculator(Global.GotchiTypeRegistry).GetStatsAsync(gotchi2)
                    }
                };

            }
            else {

                // Otherwise, generate an opponent for the user.
                await state._generateOpponentAsync();

                // If the opponent is null (no species available as opponents), abort.

                if (state.player2.Gotchi.Gotchi is null) {

                    await BotUtils.ReplyAsync_Info(context, "There are no opponents available.");

                    return;

                }

                // Since the user is battling a CPU, accept the battle immediately.
                state.accepted = true;

            }

            // Register the battle state in the battle state collection.

            _battle_states[gotchi1.OwnerId] = state;

            if (state.player2.Gotchi.Gotchi.OwnerId != WILD_GOTCHI_USER_ID)
                _battle_states[state.player2.Gotchi.Gotchi.OwnerId] = state;

            // Set the initial message displayed when the battle starts.

            StringBuilder sb = new StringBuilder();
            sb.AppendLine("The battle has begun!");
            sb.AppendLine();
            sb.AppendLine(string.Format("Pick a move with `{0}gotchi move`.\nSee your gotchi's moveset with `{0}gotchi moveset`.",
                OurFoodChainBot.Instance.Config.Prefix));

            state.battleText = sb.ToString();

            // If the user is battling a CPU, show the battle state immediately.
            // Otherwise, it will be shown when the other user accepts the battle challenge.

            if (state.IsBattlingCpu())
                await ShowBattleStateAsync(context, state);

        }
        public static void DeregisterBattle(ulong userId) {

            if (_battle_states.TryRemove(userId, out GotchiBattleState state))
                _battle_states.TryRemove(state.GetOtherPlayerUserId(userId), out state);

        }
        public static GotchiBattleState GetBattleStateByUserId(ulong userId) {

            if (!_battle_states.ContainsKey(userId))
                return null;

            return _battle_states[userId];

        }

        public static async Task ShowBattleStateAsync(ICommandContext context, GotchiBattleState state) {

            // Get an image of the battle.

            string gif_url = "";

            GotchiGifCreatorParams p1 = new GotchiGifCreatorParams {
                gotchi = state.player1.Gotchi.Gotchi,
                x = 50,
                y = 150,
                state = state.player1.Gotchi.Stats.Hp > 0 ? (state.player2.Gotchi.Stats.Hp <= 0 ? GotchiState.Happy : GotchiState.Energetic) : GotchiState.Dead,
                auto = false
            };

            GotchiGifCreatorParams p2 = new GotchiGifCreatorParams {
                gotchi = state.player2.Gotchi.Gotchi,
                x = 250,
                y = 150,
                state = state.player2.Gotchi.Stats.Hp > 0 ? (state.player1.Gotchi.Stats.Hp <= 0 ? GotchiState.Happy : GotchiState.Energetic) : GotchiState.Dead,
                auto = false
            };

            gif_url = await GotchiUtils.GenerateAndUploadGotchiGifAndReplyAsync(context, new GotchiGifCreatorParams[] { p1, p2 }, new GotchiGifCreatorExtraParams {
                backgroundFileName = await GotchiUtils.GetGotchiBackgroundFileNameAsync(state.player2.Gotchi.Gotchi, "home_battle.png"),
                overlay = (Graphics gfx) => {

                    // Draw health bars.

                    _drawHealthBar(gfx, p1.x, 180, (double)state.player1.Gotchi.Stats.Hp / state.player1.Gotchi.Stats.MaxHp);
                    _drawHealthBar(gfx, p2.x, 180, (double)state.player2.Gotchi.Stats.Hp / state.player2.Gotchi.Stats.MaxHp);

                }
            });

            EmbedBuilder embed = new EmbedBuilder();

            embed.WithTitle(string.Format("**{0}** vs. **{1}** (Turn {2})",
                StringUtils.ToTitleCase(state.player1.Gotchi.Gotchi.Name),
                StringUtils.ToTitleCase(state.player2.Gotchi.Gotchi.Name),
                state.turnCount));
            embed.WithImageUrl(gif_url);
            embed.WithDescription(state.battleText);
            if (state.IsBattleOver())
                embed.WithFooter("The battle has ended!");
            else if (state.turnCount == 0) {
                embed.WithFooter("The battle has begun. Pick your move!");
            }
            else
                embed.WithFooter(string.Format("Beginning Turn {0}. Pick your move!", state.turnCount + 1));

            await context.Channel.SendMessageAsync("", false, embed.Build());

        }

        private async Task _useMoveOnAsync(ICommandContext context, PlayerState user, PlayerState target) {

            // Check role match-up to see if the move is super-effective.
            // #todo Role match-ups should be defined in an external file.

            Role[] target_roles = await SpeciesUtils.GetRolesAsync(target.Gotchi.Gotchi.SpeciesId);
            double weakness_multiplier = !user.SelectedMove.IgnoreMatchup ? GotchiType.CheckMatchup(user.SelectedMove.Types, target.Gotchi.Types) : 1.0;
            Species target_species = await BotUtils.GetSpeciesFromDb(target.Gotchi.Gotchi.SpeciesId);

            // Execute the selected move.

            StringBuilder battle_text = new StringBuilder();
            battle_text.AppendLine(battleText);

            if (!string.IsNullOrEmpty(user.SelectedMove.LuaScriptFilePath)) {

                // Create, initialize, and execute the script associated with this move.

                Script script = new Script();
                LuaUtils.InitializeLuaContext(script);

                script.DoFile(user.SelectedMove.LuaScriptFilePath);

                // Initialize the callback args.

                LuaGotchiMoveCallbackArgs args = new LuaGotchiMoveCallbackArgs {
                    user = new LuaGotchiParameters(user.Gotchi.Stats, null, null) { status = user.Gotchi.Status },
                    target = new LuaGotchiParameters(target.Gotchi.Stats, target_roles, target_species) { status = target.Gotchi.Status }
                };

                // Initialize the move state (required for only certain moves).

                if (!(script.Globals["init"] is null))
                    await script.CallAsync(script.Globals["init"], args);

                // It's possible for a move to be used more than once in a turn (e.g., multi-hit moves).
                // Each time will trigger the callback to be called and display a new message.

                for (int i = 0; i < Math.Max(1, args.times); ++i) {

                    // Check if this was a critical hit, or if the move missed.

                    bool is_hit = target.Gotchi.Status != "blinding" && (user.SelectedMove.IgnoreAccuracy || (BotUtils.RandomInteger(0, 20 + 1) < 20 * user.SelectedMove.Accuracy * Math.Max(0.1, user.Gotchi.Stats.Acc - target.Gotchi.Stats.Eva)));
                    bool is_critical =
                        BotUtils.RandomInteger(0, (int)(10 / user.SelectedMove.CriticalRate)) == 0 ||
                        (await SpeciesUtils.GetPreyAsync(user.Gotchi.Gotchi.SpeciesId)).Any(x => x.id == target.Gotchi.Gotchi.Id);

                    if (user.SelectedMove.IgnoreCritical)
                        is_critical = false;

                    if (is_hit) {

                        // Set additional parameters in the callback.

                        args.matchupMultiplier = weakness_multiplier;

                        if (is_critical)
                            args.bonusMultiplier *= 1.5;

                        // Clone each user's stats before triggering the callback, so we can compare them before and after.

                        GotchiStats user_before = user.Gotchi.Stats.Clone();
                        GotchiStats target_before = target.Gotchi.Stats.Clone();

                        // Trigger the callback.

                        try {

                            //if (user.SelectedMove.info.type == GotchiMoveType.Recovery && user.Gotchi.Status == "heal block") {
                            //
                            //    args.text = "but it failed";
                            //
                            //}
                            //else {

                            if (script.Globals["callback"] != null)
                                await script.CallAsync(script.Globals["callback"], args);
                            else
                                args.DoDamage(user.SelectedMove.Power);

                            // Copy the statuses over for both participants (to reflect changes made in the callback).

                            user.Gotchi.Status = args.user.status;
                            target.Gotchi.Status = args.target.status;

                            //}

                        }
                        catch (Exception) {
                            args.text = "but something went wrong";
                        }

                        // If the target is "withdrawn", allow them to survive the hit with at least 1 HP.
                        if (target.Gotchi.Status == "withdrawn") {

                            target.Gotchi.Stats.Hp = Math.Max(1, target.Gotchi.Stats.Hp);
                            target.Gotchi.Status = "";

                        }

                        // If the target is "blinding", remove the status.
                        if (target.Gotchi.Status == "blinding")
                            target.Gotchi.Status = "";

                        // Show the battle text.
                        // If the move doesn't specify a text, choose one automatically (where possible).

                        string text = args.text;

                        if (string.IsNullOrEmpty(text)) {

                            if (target.Gotchi.Stats.Hp < target_before.Hp) {
                                text = "dealing {target:damage} damage";
                                //user.SelectedMove.info.Type = GotchiMoveType.Offensive;
                            }

                            else if (target.Gotchi.Stats.Atk < target_before.Atk) {
                                text = "lowering its opponent's ATK by {target:atk%}";
                                //user.SelectedMove.info.Type = GotchiMoveType.Buff;
                            }
                            else if (target.Gotchi.Stats.Def < target_before.Def) {
                                text = "lowering its opponent's DEF by {target:def%}";
                                //user.SelectedMove.info.Type = GotchiMoveType.Buff;
                            }
                            else if (target.Gotchi.Stats.Spd < target_before.Spd) {
                                text = "lowering its opponent's SPD by {target:spd%}";
                                //user.SelectedMove.info.Type = GotchiMoveType.Buff;
                            }
                            else if (target.Gotchi.Stats.Acc < target_before.Acc) {
                                text = "lowering its opponent's accuracy by {target:acc%}";
                                //user.SelectedMove.info.Type = GotchiMoveType.Buff;
                            }
                            else if (target.Gotchi.Stats.Eva < target_before.Eva) {
                                text = "lowering its opponent's evasion by {target:eva%}";
                                //user.SelectedMove.info.Type = GotchiMoveType.Buff;
                            }

                            else if (user.Gotchi.Stats.Hp > user_before.Hp) {
                                text = "recovering {user:recovered} HP";
                                //user.SelectedMove.info.Type = GotchiMoveType.Recovery;
                            }
                            else if (user.Gotchi.Stats.Atk > user_before.Atk) {
                                text = "boosting its ATK by {user:atk%}";
                                //user.SelectedMove.info.Type = GotchiMoveType.Buff;
                            }
                            else if (user.Gotchi.Stats.Def > user_before.Def) {
                                text = "boosting its DEF by {user:def%}";
                                //user.SelectedMove.info.Type = GotchiMoveType.Buff;
                            }
                            else if (user.Gotchi.Stats.Spd > user_before.Spd) {
                                text = "boosting its SPD by {user:spd%}";
                                //user.SelectedMove.info.Type = GotchiMoveType.Buff;
                            }
                            else if (user.Gotchi.Stats.Acc > user_before.Acc) {
                                text = "boosting its accuracy by {user:acc%}";
                                //user.SelectedMove.info.Type = GotchiMoveType.Buff;
                            }
                            else if (user.Gotchi.Stats.Eva > user_before.Eva) {
                                text = "boosting its evasion by {user:eva%}";
                                //user.SelectedMove.info.Type = GotchiMoveType.Buff;
                            }

                            else {
                                text = "but nothing happened?";
                                is_critical = false;
                                weakness_multiplier = 1.0;
                            }

                        }

                        // Various replacements are allowed, which the user can specify in the move's battle text.

                        text = Regex.Replace(text, @"\{([^\}]+)\}", m => {

                            switch (m.Groups[1].Value.ToLower()) {

                                case "damage":
                                case "target:damage":
                                    return string.Format("{0:0.#}", target_before.Hp - target.Gotchi.Stats.Hp);

                                case "target:atk%":
                                    return string.Format("{0:0.#}%", (Math.Abs(target_before.Atk - target.Gotchi.Stats.Atk) / target_before.Atk) * 100.0);
                                case "target:def%":
                                    return string.Format("{0:0.#}%", (Math.Abs(target_before.Def - target.Gotchi.Stats.Def) / target_before.Def) * 100.0);
                                case "target:spd%":
                                    return string.Format("{0:0.#}%", (Math.Abs(target_before.Spd - target.Gotchi.Stats.Spd) / target_before.Spd) * 100.0);
                                case "target:acc%":
                                    return string.Format("{0:0.#}%", (Math.Abs(target_before.Acc - target.Gotchi.Stats.Acc) / target_before.Acc) * 100.0);
                                case "target:eva%":
                                    return string.Format("{0:0.#}%", (Math.Abs(target_before.Eva - target.Gotchi.Stats.Eva) / target_before.Eva) * 100.0);

                                case "user:atk%":
                                    return string.Format("{0:0.#}%", (Math.Abs(user_before.Atk - user.Gotchi.Stats.Atk) / user_before.Atk) * 100.0);
                                case "user:def%":
                                    return string.Format("{0:0.#}%", (Math.Abs(user_before.Def - user.Gotchi.Stats.Def) / user_before.Def) * 100.0);
                                case "user:spd%":
                                    return string.Format("{0:0.#}%", (Math.Abs(user_before.Spd - user.Gotchi.Stats.Spd) / user_before.Spd) * 100.0);
                                case "user:acc%":
                                    return string.Format("{0:0.#}%", (Math.Abs(user_before.Acc - user.Gotchi.Stats.Acc) / user_before.Acc) * 100.0);
                                case "user:eva%":
                                    return string.Format("{0:0.#}%", (user_before.Eva == 0.0 ? user.Gotchi.Stats.Eva : (Math.Abs(user_before.Eva - user.Gotchi.Stats.Eva) / user_before.Eva)) * 100.0);

                                case "user:recovered":
                                    return string.Format("{0:0.#}", user.Gotchi.Stats.Hp - user_before.Hp);

                                default:
                                    return "???";

                            }

                        });

                        battle_text.Append(string.Format("{0} **{1}** used **{2}**, {3}!",
                            "💥", //user.SelectedMove.info.Icon(),
                            StringUtils.ToTitleCase(user.Gotchi.Gotchi.Name),
                            StringUtils.ToTitleCase(user.SelectedMove.Name),
                            text));

                        if (!user.SelectedMove.IgnoreMatchup && weakness_multiplier > 1.0)
                            battle_text.Append(" It's super effective!");

                        if (!user.SelectedMove.IgnoreCritical && is_critical && target.Gotchi.Stats.Hp < target_before.Hp)
                            battle_text.Append(" Critical hit!");

                        battle_text.AppendLine();

                    }
                    else {

                        // If the move missed, so display a failure message.
                        battle_text.AppendLine(string.Format("{0} **{1}** used **{2}**, but it missed!",
                             "💥", //user.SelectedMove.info.Icon(),
                            StringUtils.ToTitleCase(user.Gotchi.Gotchi.Name),
                            StringUtils.ToTitleCase(user.SelectedMove.Name)));

                    }

                }

                if (args.times > 1)
                    battle_text.Append(string.Format(" Hit {0} times!", args.times));

            }
            else {

                // If there is no Lua script associated with the given move, display a failure message.
                battle_text.Append(string.Format("{0} **{1}** used **{2}**, but it forgot how!",
                     "💥", //user.SelectedMove.info.Icon(),
                    StringUtils.ToTitleCase(user.Gotchi.Gotchi.Name),
                    StringUtils.ToTitleCase(user.SelectedMove.Name)));

            }

            battleText = battle_text.ToString();

        }
        private void _applyStatusProblems(ICommandContext context, PlayerState user) {

            StringBuilder sb = new StringBuilder();
            sb.AppendFormat(battleText);

            if (user.Gotchi.Status == "poisoned") {

                // If the user is poisoned, apply poison damage (1/16th of max HP).

                user.Gotchi.Stats.Hp = Math.Max(0, user.Gotchi.Stats.Hp - (user.Gotchi.Stats.MaxHp / 16));

                sb.Append(string.Format("\n⚡ **{0}** is damaged by poison!", StringUtils.ToTitleCase(user.Gotchi.Gotchi.Name)));

            }
            else if (user.Gotchi.Status == "rooted") {

                // If the user is rooted, heal some HP (1/10th of max HP).

                user.Gotchi.Stats.Hp = Math.Min(user.Gotchi.Stats.MaxHp, user.Gotchi.Stats.Hp + (user.Gotchi.Stats.MaxHp / 10));

                sb.Append(string.Format("\n❤ **{0}** absorbed nutrients from its roots!", StringUtils.ToTitleCase(user.Gotchi.Gotchi.Name)));

            }
            else if (user.Gotchi.Status == "vine-wrapped") {

                // If the user is wrapped in vines, apply poison damage (1/16th of max HP).

                user.Gotchi.Stats.Hp = Math.Max(0, user.Gotchi.Stats.Hp - (user.Gotchi.Stats.MaxHp / 16));

                sb.Append(string.Format("\n⚡ **{0}** is hurt by vines!", StringUtils.ToTitleCase(user.Gotchi.Gotchi.Name)));

            }
            else if (user.Gotchi.Status == "thorn-surrounded") {

                // #todo Damage should only be incurred when the user uses a damaging move.

                // If the user is surrounded by thorns, apply thorn damage (1/10th of max HP).
                // Only damages the user if they are attacking the opponent.

                user.Gotchi.Stats.Hp = Math.Max(0, user.Gotchi.Stats.Hp - (user.Gotchi.Stats.MaxHp / 10));

                sb.Append(string.Format("\n⚡ **{0}** is hurt by thorns!", StringUtils.ToTitleCase(user.Gotchi.Gotchi.Name)));

            }
            else if (user.Gotchi.Status == "withdrawn") {

                // This status only lasts a single turn.

                user.Gotchi.Status = "";
                sb.Append(string.Format("\n⚡ **{0}** came back out of its shell.", StringUtils.ToTitleCase(user.Gotchi.Gotchi.Name)));

            }

            battleText = sb.ToString();

        }
        private double _getWeaknessMultiplier(string moveRole, Role[] target_roles) {

            double mult = 1.0;

            /*
             parasite -> predators, base-consumers
             decomposer, scavenger, detritvore -> producers
             predator -> predator, base-conumers; -/> producers
             base-consumer -> producer
             */

            foreach (Role role in target_roles) {

                switch (moveRole.ToLower()) {

                    case "parasite":
                        if (role.name.ToLower() == "predator" || role.name.ToLower() == "base-consumer")
                            mult *= 1.2;
                        break;

                    case "decomposer":
                    case "scavenger":
                    case "detritvore":
                        if (role.name.ToLower() == "producer")
                            mult *= 1.2;
                        break;

                    case "predator":
                        if (role.name.ToLower() == "predator" || role.name.ToLower() == "base-consumer")
                            mult *= 1.2;
                        else if (role.name.ToLower() == "producer")
                            mult *= 0.8;
                        break;

                    case "base-consumer":
                        if (role.name.ToLower() == "producer")
                            mult *= 1.2;
                        break;

                }

            }

            return mult;

        }
        private double _getExpEarned(Gotchi gotchi, Gotchi opponent, bool won) {

            double exp = 0.0;

            exp = (opponent.Id == player1.Gotchi.Gotchi.Id ? player1.Gotchi.Stats.Level : player2.Gotchi.Stats.Level) * 10.0;

            if (!won)
                exp *= .5;

            return exp;

        }
        private static void _drawHealthBar(Graphics gfx, float x, float y, double amount) {

            float hp_bar_width = 50;

            using (Brush brush = new SolidBrush(System.Drawing.Color.White))
                gfx.FillRectangle(brush, new RectangleF(x - hp_bar_width / 2, y, hp_bar_width, 10));

            using (Brush brush = new SolidBrush(amount < 0.5 ? (amount < 0.2 ? System.Drawing.Color.Red : System.Drawing.Color.Orange) : System.Drawing.Color.Green))
                gfx.FillRectangle(brush, new RectangleF(x - hp_bar_width / 2, y, hp_bar_width * (float)amount, 10));

            using (Brush brush = new SolidBrush(System.Drawing.Color.Black))
            using (Pen pen = new Pen(brush))
                gfx.DrawRectangle(pen, new Rectangle((int)(x - hp_bar_width / 2), (int)y, (int)hp_bar_width, 10));

        }
        private async Task _generateOpponentAsync() {

            // Pick a random species from the same zone as the player's gotchi.

            List<Species> species_list = new List<Species>();

            foreach (SpeciesZone zone in await SpeciesUtils.GetZonesAsync(await SpeciesUtils.GetSpeciesAsync(player1.Gotchi.Gotchi.SpeciesId)))
                species_list.AddRange((await ZoneUtils.GetSpeciesAsync(zone.Zone)).Where(x => !x.isExtinct));

            player2 = new PlayerState();

            if (species_list.Count() > 0) {

                player2.Gotchi = await GotchiUtils.GenerateGotchiAsync(new GotchiGenerationParameters {
                    Base = player1.Gotchi.Gotchi,
                    Species = species_list[BotUtils.RandomInteger(species_list.Count())],
                    MinLevel = player1.Gotchi.Stats.Level - 3,
                    MaxLevel = player1.Gotchi.Stats.Level + 3,
                    GenerateMoveset = true,
                    GenerateStats = true
                });

            }

            // Set the opponent.

            if (player2.Gotchi != null) {

                player2.Gotchi.Gotchi.OwnerId = WILD_GOTCHI_USER_ID;
                player2.Gotchi.Gotchi.Id = WILD_GOTCHI_ID;

            }

        }
        private async Task _pickCpuMoveAsync(PlayerState player) {

            GotchiMove move = await player.Gotchi.Moves.GetRandomMoveAsync();

            player.SelectedMove = move;

        }
        private async Task _endBattle(ICommandContext context) {

            PlayerState winner = player1.Gotchi.Stats.Hp <= 0.0 ? player2 : player1;
            PlayerState loser = player1.Gotchi.Stats.Hp <= 0.0 ? player1 : player2;

            // Calculate the amount of EXP awarded to the winner.
            // The loser will get 50% of the winner's EXP.

            double exp = _getExpEarned(winner.Gotchi.Gotchi, loser.Gotchi.Gotchi, won: true);

            double exp1 = player2.Gotchi.Stats.Hp <= 0.0 ? exp : exp * .5;
            double exp2 = player1.Gotchi.Stats.Hp <= 0.0 ? exp : exp * .5;

            long levels1 = player1.Gotchi.Stats.AddExperience((int)exp1);
            long levels2 = player2.Gotchi.Stats.AddExperience((int)exp2);

            StringBuilder sb = new StringBuilder();
            sb.AppendLine(battleText);

            // Show the winner's accomplishments, then the loser's.

            if (!IsCpuGotchi(winner.Gotchi.Gotchi)) {

                double winner_exp = winner.Gotchi.Gotchi.Id == player1.Gotchi.Gotchi.Id ? exp1 : exp2;
                long winner_levels = winner.Gotchi.Gotchi.Id == player1.Gotchi.Gotchi.Id ? levels1 : levels2;
                long winner_g = (long)(loser.Gotchi.Stats.Level * (BotUtils.RandomInteger(100, 150) / 100.0));

                sb.AppendLine(string.Format("🏆 **{0}** won the battle! Earned **{1} EXP** and **{2}G**.",
                    StringUtils.ToTitleCase(winner.Gotchi.Gotchi.Name),
                    winner_exp,
                    winner_g));

                if (winner_levels > 0)
                    sb.AppendLine(string.Format("🆙 **{0}** leveled up to level **{1}**!", StringUtils.ToTitleCase(winner.Gotchi.Gotchi.Name), winner.Gotchi.Stats.Level));

                if (((winner.Gotchi.Stats.Level - winner_levels) / 10) < (winner.Gotchi.Stats.Level / 10))
                    if (await GotchiUtils.EvolveAndUpdateGotchiAsync(winner.Gotchi.Gotchi)) {

                        Species sp = await BotUtils.GetSpeciesFromDb(winner.Gotchi.Gotchi.SpeciesId);

                        sb.AppendLine(string.Format("🚩 Congratulations, **{0}** evolved into **{1}**!", StringUtils.ToTitleCase(winner.Gotchi.Gotchi.Name), sp.GetShortName()));

                    }

                // Update the winner's G.

                GotchiUserInfo user_data = await GotchiUtils.GetUserInfoAsync(winner.Gotchi.Gotchi.OwnerId);

                user_data.G += winner_g;

                await GotchiUtils.UpdateUserInfoAsync(user_data);

                sb.AppendLine();

            }

            if (!IsCpuGotchi(loser.Gotchi.Gotchi)) {

                double loser_exp = loser.Gotchi.Gotchi.Id == player1.Gotchi.Gotchi.Id ? exp1 : exp2;
                long loser_levels = loser.Gotchi.Gotchi.Id == player1.Gotchi.Gotchi.Id ? levels1 : levels2;

                sb.AppendLine(string.Format("💀 **{0}** lost the battle... Earned **{1} EXP**.", StringUtils.ToTitleCase(loser.Gotchi.Gotchi.Name), loser_exp));

                if (loser_levels > 0)
                    sb.AppendLine(string.Format("🆙 **{0}** leveled up to level **{1}**!", StringUtils.ToTitleCase(loser.Gotchi.Gotchi.Name), loser.Gotchi.Stats.Level));

                if (((loser.Gotchi.Stats.Level - loser_levels) / 10) < (loser.Gotchi.Stats.Level / 10))
                    if (await GotchiUtils.EvolveAndUpdateGotchiAsync(loser.Gotchi.Gotchi)) {

                        Species sp = await BotUtils.GetSpeciesFromDb(loser.Gotchi.Gotchi.SpeciesId);

                        sb.AppendLine(string.Format("🚩 Congratulations, **{0}** evolved into **{1}**!", StringUtils.ToTitleCase(loser.Gotchi.Gotchi.Name), sp.GetShortName()));

                    }

            }

            // Update level/exp in the database.

            using (SQLiteCommand cmd = new SQLiteCommand("UPDATE Gotchi SET level=$level, exp=$exp WHERE id=$id;")) {

                cmd.Parameters.AddWithValue("$id", player1.Gotchi.Gotchi.Id);
                cmd.Parameters.AddWithValue("$level", player1.Gotchi.Stats.Level);
                cmd.Parameters.AddWithValue("$exp", player1.Gotchi.Stats.Experience);

                await Database.ExecuteNonQuery(cmd);

            }

            if (!IsCpuGotchi(player2.Gotchi.Gotchi)) {

                using (SQLiteCommand cmd = new SQLiteCommand("UPDATE Gotchi SET level=$level, exp=$exp WHERE id=$id;")) {

                    cmd.Parameters.AddWithValue("$id", player2.Gotchi.Gotchi.Id);
                    cmd.Parameters.AddWithValue("$level", player2.Gotchi.Stats.Level);
                    cmd.Parameters.AddWithValue("$exp", player2.Gotchi.Stats.Experience);

                    await Database.ExecuteNonQuery(cmd);

                }

            }

            // Deregister the battle state.

            DeregisterBattle(context.User.Id);

            battleText = sb.ToString();

        }

    }

}