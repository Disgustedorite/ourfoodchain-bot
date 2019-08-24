﻿using Discord;
using Discord.Commands;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OurFoodChain {

    public class HelpCommands :
        ModuleBase {

        [Command("help"), Alias("h")]
        public async Task Help(string command = "", string nestedCommand = "") {

            await ShowHelp(Context, command, nestedCommand);

        }

        private static async Task _showHelpCategory(ICommandContext context, List<HelpUtils.CommandInfo> commandInfo, string category) {

            SortedDictionary<string, List<HelpUtils.CommandInfo>> commands_lists = new SortedDictionary<string, List<HelpUtils.CommandInfo>>();

            foreach (HelpUtils.CommandInfo c in commandInfo) {

                if (!commands_lists.ContainsKey(c.category))
                    commands_lists[c.category] = new List<HelpUtils.CommandInfo>();

                commands_lists[c.category].Add(c);

            }

            EmbedBuilder builder = new EmbedBuilder();
            StringBuilder description_builder = new StringBuilder();

            if (!string.IsNullOrEmpty(category))
                description_builder.AppendLine(string.Format("Commands listed must be prefaced with `{0}` (e.g. `{2}{0} {1}`).",
                    category,
                    commandInfo[0].name,
                    OurFoodChainBot.Instance.Config.Prefix));

            description_builder.AppendLine((string.Format("To learn more about a command, use `{0}help <command>` (e.g. `{0}help {1}{2}`).",
                OurFoodChainBot.Instance.Config.Prefix,
                string.IsNullOrEmpty(category) ? "" : category + " ",
                commandInfo[0].name)));

            string version_string = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString();

            // Remove any trailing ".0"s from the version number, so they don't appear when unused.
            while (version_string.EndsWith(".0"))
                version_string = version_string.Substring(0, version_string.Length - 2);

            builder.WithTitle("Commands list");
            builder.WithDescription(description_builder.ToString());
            builder.WithFooter(string.Format("ourfoodchain-bot v.{0} — github.com/gsemac/ourfoodchain-bot", version_string));

            foreach (string cat in commands_lists.Keys) {

                List<string> command_str_list = new List<string>();

                commands_lists[cat].Sort((lhs, rhs) => lhs.name.CompareTo(rhs.name));

                foreach (HelpUtils.CommandInfo c in commands_lists[cat])
                    command_str_list.Add(string.Format("`{0}`", c.name));

                builder.AddField(StringUtils.ToTitleCase(cat), string.Join("  ", command_str_list));

            }

            await context.Channel.SendMessageAsync("", false, builder.Build());

        }
        public static async Task ShowHelpCategory(ICommandContext context, string commandInfoDirectory, string category) {

            if (!System.IO.Directory.Exists(commandInfoDirectory)) {

                await BotUtils.ReplyAsync_Error(context, string.Format("Help information cannot be displayed, because the help directory \"{0}\" does not exist.", commandInfoDirectory));

                return;

            }

            List<HelpUtils.CommandInfo> command_info = new List<HelpUtils.CommandInfo>();
            string[] fnames = System.IO.Directory.GetFiles(commandInfoDirectory, "*.json", System.IO.SearchOption.TopDirectoryOnly);

            foreach (string fname in fnames)
                command_info.Add(JsonConvert.DeserializeObject<HelpUtils.CommandInfo>(System.IO.File.ReadAllText(fname)));

            await _showHelpCategory(context, command_info, category);

        }
        public static async Task ShowHelp(ICommandContext context, string command, string nestedCommand) {

            // Load the .json files containing command information.
            // If there is a subdirectory in the help directory with the same name as the command, load files in that subdirectory instead.

            List<HelpUtils.CommandInfo> command_info = new List<HelpUtils.CommandInfo>();
            string help_directory = Constants.HELP_DIRECTORY;

            if (!string.IsNullOrEmpty(nestedCommand) && System.IO.Directory.Exists(System.IO.Path.Combine(help_directory, command)))
                help_directory = System.IO.Path.Combine(help_directory, command);

            if (!System.IO.Directory.Exists(help_directory)) {

                await BotUtils.ReplyAsync_Error(context, string.Format("Help information cannot be displayed, because the help directory \"{0}\" does not exist.", help_directory));

                return;

            }

            string[] fnames = System.IO.Directory.GetFiles(help_directory, "*.json", System.IO.SearchOption.TopDirectoryOnly);

            foreach (string fname in fnames)
                command_info.Add(JsonConvert.DeserializeObject<HelpUtils.CommandInfo>(System.IO.File.ReadAllText(fname)));

            if (!string.IsNullOrEmpty(command)) {

                // Find the requested command.

                command = command.ToLower();
                nestedCommand = nestedCommand.ToLower();

                HelpUtils.CommandInfo info = null;

                foreach (HelpUtils.CommandInfo c in command_info)
                    if (string.IsNullOrEmpty(nestedCommand) ? (c.name == command || c.aliases.Contains(command)) : (c.name == nestedCommand || c.aliases.Contains(nestedCommand))) {

                        info = c;

                        if (!string.IsNullOrEmpty(nestedCommand))
                            info.name = command.Trim() + " " + info.name;

                        break;

                    }

                if (info is null)
                    await context.Channel.SendMessageAsync("The given command does not exist, or is not yet documented.");
                else {

                    EmbedBuilder builder = new EmbedBuilder();

                    builder.WithTitle(string.Format("Help: {0}", info.name));

                    builder.AddField("Description", info.description.Replace("\\prefix", OurFoodChainBot.Instance.Config.Prefix));

                    if (info.aliases.Count() > 0)
                        builder.AddField("Aliases", string.Join(", ", info.aliases));

                    if (info.examples.Count() > 0) {

                        for (int i = 0; i < info.examples.Count(); ++i)
                            info.examples[i] = "`" + OurFoodChainBot.Instance.Config.Prefix + (string.IsNullOrEmpty(nestedCommand) ? "" : command.Trim() + " ") + info.examples[i] + "`";

                        builder.AddField("Example(s)", string.Join(Environment.NewLine, info.examples));

                    }

                    await context.Channel.SendMessageAsync("", false, builder.Build());

                }

            }
            else
                await _showHelpCategory(context, command_info, "");

        }

    }

}