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

namespace OurFoodChain {

    public class OurFoodChainBot {

        public OurFoodChainBot() {

            // Set up a default configuration.

            Config = new Config();

            _discord_client = new DiscordSocketClient(
                new DiscordSocketConfig() {

                    LogLevel = LogSeverity.Info,

                    // Allows the bot to run on Windows 7.
                    WebSocketProvider = Discord.Net.Providers.WS4Net.WS4NetProvider.Instance

                });

            _discord_client.Log += _logAsync;
            _discord_client.MessageReceived += _onMessageReceivedAsync;
            _discord_client.ReactionAdded += _onReactionReceivedAsync;
            _discord_client.ReactionRemoved += _onReactionRemovedAsync;

            _instance = this;

        }

        public async Task LoadConfigAsync(string filePath) {

            if (!System.IO.File.Exists(filePath)) {
                await LogAsync(LogSeverity.Error, "Config", "The config.json file is missing. Please place this file in the same directory as the executable.");
                Environment.Exit(-1);
            }

            Config = Config.FromFile(filePath);

            if (string.IsNullOrEmpty(Config.Token)) {
                await LogAsync(LogSeverity.Error, "Config", "You must specify your bot token in the config.json file. For details, see the README.");
                Environment.Exit(-1);
            }

            if (string.IsNullOrEmpty(Config.Prefix))
                Config.Prefix = Config.DEFAULT_PREFIX;

        }

        public async Task ConnectAsync() {

            await _discord_client.LoginAsync(TokenType.Bot, Config.Token);
            await _discord_client.StartAsync();

            await ReloadConfigAsync();

        }
        public async Task LogAsync(LogSeverity severity, string source, string message) {
            await _logAsync(new LogMessage(severity, source, message));
        }
        public async Task ReloadConfigAsync() {

            await _installCommandsAsync();

            await _discord_client.SetGameAsync(Config.Playing);

        }

        public bool CommandIsLoaded(string commandName) {

            if (_command_service is null)
                return false;

            return _command_service.Commands
                .Any(x => x.Name.ToLower() == commandName.ToLower() || x.Aliases.Any(y => y.ToLower() == commandName.ToLower()));

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
        public Config Config { get; set; } = new Config();

        public static OurFoodChainBot Instance {
            get {
                return _instance;
            }
        }

        // Private members

        private static OurFoodChainBot _instance = null;

        private DiscordSocketClient _discord_client;
        private CommandService _command_service;
        private IServiceProvider _service_provider;

        private async Task _installCommandsAsync() {

            _command_service = new CommandService();
            _service_provider = new ServiceCollection().BuildServiceProvider();

            await _command_service.AddModulesAsync(System.Reflection.Assembly.GetEntryAssembly(), _service_provider);

            if (!Config.TrophiesEnabled)
                await _command_service.RemoveModuleAsync<Trophies.Commands>();

            if (!Config.GotchisEnabled)
                await _command_service.RemoveModuleAsync<Gotchi.Commands>();

        }

        private async Task _logAsync(LogMessage message) {

            Console.WriteLine(message.ToString());

            await Task.FromResult(false);

        }
        private async Task _onMessageReceivedAsync(SocketMessage message) {

            // If the message was not sent by a user (e.g., Discord, bot, etc.), ignore it.
            if (!_isUserMessage(message))
                return;

            if (await MultistageCommand.HandleResponseAsync(message))
                return;

            if (!_isBotCommand(message as SocketUserMessage))
                return;

            await _executeCommand(message as SocketUserMessage);

        }
        private async Task _onReactionReceivedAsync(Cacheable<IUserMessage, ulong> cached, ISocketMessageChannel channel, SocketReaction reaction) {
            await CommandUtils.HandlePaginatedMessageReaction(cached, channel, reaction, true);
        }
        private async Task _onReactionRemovedAsync(Cacheable<IUserMessage, ulong> cached, ISocketMessageChannel channel, SocketReaction reaction) {
            await CommandUtils.HandlePaginatedMessageReaction(cached, channel, reaction, false);
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

            var result = await _command_service.ExecuteAsync(context, pos, _service_provider);

            if (result.IsSuccess)
                return true;

            bool show_error_message = true;

            if (result.Error == CommandError.BadArgCount) {

                // Get the name of the command that the user attempted to use.

                System.Text.RegularExpressions.Match command_m = System.Text.RegularExpressions.Regex.Match(message.Content.Substring(pos),
                    @"^[^\s]+",
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase);

                // If help documentation exists for this command, display it.

                HelpUtils.CommandInfo command_info = HelpUtils.GetCommandInfo(command_m.Value);

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

    }

}