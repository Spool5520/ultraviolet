using System;

namespace Ultraviolet.Core.Text
{
    partial struct StringPtr16
    {
        /// <inheritdoc/>
        public override Int32 GetHashCode()
        {
            unchecked
            {
                var hash = 17;
                hash = hash * 23 + ptr.GetHashCode();
                hash = hash * 23 + length.GetHashCode();
                return hash;
            }
        }
        
        /// <summary>
        /// Compares two objects to determine whether they are equal.
        /// </summary>
        /// <param name="v1">The first value to compare.</param>
        /// <param name="v2">The second value to compare.</param>
        /// <returns><see langword="true"/> if the two values are equal; otherwise, <see langword="false"/>.</returns>
        [Preserve]
        public static Boolean operator ==(StringPtr16 v1, StringPtr16 v2)
        {
            return v1.Equals(v2);
        }
        
        /// <summary>
        /// Compares two objects to determine whether they are unequal.
        /// </summary>
        /// <param name="v1">The first value to compare.</param>
        /// <param name="v2">The second value to compare.</param>
        /// <returns><see langword="true"/> if the two values are unequal; otherwise, <see langword="false"/>.</returns>
        [Preserve]
        public static Boolean operator !=(StringPtr16 v1, StringPtr16 v2)
        {
            return !v1.Equals(v2);
        }
        
        /// <inheritdoc/>
        [Preserve]
        public override Boolean Equals(Object other)
        {
            return (other is StringPtr16 x) ? Equals(x) : false;
        }
        
        /// <inheritdoc/>
        [Preserve]
        public Boolean Equals(StringPtr16 other)
        {
            return
                this.ptr == other.ptr &&
                this.length == other.length;
        }
    }
}
