﻿using Discord;
using Discord.Commands;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using System.Text;
using System.Threading.Tasks;

namespace OurFoodChain.gotchi {

    [Group("gotchi")]
    public class Commands :
     ModuleBase {

        static Random _rng = new Random();

        [Command]
        public async Task Gotchi() {

            // Get this user's gotchi.

            Gotchi gotchi = await GotchiUtils.GetGotchiAsync(Context.User);

            if (!await GotchiUtils.Reply_ValidateGotchiAsync(Context, gotchi))
                return;

            // Check if the gotchi is able to evolve. If so, evolve it and update the species ID.

            bool evolved = false;

            if (!gotchi.IsDead() && gotchi.IsReadyToEvolve())
                evolved = await GotchiUtils.EvolveGotchiAsync(gotchi);

            // If the gotchi tried to evolve but failed, update its evolution timestamp so that we get a valid state (i.e., not "ready to evolve").
            // (Note that it will have already been updated in the database by this point.)
            if (gotchi.IsReadyToEvolve() && !evolved)
                gotchi.evolved_ts = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            // Create the gotchi GIF.

            string gif_url = await GotchiUtils.Reply_GenerateAndUploadGotchiGifAsync(Context, gotchi);

            if (string.IsNullOrEmpty(gif_url))
                return;

            // Get the gotchi's species.

            Species sp = await BotUtils.GetSpeciesFromDb(gotchi.species_id);

            // Pick status text.

            string status = "{0} is feeling happy!";

            switch (gotchi.State()) {

                case GotchiState.Dead:
                    status = "Oh no... {0} has died...";
                    break;

                case GotchiState.ReadyToEvolve:
                    status = "Congratulations, {0} " + string.Format("evolved into {0}!", sp.GetShortName());
                    break;

                case GotchiState.Sleeping:
                    long hours_left = gotchi.HoursOfSleepLeft();
                    status = "{0} is taking a nap. " + string.Format("Check back in {0} hour{1}.", hours_left, hours_left > 1 ? "s" : string.Empty);
                    break;

                case GotchiState.Hungry:
                    status = "{0} is feeling hungry!";
                    break;

                case GotchiState.Eating:
                    status = "{0} is enjoying some delicious Suka-Flakes™!";
                    break;

                case GotchiState.Energetic:
                    status = "{0} is feeling rowdy!";
                    break;

                case GotchiState.Tired:
                    status = "{0} is getting a bit sleepy...";
                    break;

            }

            // Send the message.

            EmbedBuilder embed = new EmbedBuilder();

            embed.WithTitle(string.Format("{0}'s \"{1}\"", Context.User.Username, StringUtils.ToTitleCase(gotchi.name)));
            embed.WithDescription(string.Format("{0}, age {1}", sp.GetShortName(), gotchi.Age()));
            embed.WithImageUrl(gif_url);
            embed.WithFooter(string.Format(status, StringUtils.ToTitleCase(gotchi.name)));

            await ReplyAsync("", false, embed.Build());

        }

        [Command("get")]
        public async Task Get(string species) {
            await Get("", species);
        }
        [Command("get")]
        public async Task Get(string genus, string species) {

            // Delete the user's old gotchi if already existed.

            using (SQLiteCommand cmd = new SQLiteCommand("DELETE FROM Gotchi WHERE owner_id=$owner_id;")) {

                cmd.Parameters.AddWithValue("$owner_id", Context.User.Id);

                await Database.ExecuteNonQuery(cmd);

            }

            // Get the species that the user specified.

            Species sp = await BotUtils.ReplyAsync_FindSpecies(Context, genus, species);

            if (sp is null)
                return;

            // The species must be a base species (e.g., doesn't evolve from anything).

            using (SQLiteCommand cmd = new SQLiteCommand("SELECT count(*) FROM Ancestors WHERE species_id=$species_id;")) {

                cmd.Parameters.AddWithValue("$species_id", sp.id);

                long count = await Database.GetScalar<long>(cmd);

                if (count > 0) {

                    await BotUtils.ReplyAsync_Error(Context, "You must start with a base species (i.e., a species that doesn't evolve from anything).");

                    return;

                }

            }

            // Create a gotchi for this user.

            await GotchiUtils.CreateGotchiAsync(Context.User, sp);

            await BotUtils.ReplyAsync_Success(Context, string.Format("All right **{0}**, take care of your new **{1}**!", Context.User.Username, sp.GetShortName()));

        }

        [Command("name")]
        public async Task Name(string name) {

            Gotchi gotchi = await GotchiUtils.GetGotchiAsync(Context.User);

            if (!await GotchiUtils.Reply_ValidateGotchiAsync(Context, gotchi))
                return;

            using (SQLiteCommand cmd = new SQLiteCommand("UPDATE Gotchi SET name=$name WHERE owner_id=$owner_id;")) {

                cmd.Parameters.AddWithValue("$name", name.ToLower());
                cmd.Parameters.AddWithValue("$owner_id", Context.User.Id);

                await Database.ExecuteNonQuery(cmd);

            }

            await BotUtils.ReplyAsync_Success(Context, string.Format("Sucessfully set {0}'s name to **{1}**.", StringUtils.ToTitleCase(gotchi.name), StringUtils.ToTitleCase(name)));

        }

        [Command("feed")]
        public async Task Feed() {

            Gotchi gotchi = await GotchiUtils.GetGotchiAsync(Context.User);

            if (!await GotchiUtils.Reply_ValidateGotchiAsync(Context, gotchi))
                return;

            if (gotchi.IsDead()) {

                await BotUtils.ReplyAsync_Info(Context, string.Format("You went to feed **{0}**, but it looks like it's too late...", StringUtils.ToTitleCase(gotchi.name)));

                return;

            }
            else if (gotchi.IsSleeping()) {

                await BotUtils.ReplyAsync_Info(Context, string.Format("Shhh, do not disturb! **{0}** is currently asleep. Try feeding them again later.", StringUtils.ToTitleCase(gotchi.name)));

                return;

            }

            using (SQLiteCommand cmd = new SQLiteCommand("UPDATE Gotchi SET fed_ts=$fed_ts WHERE owner_id=$owner_id;")) {

                cmd.Parameters.AddWithValue("$fed_ts", DateTimeOffset.UtcNow.ToUnixTimeSeconds());
                cmd.Parameters.AddWithValue("$owner_id", Context.User.Id);

                await Database.ExecuteNonQuery(cmd);

            }

            await BotUtils.ReplyAsync_Success(Context, string.Format("Fed **{0}** some delicious Suka-Flakes™!", StringUtils.ToTitleCase(gotchi.name)));

        }

        [Command("stats")]
        public async Task Stats() {

            // Get this user's gotchi.

            Gotchi gotchi = await GotchiUtils.GetGotchiAsync(Context.User);

            if (!await GotchiUtils.Reply_ValidateGotchiAsync(Context, gotchi))
                return;

            Species sp = await BotUtils.GetSpeciesFromDb(gotchi.species_id);

            if (!await BotUtils.ReplyAsync_ValidateSpecies(Context, sp))
                return;

            // Calculate stats for this gotchi.
            // If the user is currently in battle, show their battle stats instead.

            GotchiStats stats;

            GotchiBattleState battle_state = GotchiBattleState.GetBattleStateByUser(Context.User.Id);

            if (!(battle_state is null))
                stats = battle_state.GetStats(gotchi);
            else
                stats = await GotchiStats.CalculateStats(gotchi);

            // Create the embed.

            EmbedBuilder stats_page = new EmbedBuilder();

            stats_page.WithTitle(string.Format("{0}'s {2}, **Level {1}** (Age {3})", Context.User.Username, stats.level, sp.GetShortName(), gotchi.Age()));
            stats_page.WithThumbnailUrl(sp.pics);
            stats_page.WithFooter(string.Format("{0} experience points until next level", stats.ExperienceRequired()));

            stats_page.AddField("❤ Hit points", (int)stats.hp, inline: true);
            stats_page.AddField("💥 Attack", (int)stats.atk, inline: true);
            stats_page.AddField("🛡 Defense", (int)stats.def, inline: true);
            stats_page.AddField("💨 Speed", (int)stats.spd, inline: true);

            await ReplyAsync("", false, stats_page.Build());



        }

        [Command("moves"), Alias("moveset")]
        public async Task Moves() {

            // Get this user's gotchi.

            Gotchi gotchi = await GotchiUtils.GetGotchiAsync(Context.User);

            if (!await GotchiUtils.Reply_ValidateGotchiAsync(Context, gotchi))
                return;

            Species sp = await BotUtils.GetSpeciesFromDb(gotchi.species_id);

            if (!await BotUtils.ReplyAsync_ValidateSpecies(Context, sp))
                return;

            // Get moveset for this gotchi.

            GotchiMoveset set = await GotchiMoveset.GetMovesetAsync(gotchi);
            GotchiStats stats = await GotchiStats.CalculateStats(gotchi);

            // Create the embed.

            EmbedBuilder set_page = new EmbedBuilder();

            set_page.WithTitle(string.Format("{0}'s {2}, **Level {1}** (Age {3})", Context.User.Username, stats.level, sp.GetShortName(), gotchi.Age()));
            set_page.WithThumbnailUrl(sp.pics);
            set_page.WithFooter(string.Format("{0} experience points until next level", stats.ExperienceRequired()));

            int move_index = 1;

            foreach (GotchiMove move in set.moves)
                set_page.AddField(string.Format("Move {0}: **{1}**", move_index++, StringUtils.ToTitleCase(move.name)), move.description);

            await ReplyAsync("", false, set_page.Build());

        }

        [Command("battle"), Alias("challenge", "duel")]
        public async Task Battle(IUser user) {

            // Cannot challenge oneself.

            if (!(user is null) && user.Id == Context.User.Id) {

                await BotUtils.ReplyAsync_Error(Context, "You cannot challenge yourself.");

                return;

            }

            // Get this user's gotchi.

            Gotchi gotchi = await GotchiUtils.GetGotchiAsync(Context.User);

            if (!await _replyValidateChallengerGotchiForBattleAsync(Context, gotchi))
                return;

            // Get the opponent's gotchi.
            // If the opponent is null, assume the user is training. A random gotchi will be generated for them to battle against.

            Gotchi opposing_gotchi = null;

            if (!(user is null)) {

                opposing_gotchi = await GotchiUtils.GetGotchiAsync(user);

                if (!await _replyValidateOpponentGotchiForBattleAsync(Context, opposing_gotchi))
                    return;

            }

            // If the user is involved in an existing battle (in progress), do not permit them to start another.

            if (!await _replyVerifyChallengerAvailableForBattleAsync(Context))
                return;

            // If the other user is involved in a battle, do not permit them to start another.

            if (!(user is null) && !await _replyVerifyOpponentAvailableForBattleAsync(Context, user))
                return;

            // Challenge the user to a battle.

            await GotchiBattleState.RegisterBattleAsync(Context, gotchi, opposing_gotchi);

            if (!(user is null)) {

                // If the user is battling another user, show a message challenging them to battle.
                // Otherwise, the battle state will be shown automatically when calling RegisterBattleAsync.

                await ReplyAsync(string.Format("{0}, **{1}** is challenging you to a battle! Use `{2}gotchi accept` or `{2}gotchi deny` to respond to their challenge.",
                    user.Mention,
                    Context.User.Username,
                    OurFoodChainBot.GetInstance().GetConfig().prefix));

            }

        }

        [Command("train")]
        public async Task Train() {

            // Get this user's gotchi.

            Gotchi gotchi = await GotchiUtils.GetGotchiAsync(Context.User);

            if (!await GotchiUtils.Reply_ValidateGotchiAsync(Context, gotchi))
                return;

            // Users can train their gotchi by battling random gotchis 3 times every 15 minutes.

            long training_left = 0;
            long training_ts = 0;

            using (SQLiteCommand cmd = new SQLiteCommand("SELECT training_left, training_ts FROM Gotchi WHERE id=$id;")) {

                cmd.Parameters.AddWithValue("$id", gotchi.id);

                DataRow row = await Database.GetRowAsync(cmd);

                if (!(row is null)) {

                    training_left = row.IsNull("training_left") ? 0 : row.Field<long>("training_left");
                    training_ts = row.IsNull("training_ts") ? 0 : row.Field<long>("training_ts");

                }

            }

            // If it's been more than 15 minutes since the training timestamp was updated, reset the training count.

            long minutes_elapsed = (DateTimeOffset.UtcNow.ToUnixTimeSeconds() - training_ts) / 60;

            if (minutes_elapsed >= 15) {

                training_left = 3;
                training_ts = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            }

            // If the user has no more training attempts left, exit. 

            if (training_left <= 0) {

                long minutes_left = (15 - minutes_elapsed);

                await BotUtils.ReplyAsync_Info(Context, string.Format("**{0}** is feeling tired from all the training... Try again in {1} minute{2}.",
                    StringUtils.ToTitleCase(gotchi.name),
                    minutes_left <= 1 ? "a" : minutes_left.ToString(),
                    minutes_left > 1 ? "s" : ""));

                return;

            }

            // Update the user's training data.

            --training_left;

            using (SQLiteCommand cmd = new SQLiteCommand("UPDATE Gotchi SET training_left=$training_left, training_ts=$training_ts WHERE id=$id;")) {

                cmd.Parameters.AddWithValue("$id", gotchi.id);
                cmd.Parameters.AddWithValue("$training_left", training_left);
                cmd.Parameters.AddWithValue("$training_ts", training_ts);

                await Database.ExecuteNonQuery(cmd);

            }

            await Battle(null);

        }

        [Command("accept")]
        public async Task Accept() {

            // Get the battle state that the user is involved with.

            GotchiBattleState state = GotchiBattleState.GetBattleStateByUser(Context.User.Id);

            // If the state is null, the user has not been challenged to a battle.
            // Make sure that the user accepting the battle is not the one who initiated it.

            if (state is null ||
                (await state.GetOtherUserAsync(Context, Context.User.Id)) is null ||
                (await state.GetUser2Async(Context)).Id != Context.User.Id) {

                await BotUtils.ReplyAsync_Info(Context, "You have not been challenged to a battle.");

                return;

            }

            // Check if the battle was already accepted.

            if (state.accepted) {

                await BotUtils.ReplyAsync_Info(Context, "Your battle is already in progress!");

                return;

            }

            // Accept the battle.

            state.accepted = true;

            await ReplyAsync(string.Format("{0}, **{1}** has accepted your challenge!",
               (await state.GetOtherUserAsync(Context, Context.User.Id)).Mention,
               Context.User.Username));

            await GotchiBattleState.ShowBattleStateAsync(Context, state);

            // await BotUtils.ReplyAsync_Info(Context, state.message);

        }

        [Command("deny")]
        public async Task Deny() {

            // Get the battle state that the user is involved with.

            GotchiBattleState state = GotchiBattleState.GetBattleStateByUser(Context.User.Id);

            // If the state is null, the user has not been challenged to a battle.

            if (state is null || (await state.GetOtherUserAsync(Context, Context.User.Id)) is null) {

                await BotUtils.ReplyAsync_Info(Context, "You have not been challenged to a battle.");

                return;

            }

            // Check if the battle was already accepted.

            if (state.accepted) {

                await BotUtils.ReplyAsync_Info(Context, "Your battle is already in progress!");

                return;

            }

            // Deny the battle.

            GotchiBattleState.DeregisterBattle(Context.User.Id);

            await ReplyAsync(string.Format("{0}, **{1}** has denied your challenge.",
               (await state.GetOtherUserAsync(Context, Context.User.Id)).Mention,
               Context.User.Username));

        }

        [Command("move"), Alias("use")]
        public async Task Move(string moveIdentifier) {

            // Get the battle state that the user is involved with.

            GotchiBattleState state = GotchiBattleState.GetBattleStateByUser(Context.User.Id);

            // If the state is null, the user has not been challenged to a battle.

            if (state is null || (!state.IsBattlingCpu() && (await state.GetOtherUserAsync(Context, Context.User.Id)) is null)) {

                await BotUtils.ReplyAsync_Error(Context, "You have not been challenged to a battle.");

                return;

            }

            // Make sure that it is this user's turn.

            if (!state.IsTurn(Context.User.Id)) {

                await BotUtils.ReplyAsync_Error(Context, string.Format("It is currently {0}'s turn.",
                    state.IsBattlingCpu() ? await state.GetUsername2Async(Context) : (await state.GetOtherUserAsync(Context, Context.User.Id)).Mention));

                return;

            }

            // Get the move that was used.

            GotchiMoveset moves = await GotchiMoveset.GetMovesetAsync(state.GetGotchi(Context.User.Id));
            GotchiMove move = moves.GetMove(moveIdentifier);

            if (move is null) {

                await BotUtils.ReplyAsync_Error(Context, "The move you have selected is invalid. Please select a valid move.");

                return;

            }

            // Use the move/update the battle state.

            await state.UseMoveAsync(Context, move);

        }

        private static async Task<bool> _replyValidateChallengerGotchiForBattleAsync(ICommandContext context, Gotchi gotchi) {

            if (!await GotchiUtils.Reply_ValidateGotchiAsync(context, gotchi))
                return false;

            if (gotchi.IsDead()) {

                await BotUtils.ReplyAsync_Info(context, "Your gotchi has died, and is unable to battle.");

                return false;

            }

            return true;

        }
        private static async Task<bool> _replyValidateOpponentGotchiForBattleAsync(ICommandContext context, Gotchi gotchi) {

            if (!GotchiUtils.ValidateGotchi(gotchi)) {

                await BotUtils.ReplyAsync_Info(context, "Your opponent doesn't have a gotchi yet.");

                return false;

            }

            if (gotchi.IsDead()) {

                await BotUtils.ReplyAsync_Info(context, "Your opponent's gotchi has died, and is unable to battle.");

                return false;

            }

            return true;

        }
        private static async Task<bool> _replyVerifyChallengerAvailableForBattleAsync(ICommandContext context) {

            GotchiBattleState state = GotchiBattleState.GetBattleStateByUser(context.User.Id);

            if (!(state is null) && state.accepted) {

                ulong other_user_id = state.gotchi1.owner_id == context.User.Id ? state.gotchi2.owner_id : state.gotchi1.owner_id;
                IUser other_user = await context.Guild.GetUserAsync(other_user_id);

                // We won't lock them into the battle if the other user has left the server.

                if (!(other_user is null) || state.IsBattlingCpu()) {

                    await BotUtils.ReplyAsync_Info(context, string.Format("You are already battling **{0}**. You must finish the battle (or forfeit) before beginning a new one.",
                        state.IsBattlingCpu() ? await state.GetUsername2Async(context) : other_user.Mention));

                    return false;

                }

            }

            return true;

        }
        private static async Task<bool> _replyVerifyOpponentAvailableForBattleAsync(ICommandContext context, IUser user) {

            GotchiBattleState state = GotchiBattleState.GetBattleStateByUser(user.Id);

            if (!(state is null) && state.accepted) {

                ulong other_user_id = state.gotchi1.owner_id == context.User.Id ? state.gotchi2.owner_id : state.gotchi1.owner_id;
                IUser other_user = await context.Guild.GetUserAsync(other_user_id);

                // We won't lock them into the battle if the other user has left the server.

                if (!(other_user is null)) {

                    await BotUtils.ReplyAsync_Info(context, string.Format("**{0}** is currently battling someone else. Challenge them again later when they have finished.", other_user.Mention));

                    return false;

                }

            }

            return true;

        }

    }

}