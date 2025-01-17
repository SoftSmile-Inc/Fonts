// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace SixLabors.Fonts.Unicode
{
    /// <summary>
    /// Represents a Unicode value ([ U+0000..U+10FFFF ], inclusive).
    /// </summary>
    /// <remarks>
    /// This type's constructors and conversion operators validate the input, so consumers can call the APIs
    /// assuming that the underlying <see cref="CodePoint"/> instance is well-formed.
    /// </remarks>
    [DebuggerDisplay("{DebuggerDisplay,nq}")]
    internal readonly struct CodePoint : IComparable<CodePoint>, IEquatable<CodePoint>
    {
        // Supplementary plane code points are encoded as 2 UTF-16 code units
        private const int MaxUtf16CharsPerRune = 2;
        private const byte IsWhiteSpaceFlag = 0x80;
        private const byte UnicodeCategoryMask = 0x1F;

        private readonly uint value;

        /// <summary>
        /// Initializes a new instance of the <see cref="CodePoint"/> struct.
        /// </summary>
        /// <param name="value">The value to create the codepoint.</param>
        public CodePoint(int value)
            : this((uint)value)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="CodePoint"/> struct.
        /// </summary>
        /// <param name="value">The value to create the codepoint.</param>
        public CodePoint(uint value)
        {
            Guard.IsTrue(IsValid(value), nameof(value), "Must be in [ U+0000..U+10FFFF ], inclusive.");

            this.value = value;
            this.Value = (int)value;
        }

        // Contains information about the ASCII character range [ U+0000..U+007F ], with:
        // - 0x80 bit if set means 'is whitespace'
        // - 0x40 bit if set means 'is letter or digit'
        // - 0x20 bit is reserved for future use
        // - bottom 5 bits are the UnicodeCategory of the character
        private static ReadOnlySpan<byte> AsciiCharInfo => new byte[]
        {
            0x0E, 0x0E, 0x0E, 0x0E, 0x0E, 0x0E, 0x0E, 0x0E, 0x0E, 0x8E, 0x8E, 0x8E, 0x8E, 0x8E, 0x0E, 0x0E, // U+0000..U+000F
            0x0E, 0x0E, 0x0E, 0x0E, 0x0E, 0x0E, 0x0E, 0x0E, 0x0E, 0x0E, 0x0E, 0x0E, 0x0E, 0x0E, 0x0E, 0x0E, // U+0010..U+001F
            0x8B, 0x18, 0x18, 0x18, 0x1A, 0x18, 0x18, 0x18, 0x14, 0x15, 0x18, 0x19, 0x18, 0x13, 0x18, 0x18, // U+0020..U+002F
            0x48, 0x48, 0x48, 0x48, 0x48, 0x48, 0x48, 0x48, 0x48, 0x48, 0x18, 0x18, 0x19, 0x19, 0x19, 0x18, // U+0030..U+003F
            0x18, 0x40, 0x40, 0x40, 0x40, 0x40, 0x40, 0x40, 0x40, 0x40, 0x40, 0x40, 0x40, 0x40, 0x40, 0x40, // U+0040..U+004F
            0x40, 0x40, 0x40, 0x40, 0x40, 0x40, 0x40, 0x40, 0x40, 0x40, 0x40, 0x14, 0x18, 0x15, 0x1B, 0x12, // U+0050..U+005F
            0x1B, 0x41, 0x41, 0x41, 0x41, 0x41, 0x41, 0x41, 0x41, 0x41, 0x41, 0x41, 0x41, 0x41, 0x41, 0x41, // U+0060..U+006F
            0x41, 0x41, 0x41, 0x41, 0x41, 0x41, 0x41, 0x41, 0x41, 0x41, 0x41, 0x14, 0x19, 0x15, 0x19, 0x0E, // U+0070..U+007F
        };

        /// <summary>
        /// Gets the Unicode replacement character U+FFFD.
        /// </summary>
        public static CodePoint ReplacementCodePoint { get; } = new CodePoint(0xFFFD);

        /// <summary>
        /// Gets a value indicating whether this value is within the BMP ([ U+0000..U+FFFF ])
        /// and therefore representable by a single UTF-16 code unit.
        /// </summary>
        public bool IsBmp => UnicodeUtility.IsBmpCodePoint(this.value);

        /// <summary>
        /// Gets a value indicating whether this value is ASCII ([ U+0000..U+007F ])
        /// and therefore representable by a single UTF-8 code unit.
        /// </summary>
        public bool IsAscii => UnicodeUtility.IsAsciiCodePoint(this.value);

        // Displayed as "'<char>' (U+XXXX)"; e.g., "'e' (U+0065)"
        internal string DebuggerDisplay => FormattableString.Invariant($"U+{this.value:X4} '{(IsValid(this.value) ? this.ToString() : "\uFFFD")}'");

        /// <summary>
        /// Gets the Unicode value as an integer.
        /// </summary>
        public readonly int Value { get; }

        // Operators below are explicit because they may throw.
        public static explicit operator CodePoint(uint value) => new CodePoint(value);

        public static explicit operator CodePoint(int value) => new CodePoint(value);

        public static bool operator ==(CodePoint left, CodePoint right) => left.value == right.value;

        public static bool operator !=(CodePoint left, CodePoint right) => left.value != right.value;

        public static bool operator <(CodePoint left, CodePoint right) => left.value < right.value;

        public static bool operator <=(CodePoint left, CodePoint right) => left.value <= right.value;

        public static bool operator >(CodePoint left, CodePoint right) => left.value > right.value;

        public static bool operator >=(CodePoint left, CodePoint right) => left.value >= right.value;

        /// <summary>
        /// Returns <see langword="true"/> if <paramref name="value"/> is a valid Unicode code
        /// point, i.e., is in [ U+0000..U+10FFFF ], inclusive.
        /// </summary>
        public static bool IsValid(int value) => IsValid((uint)value);

        /// <summary>
        /// Returns <see langword="true"/> if <paramref name="value"/> is a valid Unicode code
        /// point, i.e., is in [ U+0000..U+10FFFF ], inclusive.
        /// </summary>
        public static bool IsValid(uint value) => UnicodeUtility.IsValidCodePoint(value);

        /// <summary>
        /// Gets a value indicating whether the given codepoint is white space.
        /// </summary>
        public static bool IsWhiteSpace(CodePoint codePoint)
        {
            if (codePoint.IsAscii)
            {
                return (AsciiCharInfo[codePoint.Value] & IsWhiteSpaceFlag) != 0;
            }

            // Only BMP code points can be white space, so only call into char
            // if the incoming value is within the BMP.
            return codePoint.IsBmp && char.IsWhiteSpace((char)codePoint.Value);
        }

        /// <summary>
        /// Gets a value indicating whether the given codepoint is a control.
        /// </summary>
        public static bool IsControl(CodePoint value) =>

            // Per the Unicode stability policy, the set of control characters
            // is forever fixed at [ U+0000..U+001F ], [ U+007F..U+009F ]. No
            // characters will ever be added to or removed from the "control characters"
            // group. See https://www.unicode.org/policies/stability_policy.html.
            //
            // Logic below depends on CodePoint.Value never being -1 (since CodePoint is a validating type)
            // 00..1F (+1) => 01..20 (&~80) => 01..20
            // 7F..9F (+1) => 80..A0 (&~80) => 00..20
            ((value.value + 1) & ~0x80u) <= 0x20u;

        /// <summary>
        /// Gets a value indicating whether the given codepoint is a new line indicator.
        /// </summary>
        public static bool IsNewLine(CodePoint value)
        {
            // See https://www.unicode.org/standard/reports/tr13/tr13-5.html
            switch (value.Value)
            {
                case 0x000A: // LINE FEED (LF)
                case 0x000B: // LINE TABULATION (VT)
                case 0x000C: // FORM FEED (FF)
                case 0x000D: // CARRIAGE RETURN (CR)
                case 0x0085: // NEXT LINE (NEL)
                case 0x2028: // LINE SEPARATOR (LS)
                case 0x2029: // PARAGRAPH SEPARATOR (PS)
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// Returns the number of codepoints in a given string buffer.
        /// </summary>
        /// <param name="source">The source buffer to parse.</param>
        /// <returns>The <see cref="int"/>.</returns>
        public static int GetCodePointCount(ReadOnlySpan<char> source)
        {
            if (source.IsEmpty)
            {
                return 0;
            }

            unsafe
            {
                fixed (char* c = source)
                {
                    return Encoding.UTF32.GetByteCount(c, source.Length) / sizeof(uint);
                }
            }
        }

        /// <summary>
        /// Gets the <see cref="BidiType"/> for the given codepoint.
        /// </summary>
        public static BidiType GetBidiType(CodePoint codePoint)
            => new BidiType(codePoint);

        /// <summary>
        /// Gets the <see cref="LineBreakClass"/> for the given codepoint.
        /// </summary>
        public static LineBreakClass GetLineBreakClass(CodePoint codePoint)
            => UnicodeData.GetLineBreakClass(codePoint.Value);

        /// <summary>
        /// Gets the <see cref="GraphemeClusterClass"/> for the given codepoint.
        /// </summary>
        public static GraphemeClusterClass GetGraphemeClusterClass(CodePoint codePoint)
            => UnicodeData.GetGraphemeClusterClass(codePoint.Value);

        /// <summary>
        /// Gets the <see cref="UnicodeCategory"/> for the given codepoint.
        /// </summary>
        public static UnicodeCategory GetGeneralCategory(CodePoint codePoint)
        {
            if (codePoint.IsAscii)
            {
                return (UnicodeCategory)(AsciiCharInfo[codePoint.Value] & UnicodeCategoryMask);
            }

            return UnicodeData.GetUnicodeCategory(codePoint.Value);
        }

        /// <summary>
        /// Reads the <see cref="CodePoint"/> at specified position.
        /// </summary>
        /// <param name="text">The text to read from.</param>
        /// <param name="index">The index to read at.</param>
        /// <returns>The <see cref="CodePoint"/>.</returns>
        public static CodePoint ReadAt(string text, int index)
            => ReadAt(text, index, out int _);

        /// <summary>
        /// Reads the <see cref="CodePoint"/> at specified position.
        /// </summary>
        /// <param name="text">The text to read from.</param>
        /// <param name="index">The index to read at.</param>
        /// <param name="charsConsumed">The count of chars consumed reading the buffer.</param>
        /// <returns>The <see cref="CodePoint"/>.</returns>
        public static CodePoint ReadAt(string text, int index, out int charsConsumed)
            => DecodeFromUtf16At(text.AsMemory().Span, index, out charsConsumed);

        /// <summary>
        /// Decodes the <see cref="CodePoint"/> from the provided UTF-16 source buffer at the specified position.
        /// </summary>
        /// <param name="source">The buffer to read from.</param>
        /// <param name="index">The index to read at.</param>
        /// <returns>The <see cref="CodePoint"/>.</returns>
        public static CodePoint DecodeFromUtf16At(ReadOnlySpan<char> source, int index)
            => DecodeFromUtf16At(source, index, out int _);

        /// <summary>
        /// Decodes the <see cref="CodePoint"/> from the provided UTF-16 source buffer at the specified position.
        /// </summary>
        /// <param name="source">The buffer to read from.</param>
        /// <param name="index">The index to read at.</param>
        /// <param name="charsConsumed">The count of chars consumed reading the buffer.</param>
        /// <returns>The <see cref="CodePoint"/>.</returns>
        public static CodePoint DecodeFromUtf16At(ReadOnlySpan<char> source, int index, out int charsConsumed)
        {
            if (index >= source.Length)
            {
                charsConsumed = 0;
                return ReplacementCodePoint;
            }

            // Optimistically assume input is within BMP.
            charsConsumed = 1;
            uint code = source[index];

            // High surrogate
            if (UnicodeUtility.IsHighSurrogateCodePoint(code))
            {
                uint hi, low;

                hi = code;
                index++;

                if (index == source.Length)
                {
                    return ReplacementCodePoint;
                }

                low = source[index];

                if (UnicodeUtility.IsLowSurrogateCodePoint(low))
                {
                    charsConsumed = 2;
                    return new CodePoint(UnicodeUtility.GetScalarFromUtf16SurrogatePair(hi, low));
                }

                return ReplacementCodePoint;
            }

            return new CodePoint(code);
        }

        /// <summary>
        /// Decodes the <see cref="CodePoint"/> at the end of the provided UTF-16 source buffer.
        /// </summary>
        /// <remarks>
        /// This method is very similar to <see cref="ReadAt(string, int, out int)"/>, but it allows
        /// the caller to loop backward instead of forward. The typical calling convention is that on each iteration
        /// of the loop, the caller should slice off the final <paramref name="charsConsumed"/> elements of
        /// the <paramref name="source"/> buffer.
        /// </remarks>
        /// <param name="source">The buffer to read from.</param>
        /// <param name="charsConsumed">The count of chars consumed reading the buffer.</param>
        /// <returns>The <see cref="CodePoint"/>.</returns>
        public static CodePoint DecodeLastFromUtf16(ReadOnlySpan<char> source, out int charsConsumed)
        {
            int index = source.Length - 1;
            if (index < 0)
            {
                charsConsumed = 0;
                return ReplacementCodePoint;
            }

            // Optimistically assume input is within BMP.
            charsConsumed = 1;
            uint code = source[index];

            // Low surrogate
            if (UnicodeUtility.IsLowSurrogateCodePoint(code))
            {
                if (index == 0)
                {
                    return ReplacementCodePoint;
                }

                uint hi = source[index - 1];
                uint low = code;

                if (UnicodeUtility.IsHighSurrogateCodePoint(hi))
                {
                    charsConsumed = 2;
                    return new CodePoint(UnicodeUtility.GetScalarFromUtf16SurrogatePair(hi, low));
                }

                return ReplacementCodePoint;
            }

            return new CodePoint(code);
        }

        /// <inheritdoc/>
        public int CompareTo(CodePoint other)

            // Values don't span entire 32-bit domain so won't integer overflow.
            => this.Value - other.Value;

        /// <inheritdoc/>
        public override bool Equals(object? obj) => obj is CodePoint point && this.Equals(point);

        /// <inheritdoc/>
        public bool Equals(CodePoint other) => this.value == other.value;

        /// <inheritdoc/>
        public override int GetHashCode() => HashCode.Combine(this.value);

        /// <inheritdoc/>
        public override string ToString()
        {
            if (this.IsBmp)
            {
                return ((char)this.value).ToString();
            }
            else
            {
                Span<char> buffer = stackalloc char[MaxUtf16CharsPerRune];
                UnicodeUtility.GetUtf16SurrogatesFromSupplementaryPlaneCodePoint(this.value, out buffer[0], out buffer[1]);
                return buffer.ToString();
            }
        }
    }
}
