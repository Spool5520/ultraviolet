﻿using System;
using System.Collections.Generic;
using System.Linq;

namespace TwistedLogik.Ultraviolet.Graphics.Graphics2D
{
    /// <summary>
    /// Represents a <see cref="SpriteFont"/> instance's internal index of glyph source rectangles.
    /// </summary>
    internal sealed class SpriteFontGlyphIndex
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SpriteFontGlyphIndex"/> class.
        /// </summary>
        /// <param name="regions">A collection containing the font face's character regions.</param>
        /// <param name="glyphs">A collection containing the positions of the font face's glyphs on its texture.</param>
        /// <param name="firstCharacter">The character that corresponds to font face's first glyph.</param>
        /// <param name="substitutionCharacter">The character that corresponds to the font face's substitution glyph.</param>
        public SpriteFontGlyphIndex(IEnumerable<CharacterRegion> regions, IEnumerable<Rectangle> glyphs, Char firstCharacter, Char substitutionCharacter)
        {
            this.glyphs = glyphs.ToArray();
            this.firstCharacter = firstCharacter;
            this.substitutionCharacter = substitutionCharacter;
            this.lineSpacing = glyphs.Max(x => x.Height);

            var isSubstitutionCharMissing = true;
            var isNonBreakingSpaceRequired = true;

            foreach (var region in regions)
            {
                for (var character = region.Start; character <= region.End; character++)
                {
                    if (character == substitutionCharacter)
                        isSubstitutionCharMissing = false;

                    if (character == '\u00A0')
                        isNonBreakingSpaceRequired = false;

                    var isExtendedAscii = (character < SpriteFont.ExtendedAsciiCount);
                    if (isExtendedAscii)
                    {
                        if (ascii == null)
                            ascii = new Int32[SpriteFont.ExtendedAsciiCount];

                        ascii[character] = count;
                    }
                    else
                    {
                        if (unicode == null)
                            unicode = new Dictionary<Char, Int32>();

                        unicode[character] = count;
                    }

                    count++;
                }
            }

            if (isSubstitutionCharMissing)
                throw new ArgumentOutOfRangeException("substitutionCharacter");

            if (isNonBreakingSpaceRequired)
            {
                if (ascii == null)
                    ascii = new Int32[SpriteFont.ExtendedAsciiCount];

                ascii['\u00A0'] = ascii[' '];
                count++;
            }
        }

        /// <summary>
        /// Gets the position of the specified glyph on the font face's texture.
        /// </summary>
        /// <param name="character">The character for which to retrieve glyph position information.</param>
        /// <returns>The position of the specified glyph on the font face's texture.</returns>
        public Rectangle this[Char character]
        {
            get
            {
                if (character < SpriteFont.ExtendedAsciiCount)
                    return glyphs[ascii[character]];

                Int32 index;
                if (unicode.TryGetValue(character, out index))
                    return glyphs[index];

                return glyphs[unicode[substitutionCharacter]];
            }
        }

        /// <summary>
        /// Gets the first character in the index.
        /// </summary>
        public Char FirstCharacter
        {
            get { return firstCharacter; }
        }

        /// <summary>
        /// Gets the character which is substituted for missing characters.
        /// </summary>
        public Char SubstitutionCharacter
        {
            get { return substitutionCharacter; }
        }

        /// <summary>
        /// Gets the height of a line of text.
        /// </summary>
        public Int32 LineSpacing
        {
            get { return lineSpacing; }
        }

        /// <summary>
        /// Gets the number of glyphs in the index.
        /// </summary>
        public Int32 Count
        {
            get { return count; }
        }

        // Property values.
        private readonly Char firstCharacter;
        private readonly Char substitutionCharacter;
        private readonly Int32 lineSpacing;
        private readonly Int32 count;

        // State values.
        private readonly Rectangle[] glyphs;
        private readonly Int32[] ascii;
        private readonly Dictionary<Char, Int32> unicode;
    }
}
