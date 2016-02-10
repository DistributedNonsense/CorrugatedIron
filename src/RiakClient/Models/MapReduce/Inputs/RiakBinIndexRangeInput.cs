namespace RiakClient.Models.MapReduce.Inputs
{
    using System;
    using Newtonsoft.Json;

    /// <summary>
    /// Represents a string secondary index range query mapreduce input.
    /// </summary>
    public class RiakBinIndexRangeInput : RiakIndexInput
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="RiakBinIndexRangeInput"/> class.
        /// </summary>
        /// <param name="bucket">The bucket that contains the <paramref name="index"/> to query.</param>
        /// <param name="index">
        /// The index to query. The output of that query will be used as input for the mapreduce job.
        /// </param>
        /// <param name="start">The inclusive lower bound of the string range to query for.</param>
        /// <param name="end">The inclusive upper bound of the string range to query for.</param>
        [Obsolete("Use the constructor that accepts a RiakIndexId instead. This will be removed in the next version.")]
        public RiakBinIndexRangeInput(string bucket, string index, string start, string end)
            : this(new RiakIndexId(bucket, index), start, end)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="RiakBinIndexRangeInput"/> class.
        /// </summary>
        /// <param name="indexId">
        /// The <see cref="RiakIndexId"/> that specifies which index to query.
        /// The output of that query will be used as input for the mapreduce job.
        /// </param>
        /// <param name="start">The inclusive lower bound of the string range to query for.</param>
        /// <param name="end">The inclusive upper bound of the string range to query for.</param>
        public RiakBinIndexRangeInput(RiakIndexId indexId, string start, string end)
            : base(indexId.ToBinIndexId())
        {
            Start = start;
            End = end;
        }

        /// <summary>
        /// The inclusive lower bound of the string range to query for.
        /// </summary>
        public string Start { get; set; }

        /// <summary>
        /// The inclusive upper bound of the string range to query for.
        /// </summary>
        public string End { get; set; }
        
        /// <inheritdoc/>
        public override JsonWriter WriteJson(JsonWriter writer)
        {
            WriteIndexHeaderJson(writer);

            writer.WritePropertyName("start");
            writer.WriteValue(Start);

            writer.WritePropertyName("end");
            writer.WriteValue(End);

            writer.WriteEndObject();

            return writer;
        }
    }
}
