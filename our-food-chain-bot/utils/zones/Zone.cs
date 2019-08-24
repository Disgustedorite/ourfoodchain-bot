﻿using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace OurFoodChain {

    public enum ZoneType {
        Unknown,
        Aquatic,
        Terrestrial
    }

    public class Zone {

        public long Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public ZoneType Type { get; set; }
        public string Pics { get; set; }

        public string ShortName {
            get {
                return GetShortName();
            }
        }
        public string FullName {
            get {
                return GetFullName();
            }
        }
        public string ShortDescription {
            get {
                return GetShortDescription();
            }
        }
        public string Icon {
            get {

                switch (Type) {

                    case ZoneType.Aquatic:
                        return "🌊";

                    case ZoneType.Terrestrial:
                        return "🌳";

                    default:
                        return "❓";

                }

            }
        }

        public string GetShortDescription() {
            return StringUtils.GetFirstSentence(GetDescriptionOrDefault());
        }
        public string GetDescriptionOrDefault() {

            if (string.IsNullOrEmpty(Description))
                return BotUtils.DEFAULT_ZONE_DESCRIPTION;

            return Description;

        }
        public string GetShortName() {

            return Regex.Replace(Name, "^zone\\s+", "", RegexOptions.IgnoreCase);

        }
        public string GetFullName() {
            return ZoneUtils.FormatZoneName(Name);
        }

    }

}