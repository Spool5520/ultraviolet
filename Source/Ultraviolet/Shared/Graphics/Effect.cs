﻿using System;
using Ultraviolet.Core;

namespace Ultraviolet.Graphics
{
    /// <summary>
    /// Represents a shader effect.
    /// </summary>
    public class Effect : UltravioletResource
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Effect"/> class.
        /// </summary>
        /// <param name="impl">The <see cref="EffectImplementation"/> that implements this effect.</param>
        protected Effect(EffectImplementation impl)
            : base(impl.Ultraviolet)
        {
            Contract.Require(impl, nameof(impl));

            this.impl = impl;
        }

        /// <summary>
        /// Creates a new instance of the <see cref="Effect"/> class.
        /// </summary>
        /// <param name="impl">The effect implementation that the effect encapsulates.</param>
        /// <returns>The instance of <see cref="Effect"/> that was created.</returns>
        public static Effect Create(EffectImplementation impl)
        {
            Contract.Require(impl, nameof(impl));

            return new Effect(impl);
        }

        /// <summary>
        /// Gets the effect's collection of parameters.
        /// </summary>
        public EffectParameterCollection Parameters => impl.Parameters;

        /// <summary>
        /// Gets the effect's collection of techniques.
        /// </summary>
        public EffectTechniqueCollection Techniques => impl.Techniques;

        /// <summary>
        /// Gets or sets the effect's current technique.
        /// </summary>
        public EffectTechnique CurrentTechnique
        {
            get => impl.CurrentTechnique; 
            set => impl.CurrentTechnique = value; 
        }

        /// <summary>
        /// Releases resources associated with the object.
        /// </summary>
        /// <param name="disposing"><see langword="true"/> if the object is being disposed; <see langword="false"/> if the object is being finalized.</param>
        protected override void Dispose(Boolean disposing)
        {
            if (Disposed)
                return;

            if (disposing)
            {
                impl.Dispose();
            }

            base.Dispose(disposing);
        }

        // The effect's implementation.
        private readonly EffectImplementation impl;
    }
}
