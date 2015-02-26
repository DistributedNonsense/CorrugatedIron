﻿// <copyright file="NVal.cs" company="Basho Technologies, Inc.">
// Copyright (c) 2011 - OJ Reeves & Jeremiah Peschka
// Copyright (c) 2014 - Basho Technologies, Inc.
//
// This file is provided to you under the Apache License,
// Version 2.0 (the "License"); you may not use this file
// except in compliance with the License.  You may obtain
// a copy of the License at
//
//   http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing,
// software distributed under the License is distributed on an
// "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY
// KIND, either express or implied.  See the License for the
// specific language governing permissions and limitations
// under the License.
// </copyright>

namespace RiakClient.Models
{
    using System;

    /// <summary>
    /// Represents the positive NVal values for Riak operation Quorum options.
    /// </summary>
    public class NVal : IEquatable<NVal>
    {
        private readonly uint nval = 0;

        /// <summary>
        /// Initializes a new instance of the <see cref="NVal"/> class.
        /// </summary>
        /// <param name="nval">The positive int value to use for quorum option.</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="nval"/> must be greater than zero.</exception>
        public NVal(int nval)
        {
            if (nval <= 0)
            {
                throw new ArgumentOutOfRangeException("nval must be greater than zero");
            }

            this.nval = (uint)nval;
        }

        internal NVal(uint nval)
        {
            this.nval = nval;
        }

        /// <summary>
        /// Cast the value of this <see cref="UInt32"/> to a <see cref="NVal"/>.
        /// </summary>
        /// <param name="nval">The <see cref="UInt32"/> value to cast to a <see cref="NVal"/>.</param>
        /// <returns>A <see cref="NVal"/> based on the value of the this <see cref="Int32"/>.</returns>
        public static explicit operator NVal(int nval)
        {
            return new NVal(nval);
        }

        /// <summary>
        /// Cast the value of this <see cref="NVal"/> to an <see cref="Int32"/>.
        /// </summary>
        /// <param name="nval">The <see cref="NVal"/> to cast to an <see cref="Int32"/>.</param>
        /// <returns>An <see cref="Int32"/> based on the value of this <see cref="NVal"/>.</returns>
        public static explicit operator int(NVal nval)
        {
            return (int)nval.nval;
        }

        /// <summary>
        /// Cast the value of this <see cref="NVal"/> to an <see cref="UInt32"/>.
        /// </summary>
        /// <param name="nval">The <see cref="NVal"/> to cast to a <see cref="UInt32"/>.</param>
        /// <returns>A <see cref="UInt32"/> based on the value of this <see cref="NVal"/>.</returns>
        [CLSCompliant(false)]
        public static implicit operator uint(NVal nval)
        {
            return nval.nval;
        }

        /// <summary>
        /// Determines whether the specified object is equal to the current object.
        /// </summary>
        /// <param name="obj">The object to compare with the current object.</param>
        /// <returns><b>true</b> if the specified object is equal to the current object, otherwise, <b>false</b>.</returns>
        public override bool Equals(object obj)
        {
            return Equals(obj as NVal);
        }

        /// <summary>
        /// Determines whether the specified object is equal to the current object.
        /// </summary>
        /// <param name="other">The object to compare with the current object.</param>
        /// <returns><b>true</b> if the specified object is equal to the current object, otherwise, <b>false</b>.</returns>
        public bool Equals(NVal other)
        {
            if (object.ReferenceEquals(other, null))
            {
                return false;
            }

            if (object.ReferenceEquals(other, this))
            {
                return true;
            }

            return this.GetHashCode() == other.GetHashCode();
        }

        /// <summary>
        /// Returns a hash code for the current object.
        /// Uses the HashCode of the internal <see cref="UInt32"/> nval value.
        /// </summary>
        /// <returns>A hash code for the current object.</returns>
        public override int GetHashCode()
        {
            return nval.GetHashCode();
        }
    }
}