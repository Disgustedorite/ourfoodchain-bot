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

        private const string DEFAULT_PREFIX = "?";
        private const string DEFAULT_PLAYING = "";

        public OurFoodChainBot() {

            // Set up a default configuration.

            _config = new Config {
                prefix = DEFAULT_PREFIX,
                playing = DEFAULT_PLAYING
            };

            _discord_client = new DiscordSocketClient(
                new DiscordSocketConfig() {

                    LogLevel = LogSeverity.Info,

                    // Allows the bot to run on Windows 7.
                    WebSocketProvider = Discord.Net.Providers.WS4Net.WS4NetProvider.Instance

                });

            _command_service = new CommandService();
            _service_provider = new ServiceCollection().BuildServiceProvider();

            _discord_client.Log += _log;
            _discord_client.MessageReceived += _messageReceived;
            _discord_client.ReactionAdded += _reactionReceived;
            _discord_client.ReactionRemoved += _reactionRemoved;

            _instance = this;

        }

        public async Task LoadSettings(string filePath) {

            if (!System.IO.File.Exists(filePath)) {
                await Log(LogSeverity.Error, "Config", "The config.json file is missing. Please place this file in the same directory as the executable.");
                Environment.Exit(-1);
            }

            _config = JsonConvert.DeserializeObject<Config>(System.IO.File.ReadAllText(filePath));

            if (string.IsNullOrEmpty(_config.token)) {
                await Log(LogSeverity.Error, "Config", "You must specify your bot token in the config.json file. For details, see the README.");
                Environment.Exit(-1);
            }

            if (string.IsNullOrEmpty(_config.prefix))
                _config.token = DEFAULT_PREFIX;

        }

        public async Task Connect() {

            // Install commands.

            await _command_service.AddModulesAsync(System.Reflection.Assembly.GetEntryAssembly(), _service_provider);

            // Login to Discord.

            await _discord_client.LoginAsync(TokenType.Bot, _config.token);
            await _discord_client.StartAsync();

            // Set the bot's "Now Playing".

            await _discord_client.SetGameAsync(_config.playing);

        }
        public async Task Log(LogSeverity severity, string source, string message) {
            await _log(new LogMessage(severity, source, message));
        }

        public Config GetConfig() {
            return _config;
        }
        public ulong GetUserId() {

            return _discord_client.CurrentUser.Id;

        }
        public IDiscordClient GetClient() {

            return _discord_client;

        }

        public struct Config {
            public ulong[] bot_admin_user_ids;
            public ulong[] mod_role_ids;
            public string playing;
            public string prefix;
            public ulong scratch_channel;
            public ulong scratch_server;
            public string token;
        }

        public static OurFoodChainBot GetInstance() {
            return _instance;
        }

        static OurFoodChainBot _instance = null;
        Config _config;

        private DiscordSocketClient _discord_client;
        private CommandService _command_service;
        private IServiceProvider _service_provider;

        private async Task _log(LogMessage message) {

            Console.WriteLine(message.ToString());

            await Task.FromResult(false);

        }
        private async Task _messageReceived(SocketMessage message) {

            // If the message was not sent by a user (e.g., Discord, bot, etc.), ignore it.
            if (!_isUserMessage(message))
                return;

            if (await MultistageCommand.HandleResponseAsync(message))
                return;

            if (!_isBotCommand(message as SocketUserMessage))
                return;

            await _executeCommand(message as SocketUserMessage);

        }
        private async Task _reactionReceived(Cacheable<IUserMessage, ulong> cached, ISocketMessageChannel channel, SocketReaction reaction) {
            await CommandUtils.HandlePaginatedMessageReaction(cached, channel, reaction, true);
        }
        private async Task _reactionRemoved(Cacheable<IUserMessage, ulong> cached, ISocketMessageChannel channel, SocketReaction reaction) {
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

            message.HasStringPrefix(_config.prefix, ref pos, StringComparison.InvariantCultureIgnoreCase);
            message.HasMentionPrefix(_discord_client.CurrentUser, ref pos);

            return pos;

        }
        private bool _isBotCommand(SocketUserMessage message) {

            return _getCommandArgumentsPosition(message) != 0;

        }
        private async Task<bool> _executeCommand(SocketUserMessage message) {

            // If the message is just the bot's prefix, don't attempt to respond to it (this reduces "Unknown command" spam).

            if (message.Content == _config.prefix)
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
                    embed.AddField("Example(s) of correct usage:", command_info.ExamplesToString(_config.prefix));

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