using System.Text;
using System.Text.RegularExpressions;

namespace BMIRussian_ru.Services
{
    /// <summary>
    /// Generates URL-safe SEO IDs from titles: transliterates Russian to Latin,
    /// replaces spaces and special characters with "-", returns UPPERCASE.
    /// </summary>
    public static class SeoIdHelper
    {
        private static readonly IReadOnlyDictionary<char, string> RussianToLatin = new Dictionary<char, string>
        {
            ['а'] = "a", ['б'] = "b", ['в'] = "v", ['г'] = "g", ['д'] = "d", ['е'] = "e", ['ё'] = "e",
            ['ж'] = "zh", ['з'] = "z", ['и'] = "i", ['й'] = "y", ['к'] = "k", ['л'] = "l", ['м'] = "m",
            ['н'] = "n", ['о'] = "o", ['п'] = "p", ['р'] = "r", ['с'] = "s", ['т'] = "t", ['у'] = "u",
            ['ф'] = "f", ['х'] = "kh", ['ц'] = "ts", ['ч'] = "ch", ['ш'] = "sh", ['щ'] = "shch",
            ['ъ'] = "", ['ы'] = "y", ['ь'] = "", ['э'] = "e", ['ю'] = "yu", ['я'] = "ya",
            ['А'] = "A", ['Б'] = "B", ['В'] = "V", ['Г'] = "G", ['Д'] = "D", ['Е'] = "E", ['Ё'] = "E",
            ['Ж'] = "Zh", ['З'] = "Z", ['И'] = "I", ['Й'] = "Y", ['К'] = "K", ['Л'] = "L", ['М'] = "M",
            ['Н'] = "N", ['О'] = "O", ['П'] = "P", ['Р'] = "R", ['С'] = "S", ['Т'] = "T", ['У'] = "U",
            ['Ф'] = "F", ['Х'] = "Kh", ['Ц'] = "Ts", ['Ч'] = "Ch", ['Ш'] = "Sh", ['Щ'] = "Shch",
            ['Ъ'] = "", ['Ы'] = "Y", ['Ь'] = "", ['Э'] = "E", ['Ю'] = "Yu", ['Я'] = "Ya"
        };

        /// <summary>
        /// Produces a normalized SEO ID from the title: transliteration, spaces/special → "-", UPPERCASE.
        /// </summary>
        public static string FromTitle(string? title)
        {
            if (string.IsNullOrWhiteSpace(title))
                return string.Empty;

            var sb = new StringBuilder(title.Length * 2);
            foreach (var c in title.Trim())
            {
                if (RussianToLatin.TryGetValue(c, out var replacement))
                    sb.Append(replacement);
                else if (char.IsLetterOrDigit(c))
                    sb.Append(c);
                else if (char.IsWhiteSpace(c) || c == '-' || c == '_')
                    sb.Append('-');
                // other special symbols are skipped (replaced by nothing here, we'll collapse dashes later)
            }

            var result = Regex.Replace(sb.ToString(), @"-+", "-").Trim('-');
            return result.ToUpperInvariant();
        }
    }
}
