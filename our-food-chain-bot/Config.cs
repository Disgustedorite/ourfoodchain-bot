﻿using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OurFoodChain {

    public class Config {

        /// <summary>
        /// The default prefix used when no other prefix has been specified.
        /// </summary>
        public const string DEFAULT_PREFIX = "?";
        /// <summary>
        /// The default "Playing" text shown on the bot's profile.
        /// </summary>
        public const string DEFAULT_PLAYING = "";

        [JsonProperty("token")]
        public string Token { get; set; }
        [JsonProperty("playing")]
        public string Playing { get; set; } = DEFAULT_PLAYING;
        [JsonProperty("prefix")]
        public string Prefix { get; set; } = DEFAULT_PREFIX;

        [JsonProperty("bot_admin_user_ids")]
        public ulong[] BotAdminUserIds { get; set; }
        [JsonProperty("mod_role_ids")]
        public ulong[] ModRoleIds { get; set; }

        [JsonProperty("scratch_channel")]
        public ulong ScratchChannel { get; set; }
        [JsonProperty("scratch_server")]
        public ulong ScratchServer { get; set; }

        [JsonProperty("review_channels")]
        public ulong[][] ReviewChannels { get; set; }

        [JsonProperty("trophies_enabled")]
        public bool TrophiesEnabled { get; set; } = true;
        [JsonProperty("gotchis_enabled")]
        public bool GotchisEnabled { get; set; } = true;

    }

}