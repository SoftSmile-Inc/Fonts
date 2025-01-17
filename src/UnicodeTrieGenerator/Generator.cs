// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using SixLabors.Fonts.Unicode;

namespace UnicodeTrieGenerator
{
    /// <summary>
    /// Provides methods to generate Unicode Tries.
    /// Ported from <see href="https://github.com/toptensoftware/RichTextKit/blob/master/BuildUnicodeData/generate.js"/>.
    /// </summary>
    public static class Generator
    {
        private static readonly Dictionary<string, UnicodeCategory> UnicodeCategoryMap
            = new Dictionary<string, UnicodeCategory>(StringComparer.OrdinalIgnoreCase)
        {
            { "Lu", UnicodeCategory.UppercaseLetter },
            { "Ll", UnicodeCategory.LowercaseLetter },
            { "Lt", UnicodeCategory.TitlecaseLetter },
            { "Lm", UnicodeCategory.ModifierLetter },
            { "Lo", UnicodeCategory.OtherLetter },
            { "Mn", UnicodeCategory.NonSpacingMark },
            { "Mc", UnicodeCategory.SpacingCombiningMark },
            { "Me", UnicodeCategory.EnclosingMark },
            { "Nd", UnicodeCategory.DecimalDigitNumber },
            { "Nl", UnicodeCategory.LetterNumber },
            { "No", UnicodeCategory.OtherNumber },
            { "Zs", UnicodeCategory.SpaceSeparator },
            { "Zl", UnicodeCategory.LineSeparator },
            { "Zp", UnicodeCategory.ParagraphSeparator },
            { "Cc", UnicodeCategory.Control },
            { "Cf", UnicodeCategory.Format },
            { "Cs", UnicodeCategory.Surrogate },
            { "Co", UnicodeCategory.PrivateUse },
            { "Pc", UnicodeCategory.ConnectorPunctuation },
            { "Pd", UnicodeCategory.DashPunctuation },
            { "Ps", UnicodeCategory.OpenPunctuation },
            { "Pe", UnicodeCategory.ClosePunctuation },
            { "Pi", UnicodeCategory.InitialQuotePunctuation },
            { "Pf", UnicodeCategory.FinalQuotePunctuation },
            { "Po", UnicodeCategory.OtherPunctuation },
            { "Sm", UnicodeCategory.MathSymbol },
            { "Sc", UnicodeCategory.CurrencySymbol },
            { "Sk", UnicodeCategory.ModifierSymbol },
            { "So", UnicodeCategory.OtherSymbol },
            { "Cn", UnicodeCategory.OtherNotAssigned }
        };

        private const string SixLaborsSolutionFileName = "SixLabors.Fonts.sln";
        private const string InputRulesRelativePath = @"src\UnicodeTrieGenerator\Rules";
        private const string OutputResourcesRelativePath = @"src\SixLabors.Fonts\Unicode\Resources";

        private static readonly Lazy<string> SolutionDirectoryFullPathLazy = new Lazy<string>(GetSolutionDirectoryFullPathImpl);
        private static readonly Dictionary<int, int> Bidi = new Dictionary<int, int>();

        private static string SolutionDirectoryFullPath => SolutionDirectoryFullPathLazy.Value;

        /// <summary>
        /// Generates the various Unicode tries.
        /// </summary>
        public static void GenerateUnicodeTries()
        {
            ProcessUnicodeData();
            GenerateBidiBracketsTrie();
            GenerateLineBreakTrie();
            GenerateUnicodeCategoryTrie();
            GenerateGraphemeBreakTrie();
        }

        private static void ProcessUnicodeData()
        {
            using StreamReader sr = GetStreamReader("UnicodeData.txt");
            string line;
            while ((line = sr.ReadLine()) != null)
            {
                var parts = line.Split(';');

                if (parts.Length > 4)
                {
                    // Get the directionality.
                    int codePoint = ParseHexInt(parts[0]);
                    BidiCharacterType cls = Enum.Parse<BidiCharacterType>(parts[4]);
                    Bidi[codePoint] = (int)cls << 24;
                }
            }
        }

        /// <summary>
        /// Generates the UnicodeTrie for the Grapheme cluster breaks code points.
        /// </summary>
        public static void GenerateGraphemeBreakTrie()
        {
            var regex = new Regex(@"^([0-9A-F]+)(?:\.\.([0-9A-F]+))?\s*;\s*(.*?)\s*#");
            var builder = new UnicodeTrieBuilder((uint)GraphemeClusterClass.Any);

            using (StreamReader sr = GetStreamReader("GraphemeBreakProperty.txt"))
            {
                string line;
                while ((line = sr.ReadLine()) != null)
                {
                    Match match = regex.Match(line);

                    if (match.Success)
                    {
                        var start = match.Groups[1].Value;
                        var end = match.Groups[2].Value;
                        var point = match.Groups[3].Value;

                        if (end?.Length == 0)
                        {
                            end = start;
                        }

                        builder.SetRange(ParseHexInt(start), ParseHexInt(end), (uint)Enum.Parse<GraphemeClusterClass>(point), true);
                    }
                }
            }

            using (StreamReader sr = GetStreamReader("emoji-data.txt"))
            {
                string line;
                while ((line = sr.ReadLine()) != null)
                {
                    Match match = regex.Match(line);

                    if (match.Success)
                    {
                        var start = match.Groups[1].Value;
                        var end = match.Groups[2].Value;
                        var prop = match.Groups[3].Value;

                        if (end?.Length == 0)
                        {
                            end = start;
                        }

                        if (prop == "Extended_Pictographic")
                        {
                            builder.SetRange(ParseHexInt(start), ParseHexInt(end), (uint)GraphemeClusterClass.ExtPict, true);
                        }
                    }
                }
            }

            UnicodeTrie trie = builder.Freeze();

            using FileStream stream = GetStreamWriter("Grapheme.trie");
            trie.Save(stream);
        }

        /// <summary>
        /// Generates the UnicodeTrie for the Bidi Brackets code points.
        /// </summary>
        private static void GenerateBidiBracketsTrie()
        {
            var regex = new Regex(@"^([0-9A-F]+);\s([0-9A-F]+);\s([ocn])");
            var builder = new UnicodeTrieBuilder(0u);

            using (StreamReader sr = GetStreamReader("BidiBrackets.txt"))
            {
                string line;
                while ((line = sr.ReadLine()) != null)
                {
                    Match match = regex.Match(line);
                    if (match.Success)
                    {
                        int point = ParseHexInt(match.Groups[1].Value);
                        int otherPoint = ParseHexInt(match.Groups[2].Value);
                        BidiPairedBracketType kind = Enum.Parse<BidiPairedBracketType>(match.Groups[3].Value, true);

                        Bidi[point] |= otherPoint | ((int)kind << 16);
                    }
                }
            }

            foreach (KeyValuePair<int, int> item in Bidi)
            {
                if (item.Value != 0)
                {
                    builder.Set(item.Key, (uint)item.Value);
                }
            }

            UnicodeTrie trie = builder.Freeze();

            using FileStream stream = GetStreamWriter("Bidi.trie");
            trie.Save(stream);
        }

        /// <summary>
        /// Generates the UnicodeTrie for the LineBreak code point ranges.
        /// </summary>
        private static void GenerateLineBreakTrie()
        {
            var regex = new Regex(@"^([0-9A-F]+)(?:\.\.([0-9A-F]+))?\s*;\s*(.*?)\s*#");
            var builder = new UnicodeTrieBuilder((uint)LineBreakClass.XX);

            using (StreamReader sr = GetStreamReader("LineBreak.txt"))
            {
                string line;
                while ((line = sr.ReadLine()) != null)
                {
                    Match match = regex.Match(line);

                    if (match.Success)
                    {
                        var start = match.Groups[1].Value;
                        var end = match.Groups[2].Value;
                        var point = match.Groups[3].Value;

                        if (end?.Length == 0)
                        {
                            end = start;
                        }

                        builder.SetRange(ParseHexInt(start), ParseHexInt(end), (uint)Enum.Parse<LineBreakClass>(point), true);
                    }
                }
            }

            UnicodeTrie trie = builder.Freeze();

            using FileStream stream = GetStreamWriter("LineBreak.trie");
            trie.Save(stream);
        }

        /// <summary>
        /// Generates the UnicodeTrie for the general category code point ranges.
        /// </summary>
        private static void GenerateUnicodeCategoryTrie()
        {
            var regex = new Regex(@"^([0-9A-F]+)(?:\.\.([0-9A-F]+))?\s*;\s*(.*?)\s*#");
            var builder = new UnicodeTrieBuilder((uint)UnicodeCategory.OtherNotAssigned);

            using (StreamReader sr = GetStreamReader("DerivedGeneralCategory.txt"))
            {
                string line;
                while ((line = sr.ReadLine()) != null)
                {
                    Match match = regex.Match(line);

                    if (match.Success)
                    {
                        var start = match.Groups[1].Value;
                        var end = match.Groups[2].Value;
                        var point = match.Groups[3].Value;

                        if (end?.Length == 0)
                        {
                            end = start;
                        }

                        builder.SetRange(ParseHexInt(start), ParseHexInt(end), (uint)UnicodeCategoryMap[point], true);
                    }
                }
            }

            UnicodeTrie trie = builder.Freeze();

            using FileStream stream = GetStreamWriter("UnicodeCategory.trie");
            trie.Save(stream);
        }

        private static StreamReader GetStreamReader(string path)
        {
            var filename = GetFullPath(Path.Combine(InputRulesRelativePath, path));
            return File.OpenText(filename);
        }

        private static FileStream GetStreamWriter(string path)
        {
            var filename = GetFullPath(Path.Combine(OutputResourcesRelativePath, path));
            return File.OpenWrite(filename);
        }

        private static string GetSolutionDirectoryFullPathImpl()
        {
            string assemblyLocation = typeof(Generator).Assembly.Location;

            var assemblyFile = new FileInfo(assemblyLocation);

            DirectoryInfo directory = assemblyFile.Directory;

            while (!directory.EnumerateFiles(SixLaborsSolutionFileName).Any())
            {
                try
                {
                    directory = directory.Parent;
                }
                catch (Exception ex)
                {
                    throw new Exception(
                        $"Unable to find SixLabors solution directory from {assemblyLocation} because of {ex.GetType().Name}!",
                        ex);
                }

                if (directory == null)
                {
                    throw new Exception($"Unable to find SixLabors solution directory from {assemblyLocation}!");
                }
            }

            return directory.FullName;
        }

        private static int ParseHexInt(string value)
            => int.Parse(value, NumberStyles.HexNumber);

        private static string GetFullPath(string relativePath)
            => Path.Combine(SolutionDirectoryFullPath, relativePath).Replace('\\', Path.DirectorySeparatorChar);
    }
}
