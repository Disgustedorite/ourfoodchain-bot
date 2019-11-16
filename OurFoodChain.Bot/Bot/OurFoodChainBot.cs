﻿using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OurFoodChain.Bot {

    public class OurFoodChainBot {

        public OurFoodChainBot() {

            // Set up a default configuration.

            Config = new BotConfig();

            _discord_client = new DiscordSocketClient(
                new DiscordSocketConfig() {

                    LogLevel = Discord.LogSeverity.Info,

                    // Allows the bot to run on Windows 7.
                    WebSocketProvider = Discord.Net.Providers.WS4Net.WS4NetProvider.Instance

                });

            _discord_client.Log += _logAsync;
            _discord_client.MessageReceived += _onMessageReceivedAsync;
            _discord_client.ReactionAdded += _onReactionReceivedAsync;
            _discord_client.ReactionRemoved += _onReactionRemovedAsync;

            Instance = this;

        }

        public async Task LoadConfigAsync(string filePath) {

            if (!System.IO.File.Exists(filePath)) {
                await LogAsync(LogSeverity.Error, "Config", "The config.json file is missing. Please place this file in the same directory as the executable.");
                Environment.Exit(-1);
            }

            Config = Bot.BotConfig.Open(filePath);

            if (string.IsNullOrEmpty(Config.Token)) {
                await LogAsync(LogSeverity.Error, "Config", "You must specify your bot token in the config.json file. For details, see the README.");
                Environment.Exit(-1);
            }

            if (string.IsNullOrEmpty(Config.Prefix))
                Config.Prefix = Bot.BotConfig.DefaultPrefix;

        }

        public async Task StartAsync() {

            await LogAsync(LogSeverity.Info, "OurFoodChain", "Starting bot");

            // Initialize the database (apply updates, etc.).

            await LogAsync(LogSeverity.Info, "Database", "Initializing database");

            await Database.InitializeAsync();

            // Copy user's custom data to the main data directory.

            await CopyCustomDataFilesAsync();

            if (Config.GotchisEnabled)
                await _initializeGotchiContextAsync();

            // Initialize services.

            await LogAsync(LogSeverity.Info, "OurFoodChain", "Configuring services");

            await ConfigureServicesAsync();

            await ConnectAsync();

        }
        public async Task ConnectAsync() {

            await _discord_client.LoginAsync(TokenType.Bot, Config.Token);
            await _discord_client.StartAsync();

            await ReloadConfigAsync();

        }
        public async Task LogAsync(LogMessage logMessage) {

            Discord.LogSeverity dSeverity = Discord.LogSeverity.Info;

            switch (logMessage.Severity) {

                case LogSeverity.Info:

                    dSeverity = Discord.LogSeverity.Info;

                    break;

                case LogSeverity.Warning:

                    dSeverity = Discord.LogSeverity.Warning;

                    break;

                case LogSeverity.Error:

                    dSeverity = Discord.LogSeverity.Error;

                    break;

            }

            await LogAsync(dSeverity, logMessage.Source, logMessage.Message);

        }
        public async Task LogAsync(LogSeverity severity, string source, string message) {

            await LogAsync(new LogMessage {
                Severity = severity,
                Source = source,
                Message = message
            });

        }
        public async Task LogAsync(Discord.LogSeverity severity, string source, string message) {
            await _logAsync(new Discord.LogMessage(severity, source, message));
        }
        public async Task ReloadConfigAsync() {

            await _installCommandsAsync();

            await _discord_client.SetGameAsync(Config.Playing);

        }

        public bool CommandIsInstalled(string commandName) {
            return GetInstalledCommandByName(commandName) != null;
        }
        public CommandInfo GetInstalledCommandByName(string commandName) {

            if (CommandService is null)
                return null;

            foreach (CommandInfo info in CommandService.Commands)
                if (info.Name.ToLower() == commandName.ToLower() || info.Aliases.Any(y => y.ToLower() == commandName.ToLower()))
                    return info;

            return null;

        }

        public ulong UserId {
            get {
                return _discord_client.CurrentUser.Id;
            }
        }
        public IDiscordClient Client {
            get {
                return _discord_client;
            }
        }
        public CommandService CommandService { get; private set; }
        public IServiceProvider ServiceProvider { get; private set; }
        public BotConfig Config { get; set; } = new BotConfig();

        public static OurFoodChainBot Instance { get; private set; } = null;

        private DiscordSocketClient _discord_client;

        private async Task _installCommandsAsync() {

            CommandService = new CommandService();

            await CommandService.AddModulesAsync(System.Reflection.Assembly.GetEntryAssembly(), ServiceProvider);

            if (!Config.TrophiesEnabled)
                await CommandService.RemoveModuleAsync<TrophyCommands>();

            if (!Config.GotchisEnabled)
                await CommandService.RemoveModuleAsync<GotchiCommands>();

        }

        private async Task _logAsync(Discord.LogMessage message) {

            Console.WriteLine(message.ToString());

            await Task.FromResult(false);

        }
        private async Task _onMessageReceivedAsync(SocketMessage message) {

            // If the message was not sent by a user (e.g., Discord, bot, etc.), ignore it.
            if (!_isUserMessage(message))
                return;

            if (await Bot.DiscordUtils.HandleMultiPartMessageResponseAsync(message))
                return;

            if (!_isBotCommand(message as SocketUserMessage))
                return;

            await _executeCommand(message as SocketUserMessage);

        }
        private async Task _onReactionReceivedAsync(Cacheable<IUserMessage, ulong> cached, ISocketMessageChannel channel, SocketReaction reaction) {
            await Bot.DiscordUtils.HandlePaginatedMessageReactionAsync(cached, channel, reaction, true);
        }
        private async Task _onReactionRemovedAsync(Cacheable<IUserMessage, ulong> cached, ISocketMessageChannel channel, SocketReaction reaction) {
            await Bot.DiscordUtils.HandlePaginatedMessageReactionAsync(cached, channel, reaction, false);
        }

        private bool _isUserMessage(SocketMessage message) {

            var m = message as SocketUserMessage;

            return (m != null);

        }
        private int _getCommandArgumentsPosition(SocketUserMessage message) {

            int pos = 0;

            if (message == null)
                return pos;

            message.HasStringPrefix(Config.Prefix, ref pos, StringComparison.InvariantCultureIgnoreCase);
            message.HasMentionPrefix(_discord_client.CurrentUser, ref pos);

            return pos;

        }
        private bool _isBotCommand(SocketUserMessage message) {

            return _getCommandArgumentsPosition(message) != 0;

        }
        private async Task<bool> _executeCommand(SocketUserMessage message) {

            // If the message is just the bot's prefix, don't attempt to respond to it (this reduces "Unknown command" spam).

            if (message.Content == Config.Prefix)
                return false;

            int pos = _getCommandArgumentsPosition(message);
            var context = new CommandContext(_discord_client, message);

            var result = await CommandService.ExecuteAsync(context, pos, ServiceProvider);

            if (result.IsSuccess)
                return true;

            bool show_error_message = true;

            if (result.Error == CommandError.BadArgCount) {

                // Get the name of the command that the user attempted to use.

                System.Text.RegularExpressions.Match command_m = System.Text.RegularExpressions.Regex.Match(message.Content.Substring(pos),
                    @"^[^\s]+",
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase);

                // If help documentation exists for this command, display it.

                CommandHelpInfo command_info = HelpUtils.GetCommandInfo(command_m.Value);

                if (!(command_info is null)) {

                    EmbedBuilder embed = new EmbedBuilder();
                    embed.WithColor(Color.Red);
                    embed.WithTitle(string.Format("Incorrect usage of \"{0}\" command", command_m.Value));
                    embed.WithDescription("❌ " + result.ErrorReason);
                    embed.AddField("Example(s) of correct usage:", command_info.ExamplesToString(Config.Prefix));

                    await context.Channel.SendMessageAsync("", false, embed.Build());

                    show_error_message = false;

                }

            }

            if (show_error_message)
                await BotUtils.ReplyAsync_Error(context, result.ErrorReason);

            return false;

        }

        private async Task _initializeGotchiContextAsync() {

            Gotchis.GotchiContext gotchiContext = new Gotchis.GotchiContext();

            gotchiContext.LogAsync += async x => await LogAsync(x);

            // Load gotchi config.

            if (System.IO.File.Exists("gotchi-config.json"))
                gotchiContext.Config = Gotchis.GotchiConfig.Open("gotchi-config.json");

            // Initialize registries.

            await LogAsync(LogSeverity.Info, "Gotchi", "Registering gotchi types");

            await gotchiContext.TypeRegistry.RegisterAllAsync(Global.GotchiDataDirectory + "types/");

            await LogAsync(LogSeverity.Info, "Gotchi", "Finished registering gotchi types");

            await LogAsync(LogSeverity.Info, "Gotchi", "Registering gotchi statuses");

            await gotchiContext.StatusRegistry.RegisterAllAsync(Global.GotchiDataDirectory + "statuses/");

            await LogAsync(LogSeverity.Info, "Gotchi", "Finished registering gotchi statuses");

            await LogAsync(LogSeverity.Info, "Gotchi", "Registering gotchi moves");

            await gotchiContext.MoveRegistry.RegisterAllAsync(Global.GotchiDataDirectory + "moves/");

            await LogAsync(LogSeverity.Info, "Gotchi", "Finished registering gotchi moves");

            Global.GotchiContext = gotchiContext;

        }
        private async Task ConfigureServicesAsync() {

            ServiceProvider = new ServiceCollection()
                .AddSingleton<Gotchis.GotchiBackgroundService>()
                .BuildServiceProvider();

            await ServiceProvider.GetService<Gotchis.GotchiBackgroundService>().StartAsync();

        }
        private async Task CopyCustomDataFilesAsync() {

            if (System.IO.Directory.Exists(Global.CustomDataDirectory)) {

                IEnumerable<string> customFiles = System.IO.Directory.EnumerateFiles(Global.CustomDataDirectory, "*", System.IO.SearchOption.AllDirectories)
                    .Where(f => f != System.IO.Path.Combine(Global.CustomDataDirectory, "README.txt"));

                if (customFiles.Count() > 0) {

                    await LogAsync(LogSeverity.Info, "Database", "Copying custom data files");

                    foreach (string inputFilePath in customFiles) {

                        string relativeInputFilePath = inputFilePath.Replace(Global.CustomDataDirectory, string.Empty);

                        string relativeOutputDirectoryPath = System.IO.Path.GetDirectoryName(relativeInputFilePath);

                        if (!string.IsNullOrEmpty(relativeOutputDirectoryPath))
                            System.IO.Directory.CreateDirectory(System.IO.Path.Combine(Global.DataDirectory, relativeOutputDirectoryPath));

                        string outputFilePath = System.IO.Path.Combine(Global.DataDirectory, relativeInputFilePath);

                        System.IO.File.Copy(inputFilePath, outputFilePath, true);

                    }

                }

            }

        }

    }

}