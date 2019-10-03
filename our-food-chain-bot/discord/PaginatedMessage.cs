﻿using Discord;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OurFoodChain {

    public enum PaginatedMessageReaction {
        None,
        Next,
        Previous,
        Yes,
        No
    }

    public class PaginatedMessageCallbackArgs {

        // Public members

        public IUserMessage DiscordMessage { get; set; }
        public CommandUtils.PaginatedMessage PaginatedMessage { get; set; }

        public bool ReactionAdded { get; set; } = false;

        public string Reaction { get; set; } = "";
        public PaginatedMessageReaction ReactionType {
            get {
                return OurFoodChain.PaginatedMessage.StringToReactionType(Reaction);
            }
        }

    }

    public class PaginatedMessage {

        public string Message { get; set; }
        public string Title {
            get {
                return _title;
            }
            set {
                _title = value;
            }
        }
        public string Description {
            get {
                return _description;
            }
            set {
                _description = value;
            }
        }
        public Color Color {
            get {
                return _color;
            }
            set {
                _color = value;
            }
        }

        public Action<PaginatedMessageCallbackArgs> Callback { get; set; }

        public bool Restricted { get; set; } = false;

        public int Length {
            get {
                return _pages.Count > 0 ? _pages[0].Length : 0;
            }
        }
        public int FieldCount {
            get {

                int count = 0;

                _pages.ForEach(x => count += x.Fields.Count);

                return count;

            }
        }

        public PaginatedMessage() { }
        public PaginatedMessage(List<EmbedBuilder> pages) {

            _pages.AddRange(pages);
        }

        public void SetTitle(string title) {

            if (_pages.Count <= 0)
                _pages.Add(new EmbedBuilder());

            foreach (EmbedBuilder page in _pages)
                page.WithTitle(title);

        }
        public void PrependDescription(string description) {

            if (_pages.Count <= 0)
                _pages.Add(new EmbedBuilder());

            foreach (EmbedBuilder page in _pages)
                page.WithDescription(string.IsNullOrEmpty(page.Description) ? description : description + page.Description);

        }
        public void SetDescription(string description) {

            if (_pages.Count <= 0)
                _pages.Add(new EmbedBuilder());

            foreach (EmbedBuilder page in _pages)
                page.WithDescription(description);

        }
        public void SetThumbnailUrl(string thumbnailUrl) {

            if (_pages.Count <= 0)
                _pages.Add(new EmbedBuilder());

            foreach (EmbedBuilder page in _pages)
                page.WithThumbnailUrl(thumbnailUrl);

        }
        public void SetFooter(string footer) {

            if (_pages.Count <= 0)
                _pages.Add(new EmbedBuilder());

            foreach (EmbedBuilder page in _pages)
                page.WithFooter(footer);

        }
        public void AppendFooter(string footer) {

            if (_pages.Count <= 0)
                _pages.Add(new EmbedBuilder());

            foreach (EmbedBuilder page in _pages)
                page.WithFooter(page.Footer is null ? footer : page.Footer.Text + footer);

        }
        public void SetColor(Color color) {

            foreach (EmbedBuilder page in _pages)
                page.WithColor(color);

        }
        public void SetColor(byte r, byte g, byte b) {

            foreach (EmbedBuilder page in _pages)
                page.WithColor(new Color(r, g, b));

        }

        public void AddPages(IEnumerable<EmbedBuilder> pages) {

            if (!string.IsNullOrEmpty(Title))
                pages.ToList().ForEach(x => {
                    x.Title = string.IsNullOrEmpty(x.Title) ? Title : x.Title;
                });

            if (!string.IsNullOrEmpty(Description))
                pages.ToList().ForEach(x => {
                    if (!x.Description.StartsWith(Description))
                        x.Description = Description + x.Description;
                });

            if (Color != Color.DarkGrey)
                pages.ToList().ForEach(x => {
                    x.Color = Color;
                });

            _pages.AddRange(pages);

        }
        public void AddPageNumbers() {

            int num = 1;

            foreach (EmbedBuilder page in _pages) {

                string num_string = string.Format("Page {0} of {1}", num, _pages.Count());

                page.WithFooter(page.Footer is null || string.IsNullOrEmpty(page.Footer.Text) ? num_string : num_string + " — " + page.Footer.Text);

                ++num;

            }

        }

        public void SetCallback(Action<PaginatedMessageCallbackArgs> callback) {
            Callback = callback;
        }

        public void AddReaction(string reaction) {

            _reactions.Add(reaction);

        }
        public void AddReaction(PaginatedMessageReaction reaction) {

            string str = ReactionTypeToString(reaction);

            if (!string.IsNullOrEmpty(str))
                AddReaction(str);

        }

        public CommandUtils.PaginatedMessage Build() {

            CommandUtils.PaginatedMessage message = new CommandUtils.PaginatedMessage {
                message = Message,
                RespondToSenderOnly = Restricted
            };

            foreach (EmbedBuilder page in _pages)
                message.pages.Add(page.Build());

            message.callback = Callback;

            // In the future, all reactions should be added.
            if (_reactions.Count() > 0)
                message.emojiToggle = _reactions[0];

            return message;

        }

        public static string ReactionTypeToString(PaginatedMessageReaction reaction) {

            switch (reaction) {

                case PaginatedMessageReaction.Next:
                    return "▶";

                case PaginatedMessageReaction.Previous:
                    return "◀";

                case PaginatedMessageReaction.Yes:
                    return "👍";

                case PaginatedMessageReaction.No:
                    return "👎";

                default:
                    return string.Empty;

            }

        }
        public static PaginatedMessageReaction StringToReactionType(string reaction) {

            switch (reaction) {

                case "▶":
                    return PaginatedMessageReaction.Next;

                case "◀":
                    return PaginatedMessageReaction.Previous;

                case "👍":
                    return PaginatedMessageReaction.Yes;

                case "👎":
                    return PaginatedMessageReaction.No;

                default:
                    return PaginatedMessageReaction.None;

            }

        }

        private string _title = "";
        private string _description = "";
        private Color _color = Color.DarkGrey;

        private List<EmbedBuilder> _pages = new List<EmbedBuilder>();
        private List<string> _reactions = new List<string>();

    }

}