﻿namespace RiakClient.Messages
{
    using System;
    using Util;

    public class RpbReq
    {
        // TODO 3.0 not needed
        private readonly MessageCode messageCode;
        private readonly bool isMessageCodeOnly = false;

        public RpbReq()
        {
        }

        public RpbReq(MessageCode messageCode)
        {
            this.messageCode = messageCode;
            this.isMessageCodeOnly = true;
        }

        // TODO 3.0 not needed
        public MessageCode MessageCode
        {
            get { return messageCode; }
        }

        // TODO 3.0 not needed
        public bool IsMessageCodeOnly
        {
            get { return isMessageCodeOnly; }
        }
    }

    public interface IRpbGeneratedKey
    {
        bool HasKey { get; }

        byte[] key { get; }
    }

    public abstract class RpbResp { }

    [CLSCompliant(false)]
    public sealed partial class RpbErrorResp { }

    public sealed partial class RpbGetServerInfoResp : RpbResp { }

    public sealed partial class RpbPair { }

    public sealed partial class RpbGetBucketReq { }

    [CLSCompliant(false)]
    public sealed partial class RpbGetBucketResp { }

    [CLSCompliant(false)]
    public sealed partial class RpbSetBucketReq { }

    public sealed partial class RpbResetBucketReq { }

    public sealed partial class RpbGetBucketTypeReq { }

    [CLSCompliant(false)]
    public sealed partial class RpbSetBucketTypeReq { }

    public sealed partial class RpbModFun { }

    public sealed partial class RpbCommitHook { }

    [CLSCompliant(false)]
    public sealed partial class RpbBucketProps { }

    public sealed partial class RpbAuthReq { }

    public sealed partial class MapField { }

    public sealed partial class MapEntry { }

    [CLSCompliant(false)]
    public sealed partial class DtFetchReq : RpbReq { }

    public sealed partial class DtValue { }

    public sealed partial class DtFetchResp : RpbResp { }

    public sealed partial class CounterOp { }

    public sealed partial class SetOp { }

    public sealed partial class MapUpdate { }

    public sealed partial class MapOp { }

    public sealed partial class DtOp { }

    [CLSCompliant(false)]
    public sealed partial class DtUpdateReq : RpbReq { }

    public sealed partial class DtUpdateResp : RpbResp, IRpbGeneratedKey
    {
        public bool HasKey
        {
            get { return EnumerableUtil.NotNullOrEmpty(key); }
        }
    }

    public sealed partial class RpbGetClientIdResp { }

    public sealed partial class RpbSetClientIdReq { }

    [CLSCompliant(false)]
    public sealed partial class RpbGetReq { }

    [CLSCompliant(false)]
    public sealed partial class RpbGetResp { }

    [CLSCompliant(false)]
    public sealed partial class RpbPutReq { }

    [CLSCompliant(false)]
    public sealed partial class RpbPutResp { }

    [CLSCompliant(false)]
    public sealed partial class RpbDelReq { }

    [CLSCompliant(false)]
    public sealed partial class RpbListBucketsReq : RpbReq { }

    public sealed partial class RpbListBucketsResp : RpbResp { }

    [CLSCompliant(false)]
    public sealed partial class RpbListKeysReq { }

    public sealed partial class RpbListKeysResp { }

    public sealed partial class RpbMapRedReq { }

    [CLSCompliant(false)]
    public sealed partial class RpbMapRedResp { }

    [CLSCompliant(false)]
    public sealed partial class RpbIndexReq { }

    public sealed partial class RpbIndexResp { }

    [CLSCompliant(false)]
    public sealed partial class RpbIndexBodyResp { }

    [CLSCompliant(false)]
    public sealed partial class RpbCSBucketReq { }

    [CLSCompliant(false)]
    public sealed partial class RpbCSBucketResp { }

    [CLSCompliant(false)]
    public sealed partial class RpbIndexObject { }

    [CLSCompliant(false)]
    public sealed partial class RpbContent { }

    [CLSCompliant(false)]
    public sealed partial class RpbCoverageEntry { }

    [CLSCompliant(false)]
    public sealed partial class RpbCoverageReq { }

    [CLSCompliant(false)]
    public sealed partial class RpbCoverageResp { }

    public sealed partial class RpbLink { }

    [CLSCompliant(false)]
    public sealed partial class RpbCounterUpdateReq { }

    public sealed partial class RpbCounterUpdateResp { }

    [CLSCompliant(false)]
    public sealed partial class RpbCounterGetReq { }

    public sealed partial class RpbCounterGetResp { }

    public sealed partial class RpbSearchDoc { }

    [CLSCompliant(false)]
    public sealed partial class RpbSearchQueryReq { }

    [CLSCompliant(false)]
    public sealed partial class RpbSearchQueryResp { }

    [CLSCompliant(false)]
    public sealed partial class RpbYokozunaIndex { }

    public sealed partial class RpbYokozunaIndexGetReq { }

    [CLSCompliant(false)]
    public sealed partial class RpbYokozunaIndexGetResp { }

    [CLSCompliant(false)]
    public sealed partial class RpbYokozunaIndexPutReq { }

    public sealed partial class RpbYokozunaIndexDeleteReq { }

    public sealed partial class RpbYokozunaSchema { }

    public sealed partial class RpbYokozunaSchemaPutReq { }

    public sealed partial class RpbYokozunaSchemaGetReq { }

    public sealed partial class RpbYokozunaSchemaGetResp { }

    public sealed partial class RpbGetBucketKeyPreflistReq : RpbReq { }

    public sealed partial class RpbGetBucketKeyPreflistResp : RpbResp { }

    public sealed partial class RpbBucketKeyPreflistItem { }

    [CLSCompliant(false)]
    public sealed partial class TsCoverageEntry { }

    [CLSCompliant(false)]
    public sealed partial class TsCoverageResp { }

    [CLSCompliant(false)]
    public sealed partial class TsListKeysReq { }

    public sealed partial class TsListKeysResp { }

    [CLSCompliant(false)]
    public sealed partial class TsGetReq : RpbReq, ITsByKeyReq { }

    public sealed partial class TsGetResp : RpbResp { }

    public sealed partial class TsPutReq : RpbReq { }

    public sealed partial class TsPutResp : RpbResp { }

    public sealed partial class TsQueryReq : RpbReq { }

    public sealed partial class TsQueryResp : RpbResp, IRpbStreamingResp { }

    public sealed partial class TsListKeysReq : RpbReq { }

    public sealed partial class TsListKeysResp : RpbResp, IRpbStreamingResp { }

    [CLSCompliant(false)]
    public sealed partial class TsDelReq : RpbReq, ITsByKeyReq { }

    public sealed partial class TsDelResp : RpbResp { }

    public sealed partial class TsCoverageReq { }

    public sealed partial class TsRange { }

    public sealed partial class TsInterpolation { }

    public sealed class TsTtbMsg { }
}
