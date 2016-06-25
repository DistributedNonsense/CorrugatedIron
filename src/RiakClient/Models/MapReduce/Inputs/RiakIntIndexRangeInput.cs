namespace RiakClient.Models.MapReduce.Inputs
{
    using System;
    using System.Numerics;
    using Newtonsoft.Json;

    /// <summary>
    /// Represents an integer secondary index range query mapreduce input.
    /// </summary>
    public class RiakIntIndexRangeInput : RiakIndexInput
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="RiakIntIndexRangeInput"/> class.
        /// </summary>
        /// <param name="bucket">The bucket that contains the <paramref name="index"/> to query.</param>
        /// <param name="index">
        /// The index to query. The output of that query will be used as input for the mapreduce job.
        /// </param>
        /// <param name="start">The inclusive lower bound of the integer range to query for.</param>
        /// <param name="end">The inclusive upper bound of the integer range to query for.</param>
        [Obsolete("Use the constructor that accepts a RiakIndexId instead. This will be removed in the next version.")]
        public RiakIntIndexRangeInput(string bucket, string index, BigInteger start, BigInteger end)
            : this(new RiakIndexId(bucket, index), start, end)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="RiakIntIndexRangeInput"/> class.
        /// </summary>
        /// <param name="indexId">
        /// The <see cref="RiakIndexId"/> that specifies which index to query.
        /// The output of that query will be used as input for the mapreduce job.
        /// </param>
        /// <param name="start">The inclusive lower bound of the integer range to query for.</param>
        /// <param name="end">The inclusive upper bound of the integer range to query for.</param>
        public RiakIntIndexRangeInput(RiakIndexId indexId, BigInteger start, BigInteger end)
            : base(indexId.ToIntIndexId())
        {
            Start = start;
            End = end;
        }

        // TODO: Make the properties immutable.

        /// <summary>
        /// The inclusive lower bound of the integer range to query for.
        /// </summary>
        public BigInteger Start { get; set; }

        /// <summary>
        /// The inclusive upper bound of the integer range to query for.
        /// </summary>
        public BigInteger End { get; set; }

        /// <inheritdoc/>
        public override JsonWriter WriteJson(JsonWriter writer)
        {
            WriteIndexHeaderJson(writer);

            writer.WritePropertyName("start");
            writer.WriteValue(Start.ToString());

            writer.WritePropertyName("end");
            writer.WriteValue(End.ToString());

            writer.WriteEndObject();

            return writer;
        }
    }
}
