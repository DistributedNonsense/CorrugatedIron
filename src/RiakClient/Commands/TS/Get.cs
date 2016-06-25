﻿namespace RiakClient.Commands.TS
{
    using System;
    using Messages;

    /// <summary>
    /// Fetches timeseries data from Riak
    /// </summary>
    [CLSCompliant(false)]
    public class Get : ByKeyCommand<GetResponse>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Get"/> class.
        /// </summary>
        /// <param name="options">Options for this operation. See <see cref="ByKeyOptions"/></param>
        public Get(ByKeyOptions options)
            : base(options)
        {
        }

        public override MessageCode RequestCode
        {
            get { return MessageCode.TsGetReq; }
        }

        public override MessageCode ResponseCode
        {
            get { return MessageCode.TsGetResp; }
        }

        public override Type ResponseType
        {
            get { return typeof(TsGetResp); }
        }

        public override void OnSuccess(RpbResp response)
        {
            var decoder = new ResponseDecoder((TsGetResp)response);
            DecodedResponse dr = decoder.Decode();

            Response = new GetResponse(CommandOptions.Key, dr.Columns, dr.Rows);
        }

        protected override ITsByKeyReq GetByKeyReq()
        {
            return new TsGetReq();
        }

        /// <inheritdoc />
        public class Builder
            : Builder<Get>
        {
        }
    }
}
