﻿using Discord;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OurFoodChain.Bot {

    public class PaginatedMessageBuilder :
        ICollection<EmbedBuilder> {

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

        public Action<PaginatedMessageReactionCallbackArgs> Callback { get; set; }

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

        public int Count => _pages.Count;
        public bool IsReadOnly => false;

        public PaginatedMessageBuilder() { }
        public PaginatedMessageBuilder(List<EmbedBuilder> pages) {

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
        public void SetImageUrl(string value) {

            if (_pages.Count <= 0)
                _pages.Add(new EmbedBuilder());

            foreach (EmbedBuilder page in _pages)
                page.WithImageUrl(value);

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

        public void SetCallback(Action<PaginatedMessageReactionCallbackArgs> callback) {
            Callback = callback;
        }

        public void AddReaction(string reaction) {

            _reactions.Add(reaction);

        }
        public void AddReaction(PaginatedMessageReaction reaction) {

            string str = PaginatedMessage.ReactionTypeToString(reaction);

            if (!string.IsNullOrEmpty(str))
                AddReaction(str);

        }

        public PaginatedMessage Build() {

            PaginatedMessage message = new PaginatedMessage {
                Message = Message,
                RespondToSenderOnly = Restricted
            };

            foreach (EmbedBuilder page in _pages)
                message.Pages.Add(page.Build());

            message.ReactionCallback = Callback;

            // In the future, all reactions should be added.
            if (_reactions.Count() > 0)
                message.ToggleEmoji = _reactions[0];

            return message;

        }

        public void Add(EmbedBuilder item) {
            _pages.Add(item);
        }
        public void Clear() {
            _pages.Clear();
        }
        public bool Contains(EmbedBuilder item) {
            return _pages.Contains(item);
        }
        public void CopyTo(EmbedBuilder[] array, int arrayIndex) {
            _pages.CopyTo(array, arrayIndex);
        }
        public bool Remove(EmbedBuilder item) {
            return _pages.Remove(item);
        }
        public IEnumerator<EmbedBuilder> GetEnumerator() {
            return _pages.GetEnumerator();
        }
        IEnumerator IEnumerable.GetEnumerator() {
            return _pages.GetEnumerator();
        }

        private string _title = "";
        private string _description = "";
        private Color _color = Color.DarkGrey;

        private List<EmbedBuilder> _pages = new List<EmbedBuilder>();
        private List<string> _reactions = new List<string>();

    }

}