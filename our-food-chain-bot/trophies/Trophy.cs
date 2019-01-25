﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OurFoodChain.trophies {

    public enum TrophyFlags {
        Hidden = 1,
        OneTime = 2
    }

    public class Trophy {

        public Trophy(string name, string description, Func<TrophyScanner.ScannerQueueItem, Task<bool>> checkUnlocked) {

            this.name = name;
            _description = description;
            _checkUnlocked = checkUnlocked;
            Flags = 0;

        }
        public Trophy(string name, string description, TrophyFlags flags, Func<TrophyScanner.ScannerQueueItem, Task<bool>> checkUnlocked) :
            this(name, description, checkUnlocked) {

            Flags = flags;

        }

        public string GetName() {
            return StringUtils.ToTitleCase(name);
        }
        public string GetIdentifier() {
            return name.ToLower().Replace(' ', '_');
        }
        public string GetDescription() {
            return _description;
        }
        public async Task<bool> IsUnlocked(TrophyScanner.ScannerQueueItem item) {

            if (_checkUnlocked is null)
                return false;

            return await _checkUnlocked(item);

        }

        public TrophyFlags Flags { get; }

        public string name;


        private string _description;
        private Func<TrophyScanner.ScannerQueueItem, Task<bool>> _checkUnlocked;

    }

}