﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace OurFoodChain {

    class StringUtils {

        public static string ToTitleCase(string str) {

            return System.Threading.Thread.CurrentThread.CurrentCulture.TextInfo.ToTitleCase(str.ToLower());

        }
        public static string ToSentenceCase(string str) {

            if (string.IsNullOrEmpty(str) || str.Length < 1)
                return str;

            str = str.ToLower();

            return str[0].ToString().ToUpper() + str.Substring(1);

        }
        public static bool IsNumeric(string str) {

            double num;

            return double.TryParse(str, out num);

        }
        public static string DisjunctiveJoin(string separator, IEnumerable<string> values) {
            return _joinWithUniqueEndSeparator(separator, " or ", values);
        }
        public static string ConjunctiveJoin(string separator, IEnumerable<string> values) {
            return _joinWithUniqueEndSeparator(separator, " and ", values);
        }
        public static string GetFirstSentence(string value) {

            Match match = Regex.Match(value, "^[a-zA-Z0-9 ,;:\\-\"’']+(?:\\.+|[!\\?])");

            if (!match.Success || match.Length <= 0)
                return value;

            return match.Value;

        }
        public static string ReplaceWhitespaceCharacters(string value, string with = "_") {

            if (string.IsNullOrEmpty(value))
                return value;

            return Regex.Replace(value, @"\s", with);

        }

        // https://stackoverflow.com/questions/11454004/calculate-a-md5-hash-from-a-string
        public static string CreateMD5(string input) {
            // Use input string to calculate MD5 hash
            using (System.Security.Cryptography.MD5 md5 = System.Security.Cryptography.MD5.Create()) {
                byte[] inputBytes = System.Text.Encoding.ASCII.GetBytes(input);
                byte[] hashBytes = md5.ComputeHash(inputBytes);

                // Convert the byte array to hexadecimal string
                StringBuilder sb = new StringBuilder();
                for (int i = 0; i < hashBytes.Length; i++) {
                    sb.Append(hashBytes[i].ToString("X2"));
                }
                return sb.ToString();
            }
        }
        public static int SumStringChars(string value) {

            int sum = 0;

            for (int i = 0; i < value.Length; ++i)
                sum += value[i];

            return sum;

        }

        public static string TimeSpanToString(TimeSpan span) {

            string format = "{0} {1}";

            if (span < TimeSpan.FromSeconds(60))
                return string.Format(format, span.Seconds, span.Seconds == 1 ? "second" : "seconds");
            else if (span < TimeSpan.FromMinutes(60))
                return string.Format(format, span.Minutes, span.Minutes == 1 ? "minute" : "minutes");
            else if (span < TimeSpan.FromHours(24))
                return string.Format(format, span.Hours, span.Hours == 1 ? "hour" : "hours");
            else if (span < TimeSpan.FromDays(30))
                return string.Format(format, span.Days, span.Days == 1 ? "day" : "days");
            else if (span < TimeSpan.FromDays(365))
                return string.Format(format, span.Days / 30, span.Days / 30 == 1 ? "day" : "days");
            else
                return string.Format(format, span.Days / 365, span.Days / 365 == 1 ? "year" : "years");

        }

        private static string _joinWithUniqueEndSeparator(string separator, string endSeparator, IEnumerable<string> values) {

            if (values.Count() <= 0)
                return "";

            if (values.Count() == 1)
                return values.First();

            if (values.Count() == 2)
                return string.Join(endSeparator, values);

            StringBuilder sb = new StringBuilder();

            for (int i = 0; i + 1 < values.Count(); ++i) {
                sb.Append(values.ElementAt(i));
                sb.Append(separator);
            }

            sb.Append(endSeparator);
            sb.Append(values.Last());

            return sb.ToString();


        }

    }

}