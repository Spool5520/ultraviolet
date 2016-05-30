﻿using System;
using Newtonsoft.Json;

namespace TwistedLogik.Ultraviolet.Graphics.Graphics2D
{
    /// <summary>
    /// Describes a <see cref="SpriteFrame"/> object during deserialization.
    /// </summary>
    internal sealed class SpriteFrameDescription
    {
        /// <summary>
        /// Gets or sets the content resource path to the frame's texture atlas.
        /// </summary>
        [JsonProperty(PropertyName = "atlas", Required = Required.DisallowNull)]
        public String Atlas { get; set; }

        /// <summary>
        /// Gets or sets the name of the frame's texture atlas cell.
        /// </summary>
        [JsonProperty(PropertyName = "atlasCell", Required = Required.DisallowNull)]
        public String AtlasCell { get; set; }

        /// <summary>
        /// Gets or sets the content resource path to the frame's texture.
        /// </summary>
        [JsonProperty(PropertyName = "texture", Required = Required.DisallowNull)]
        public String Texture { get; set; }
        
        /// <summary>
        /// Gets or sets the frame's area on its texture.
        /// </summary>
        [JsonProperty(PropertyName = "area", Required = Required.DisallowNull)]
        public Rectangle? Area { get; set; }

        /// <summary>
        /// Gets or sets the frame's point of origin.
        /// </summary>
        [JsonProperty(PropertyName = "origin", Required = Required.DisallowNull)]
        public Point2? Origin { get; set; }

        /// <summary>
        /// Gets or sets the frame's duration in milliseconds.
        /// </summary>
        [JsonProperty(PropertyName = "duration", Required = Required.DisallowNull)]
        public Int32? Duration { get; set; }
    }
}
