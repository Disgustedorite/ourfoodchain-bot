﻿using MoonSharp.Interpreter;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OurFoodChain.Gotchi {

    [MoonSharpUserData]
    public class BattleGotchi {

        public GotchiContext Context { get; set; } = null;
        public Gotchi Gotchi { get; set; } = new Gotchi();

        public GotchiStats Stats { get; set; } = new GotchiStats();
        public GotchiType[] Types { get; set; } = new GotchiType[] { };
        public GotchiMoveSet Moves { get; set; } = new GotchiMoveSet();
        public GotchiStatus Status { get; private set; } = null;

        public bool HasStatus {
            get {
                return Status != null;
            }
        }
        public bool StatusChanged { get; set; } = false;
        public string StatusName {
            get {
                return HasStatus ? Status.Name : "";
            }
        }

        public bool TestRequirements(GotchiRequirements requirements) {

            return new GotchiRequirementsChecker { Requires = requirements }.CheckAsync(Gotchi).Result;

        }

        public void ResetStats() {

            if (Context is null)
                throw new Exception("Context is null");

            Stats = new GotchiStatsCalculator(Context).GetStatsAsync(Gotchi).Result;

        }

        public void SetStatus(string statusName) {

            if (Context is null)
                throw new Exception("Context is null");

            SetStatus(Context.StatusRegistry.GetStatusAsync(statusName).Result);

        }
        public void SetStatus(GotchiStatus status) {

            Status = status;

            StatusChanged = true;

        }
        public void ClearStatus() {

            Status = null;

            StatusChanged = true;

        }

    }

}