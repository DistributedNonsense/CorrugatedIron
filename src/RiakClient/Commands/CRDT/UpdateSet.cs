﻿namespace RiakClient.Commands.CRDT
{
    using System.Collections.Generic;
    using Extensions;
    using Messages;
    using Util;

    /// <summary>
    /// Command used to update a Set in Riak. As a convenience, a builder method
    /// is provided as well as an object with a fluent API for constructing the
    /// update.
    /// See <see cref="UpdateSet.Builder"/>
    /// <code>
    /// var update = new UpdateSet.Builder()
    ///           .WithBucketType("maps")
    ///           .WithBucket("myBucket")
    ///           .WithKey("map_1")
    ///           .WithReturnBody(true)
    ///           .Build();
    /// </code>
    /// </summary>
    public class UpdateSet : UpdateSetBase
    {
        private readonly UpdateSetOptions setOptions;

        /// <summary>
        /// Initializes a new instance of the <see cref="UpdateSet"/> class.
        /// </summary>
        /// <param name="options">Options for this operation. See <see cref="UpdateSetOptions"/></param>
        /// <inheritdoc />
        public UpdateSet(UpdateSetOptions options)
            : base(options)
        {
            setOptions = options;
        }

        protected override DtOp GetRequestOp()
        {
            var op = new DtOp();
            op.set_op = new SetOp();

            if (EnumerableUtil.NotNullOrEmpty(setOptions.Additions))
            {
                op.set_op.adds.AddRange(setOptions.Additions);
            }

            if (EnumerableUtil.NotNullOrEmpty(setOptions.Removals))
            {
                op.set_op.removes.AddRange(setOptions.Removals);
            }

            return op;
        }

        public class Builder
            : UpdateCommandBuilder<Builder, UpdateSet, UpdateSetOptions, SetResponse>
        {
            private ISet<byte[]> additions;
            private ISet<byte[]> removals;

            public Builder()
            {
            }

            public Builder(ISet<byte[]> additions, ISet<byte[]> removals)
            {
                this.additions = additions;
                this.removals = removals;
            }

            public Builder(ISet<string> additions, ISet<string> removals)
            {
                this.additions = additions.GetUTF8Bytes();
                this.removals = removals.GetUTF8Bytes();
            }

            public Builder(ISet<byte[]> additions, ISet<byte[]> removals, Builder source)
                : base(source)
            {
                this.additions = additions;
                this.removals = removals;
            }

            public Builder(ISet<string> additions, ISet<string> removals, Builder source)
                : base(source)
            {
                this.additions = additions.GetUTF8Bytes();
                this.removals = removals.GetUTF8Bytes();
            }

            public Builder WithAdditions(ISet<byte[]> additions)
            {
                this.additions = additions;
                return this;
            }

            public Builder WithAdditions(ISet<string> additions)
            {
                this.additions = additions.GetUTF8Bytes();
                return this;
            }

            public Builder WithRemovals(ISet<byte[]> removals)
            {
                this.removals = removals;
                return this;
            }

            public Builder WithRemovals(ISet<string> removals)
            {
                this.removals = removals.GetUTF8Bytes();
                return this;
            }

            protected override void PopulateOptions(UpdateSetOptions options)
            {
                options.Additions = additions;
                options.Removals = removals;
            }
        }
    }
}