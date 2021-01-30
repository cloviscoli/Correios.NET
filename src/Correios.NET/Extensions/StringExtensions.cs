using System;
using System.Text.RegularExpressions;

namespace Correios.NET.Extensions
{
    public static class StringExtensions
    {
        public const char SPACE = ' ';
        public const string HYPHEN = "-";

        public static string RemoveLineEndings(this string text)
        {
            return Regex.Replace(text, @"(\r\n?|\n|\t)", SPACE.ToString()).Trim();
        }

        public static string RemoveNonNumeric(this string text)
        {
            return Regex.Replace(text, "[^0-9.]", string.Empty);
        }

        public static string RemoveHyphens(this string text)
        {
            return text.Replace(HYPHEN, string.Empty).Trim();
        }

        public static string[] SplitSpaces(this string text, int count = 1)
        {
            var separator = new string(SPACE, count);
            return text.Split(new[] { separator }, StringSplitOptions.RemoveEmptyEntries);
        }
    }
}
