﻿using Discord;
using Discord.Commands;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OurFoodChain.trophies {

    public class TrophyCommands :
        ModuleBase {

        [Command("trophies"), Alias("achievements")]
        public async Task Trophies(IUser user = null) {

            if (user is null)
                user = Context.User;

            UnlockedTrophyInfo[] unlocked = await TrophyRegistry.GetUnlockedTrophiesAsync(user.Id);

            Array.Sort(unlocked, (x, y) => x.timestamp.CompareTo(y.timestamp));

            EmbedBuilder embed = new EmbedBuilder();
            embed.WithTitle(string.Format("{0}'s Trophies ({1:0.##}%)", user.Username, 100.0 * unlocked.Count() / (await TrophyRegistry.GetTrophiesAsync()).Count));
            embed.WithFooter(string.Format("See a list of all available trophies with the \"{0}trophylist\" command.", OurFoodChainBot.GetInstance().GetConfig().prefix));
            embed.WithColor(new Color(255, 204, 77));

            StringBuilder description_builder = new StringBuilder();
            int total_users = (await Context.Guild.GetUsersAsync()).Count;

            foreach (UnlockedTrophyInfo info in unlocked) {

                Trophy trophy = await TrophyRegistry.GetTrophyByIdentifierAsync(info.identifier);

                if (trophy is null)
                    continue;

                description_builder.AppendLine(string.Format("{0} **{1}** - Earned {2} ({3:0.##}%)",
                   trophy.GetIcon(),
                   trophy.GetName(),
                   BotUtils.GetTimeStampAsDateString(info.timestamp),
                   100.0 * info.timesUnlocked / total_users
                  ));

            }

            embed.WithDescription(description_builder.ToString());

            await ReplyAsync("", false, embed.Build());

        }

        [Command("trophylist"), Alias("achievementlist")]
        public async Task TrophyList() {

            int total_users = (await Context.Guild.GetUsersAsync()).Count;
            int total_trophies = (await TrophyRegistry.GetTrophiesAsync()).Count;
            int trophies_per_page = 8;
            int total_pages = (int)Math.Ceiling((float)total_trophies / trophies_per_page);
            int current_page = 0;
            int current_page_trophy_count = 0;

            CommandUtils.PaginatedMessage message = new CommandUtils.PaginatedMessage();
            EmbedBuilder embed = null;

            foreach (Trophy trophy in await TrophyRegistry.GetTrophiesAsync()) {

                if (current_page_trophy_count == 0) {

                    ++current_page;

                    embed = new EmbedBuilder();
                    embed.WithTitle(string.Format("All Trophies ({0})", (await TrophyRegistry.GetTrophiesAsync()).Count));
                    embed.WithFooter(string.Format("Page {0} of {1} — Want to know more about a trophy? Use the \"{2}trophy\" command, e.g.: {2}trophy \"polar power\"", current_page, total_pages, OurFoodChainBot.GetInstance().GetConfig().prefix));
                    embed.WithColor(new Color(255, 204, 77));

                }

                long times_unlocked = await TrophyRegistry.GetTimesUnlocked(trophy);
                string description = (trophy.Flags.HasFlag(TrophyFlags.Hidden) && times_unlocked <= 0) ? string.Format("_{0}_", trophies.Trophy.HIDDEN_TROPHY_DESCRIPTION) : trophy.GetDescription();

                // If this was a first-time trophy, show who unlocked it.

                if (trophy.Flags.HasFlag(TrophyFlags.OneTime) && times_unlocked > 0) {

                    ulong[] user_ids = await TrophyRegistry.GetUsersUnlocked(trophy);

                    if (user_ids.Count() > 0) {

                        IGuildUser user = await Context.Guild.GetUserAsync(user_ids.First());

                        if (!(user is null))
                            description += string.Format(" (unlocked by {0})", user.Mention);

                    }

                }

                embed.AddField(string.Format("{0} **{1}** ({2:0.##}%)", trophy.GetIcon(), trophy.name, 100.0 * times_unlocked / total_users), description);

                ++current_page_trophy_count;

                if (current_page_trophy_count >= trophies_per_page) {

                    message.pages.Add(embed.Build());

                    current_page_trophy_count = 0;

                }

            }

            // Add the last embed to the message.
            if (!(embed is null))
                message.pages.Add(embed.Build());

            await CommandUtils.ReplyAsync_SendPaginatedMessage(Context, message);

        }

        [Command("trophy"), Alias("achievement")]
        public async Task Trophy(string name) {

            // Find the trophy with this name.

            Trophy trophy = null;

            foreach (Trophy t in await TrophyRegistry.GetTrophiesAsync())
                if (t.GetName().ToLower() == name.ToLower()) {

                    trophy = t;

                    break;

                }

            // If no such trophy exists, return an error.

            if (trophy is null) {

                await BotUtils.ReplyAsync_Error(Context, "No such trophy exists.");

                return;

            }

            // Show trophy information.

            int total_users = (await Context.Guild.GetUsersAsync()).Count;
            long times_unlocked = await TrophyRegistry.GetTimesUnlocked(trophy);

            EmbedBuilder embed = new EmbedBuilder();
            embed.WithTitle(string.Format("{0} {1} ({2:0.##}%)", trophy.GetIcon(), trophy.GetName(), 100.0 * times_unlocked / total_users));
            embed.WithDescription(trophy.Flags.HasFlag(TrophyFlags.Hidden) && times_unlocked <= 0 ? trophies.Trophy.HIDDEN_TROPHY_DESCRIPTION : trophy.GetDescription());
            embed.WithColor(new Color(255, 204, 77));

            await ReplyAsync("", false, embed.Build());

        }

        [Command("awardtrophy"), Alias("award", "awardachievement")]
        public async Task AwardTrophy(IGuildUser user, string trophy) {

            if (!await BotUtils.ReplyAsync_CheckPrivilege(Context, (IGuildUser)Context.User, PrivilegeLevel.ServerModerator))
                return;

            Trophy t = await TrophyRegistry.GetTrophyByNameAsync(trophy);

            if (t is null) {

                await BotUtils.ReplyAsync_Error(Context, "No such trophy exists.");

                return;

            }

            // #todo Show warning and do nothing if the user already has the trophy

            await TrophyRegistry.SetUnlocked(user.Id, t);

            await BotUtils.ReplyAsync_Success(Context, string.Format("Successfully awarded **{0}** trophy to {1}.", t.GetName(), user.Mention));

        }
        [Command("scantrophies"), Alias("trophyscan")]
        public async Task ScanTrophies(IGuildUser user = null) {

            if (user is null)
                user = (IGuildUser)Context.User;
            else if (!await BotUtils.ReplyAsync_CheckPrivilege(Context, (IGuildUser)Context.User, PrivilegeLevel.ServerModerator)) // Mod privileges are required to scan someone else's trophies
                return;

            await TrophyScanner.AddToQueueAsync(Context, user.Id, TrophyScanner.NO_DELAY);

            await BotUtils.ReplyAsync_Success(Context, string.Format("Successfully added user **{0}** to the trophy scanner queue.", user.Username));

        }

    }

}