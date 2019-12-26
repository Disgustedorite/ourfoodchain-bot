﻿using Discord;
using Discord.Commands;
using Newtonsoft.Json;
using OurFoodChain.Bot.Attributes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OurFoodChain.Bot.Modules {

    public class HelpModule :
        ModuleBase {

        // Public members

        public IOurFoodChainBotConfiguration BotConfiguration { get; set; }
        public Discord.Services.ICommandHandlingService CommandHandlingService { get; set; }
        public IServiceProvider ServiceProvider { get; set; }
        public Discord.Services.IHelpService HelpService { get; set; }

        [Command("help"), Alias("h")]
        public async Task Help() {

            IEnumerable<Discord.ICommandHelpInfo> helpInfos = await HelpService.GetCommandHelpInfoAsync(Context);

            await ReplyAsync(embed: Discord.DiscordUtilities.BuildCommandHelpInfoEmbed(helpInfos, BotConfiguration).Build());

        }
        [Command("help"), Alias("h")]
        public async Task Help([Remainder]string commandName) {

            Discord.ICommandHelpInfo helpInfo = await HelpService.GetCommandHelpInfoAsync(commandName.Trim());

            await ReplyAsync(embed: Discord.DiscordUtilities.BuildCommandHelpInfoEmbed(helpInfo, BotConfiguration).Build());

        }

    }

}