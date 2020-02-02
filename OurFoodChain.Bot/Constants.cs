﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OurFoodChain {

    public static class Constants {

        public static string DataDirectory { get; } = "data/";
        public static string CustomDataDirectory { get; } = "customdata/";
        public static string TempDirectory { get; } = DataDirectory + "temp/";

        public static string GotchiDataDirectory { get; } = DataDirectory + "gotchi/";
        public static string GotchiMovesDirectory { get; } = GotchiDataDirectory + "moves/";
        public static string GotchiItemsDirectory { get; } = GotchiDataDirectory + "items/";
        public static string GotchiImagesDirectory { get; } = GotchiDataDirectory + "images/";

        public static string DatabaseDirectory { get; } = string.Empty;
        public static string DatabaseFilePath { get; } = DatabaseDirectory + "data.db";
        public static string DatabaseUpdatesDirectory { get; } = DataDirectory + "updates/";

    }

}