// <copyright file="RiakClient.cs" company="Basho Technologies, Inc.">
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

namespace RiakClient
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Numerics;
    using System.Web;
    using Comms;
    using Extensions;
    using Messages;
    using Models;
    using Models.Index;
    using Models.MapReduce;
    using Models.MapReduce.Inputs;
    using Models.Rest;
    using Models.RiakDt;
    using Models.Search;
    using Util;

    /// <summary>
    /// Provides methods for interacting with a Riak database.
    /// The primary implementation of <see cref="IRiakClient"/>.
    /// </summary>
    public class RiakClient : IRiakClient
    {
        private const string ListBucketsWarning = "*** [CI] -> ListBuckets has serious performance implications and should not be used in production applications. ***";
        private const string ListKeysWarning = "*** [CI] -> ListKeys has serious performance implications and should not be used in production applications. ***";
        private const string InvalidBucketErrorMessage = "Bucket cannot be blank or contain forward-slashes";
        private const string InvalidKeyErrorMessage = "Key cannot be blank or contain forward-slashes";
        private const string InvalidBucketTypeErrorMessage = "Bucket type cannot be blank or contain forward-slashes";

        private readonly IRiakEndPoint endPoint;
        private readonly IRiakConnection batchConnection;

        internal RiakClient(IRiakEndPoint endPoint)
        {
            this.endPoint = endPoint;
            Async = new RiakAsyncClient(this);
        }

        private RiakClient(IRiakConnection batchConnection)
        {
            this.batchConnection = batchConnection;
            Async = new RiakAsyncClient(this);
        }

        /// <inheritdoc/>
        public int RetryCount { get; set; }

        /// <inheritdoc/>
        public IRiakAsyncClient Async { get; private set; }

        /*
         * TODO: these should be client options set via an options object and/or set in app.config
         */
        internal static bool DisableListBucketsWarning { get; set; }

        internal static bool DisableListKeysWarning { get; set; }

        /// <inheritdoc/>
        public RiakResult Ping()
        {
            return UseConnection(conn => conn.PbcWriteRead(MessageCode.RpbPingReq, MessageCode.RpbPingResp));
        }

        /// <inheritdoc/>
        public RiakCounterResult IncrementCounter(string bucket, string counter, long amount, RiakCounterUpdateOptions options = null)
        {
            var request = new RpbCounterUpdateReq { bucket = bucket.ToRiakString(), key = counter.ToRiakString(), amount = amount };
            options = options ?? new RiakCounterUpdateOptions();
            options.Populate(request);

            var result = UseConnection(conn => conn.PbcWriteRead<RpbCounterUpdateReq, RpbCounterUpdateResp>(request));

            if (!result.IsSuccess)
            {
                return new RiakCounterResult(RiakResult<RiakObject>.Error(result.ResultCode, result.ErrorMessage, result.NodeOffline), null);
            }

            var o = new RiakObject(bucket, counter, result.Value.value);
            bool parseResult = false;
            long counterValue = 0L;
            if (options.ReturnValue != null && options.ReturnValue.Value)
            {
                parseResult = long.TryParse(o.Value.FromRiakString(), out counterValue);
            }

            return new RiakCounterResult(RiakResult<RiakObject>.Success(o), parseResult ? (long?)counterValue : null);
        }

        /// <inheritdoc/>
        public RiakCounterResult GetCounter(string bucket, string counter, RiakCounterGetOptions options = null)
        {
            var request = new RpbCounterGetReq { bucket = bucket.ToRiakString(), key = counter.ToRiakString() };
            options = options ?? new RiakCounterGetOptions();
            options.Populate(request);

            var result = UseConnection(conn => conn.PbcWriteRead<RpbCounterGetReq, RpbCounterGetResp>(request));

            if (!result.IsSuccess)
            {
                return new RiakCounterResult(RiakResult<RiakObject>.Error(result.ResultCode, result.ErrorMessage, result.NodeOffline), null);
            }

            var o = new RiakObject(bucket, counter, result.Value.value);
            long counterValue;
            bool parseResult = long.TryParse(o.Value.FromRiakString(), out counterValue);

            return new RiakCounterResult(RiakResult<RiakObject>.Success(o), parseResult ? (long?)counterValue : null);
        }

        /// <inheritdoc/>
        public RiakResult<RiakObject> Get(string bucketType, string bucket, string key, RiakGetOptions options = null)
        {
            options = options ?? RiakGetOptions.Default;
            return Get(new RiakObjectId(bucketType, bucket, key), options);
        }

        /// <inheritdoc/>
        public RiakResult<RiakObject> Get(string bucket, string key, RiakGetOptions options = null)
        {
            return Get(null, bucket, key, RiakGetOptions.Default);
        }

        /// <inheritdoc/>
        public RiakResult<RiakObject> Get(RiakObjectId objectId, RiakGetOptions options = null)
        {
            var request = new RpbGetReq
            {
                type = objectId.BucketType.ToRiakString(),
                bucket = objectId.Bucket.ToRiakString(),
                key = objectId.Key.ToRiakString()
            };

            options = options ?? new RiakGetOptions();
            options.Populate(request);

            var result = UseConnection(conn => conn.PbcWriteRead<RpbGetReq, RpbGetResp>(request));

            if (!result.IsSuccess)
            {
                return RiakResult<RiakObject>.Error(result.ResultCode, result.ErrorMessage, result.NodeOffline);
            }

            if (result.Value.vclock == null)
            {
                return RiakResult<RiakObject>.Error(ResultCode.NotFound, "Unable to find value in Riak", false);
            }

            var o = new RiakObject(objectId.BucketType, objectId.Bucket, objectId.Key, result.Value.content, result.Value.vclock);

            return RiakResult<RiakObject>.Success(o);
        }

        /// <inheritdoc/>
        public IEnumerable<RiakResult<RiakObject>> Get(
            IEnumerable<RiakObjectId> objectIds, RiakGetOptions options = null)
        {
            objectIds = objectIds.ToList();

            options = options ?? new RiakGetOptions();

            var results = UseConnection(conn =>
            {
                var responses = objectIds.Select(bkp =>
                {
                    // modified closure FTW
                    var bk = bkp;

                    var req = new RpbGetReq
                    {
                        type = bk.BucketType.ToRiakString(),
                        bucket = bk.Bucket.ToRiakString(),
                        key = bk.Key.ToRiakString()
                    };
                    options.Populate(req);

                    return conn.PbcWriteRead<RpbGetReq, RpbGetResp>(req);
                }).ToList();
                return RiakResult<IEnumerable<RiakResult<RpbGetResp>>>.Success(responses);
            });

            return results.Value.Zip(objectIds, Tuple.Create).Select(result =>
            {
                if (!result.Item1.IsSuccess)
                {
                    return RiakResult<RiakObject>.Error(result.Item1.ResultCode, result.Item1.ErrorMessage, result.Item1.NodeOffline);
                }

                if (result.Item1.Value.vclock == null)
                {
                    return RiakResult<RiakObject>.Error(ResultCode.NotFound, "Unable to find value in Riak", false);
                }

                var o = new RiakObject(result.Item2.BucketType, result.Item2.Bucket, result.Item2.Key, result.Item1.Value.content.First(), result.Item1.Value.vclock);

                if (result.Item1.Value.content.Count > 1)
                {
                    o.Siblings = result.Item1.Value.content.Select(c =>
                        new RiakObject(result.Item2.BucketType, result.Item2.Bucket, result.Item2.Key, c, result.Item1.Value.vclock)).ToList();
                }

                return RiakResult<RiakObject>.Success(o);
            });
        }

        /// <inheritdoc/>
        public RiakResult<RiakObject> Put(RiakObject value, RiakPutOptions options = null)
        {
            options = options ?? new RiakPutOptions();

            var request = value.ToMessage();
            options.Populate(request);

            var result = UseConnection(conn => conn.PbcWriteRead<RpbPutReq, RpbPutResp>(request));

            if (!result.IsSuccess)
            {
                return RiakResult<RiakObject>.Error(result.ResultCode, result.ErrorMessage, result.NodeOffline);
            }

            var finalResult = options.ReturnBody
                ? new RiakObject(value.BucketType, value.Bucket, value.Key, result.Value.content.First(), result.Value.vclock)
                : value;

            if (options.ReturnBody && result.Value.content.Count > 1)
            {
                finalResult.Siblings = result.Value.content.Select(c =>
                    new RiakObject(value.BucketType, value.Bucket, value.Key, c, result.Value.vclock)).ToList();
            }

            return RiakResult<RiakObject>.Success(finalResult);
        }

        /// <inheritdoc/>
        public IEnumerable<RiakResult<RiakObject>> Put(IEnumerable<RiakObject> values, RiakPutOptions options = null)
        {
            options = options ?? new RiakPutOptions();

            var results = UseConnection(conn =>
            {
                var responses = values.Select(v =>
                {
                    var msg = v.ToMessage();
                    options.Populate(msg);

                    return conn.PbcWriteRead<RpbPutReq, RpbPutResp>(msg);
                }).ToList();

                return RiakResult<IEnumerable<RiakResult<RpbPutResp>>>.Success(responses);
            });

            return results.Value.Zip(values, Tuple.Create).Select(t =>
            {
                if (t.Item1.IsSuccess)
                {
                    var finalResult = options.ReturnBody
                        ? new RiakObject(t.Item2.BucketType, t.Item2.Bucket, t.Item2.Key, t.Item1.Value.content.First(), t.Item1.Value.vclock)
                        : t.Item2;

                    if (options.ReturnBody && t.Item1.Value.content.Count > 1)
                    {
                        finalResult.Siblings = t.Item1.Value.content.Select(c =>
                            new RiakObject(t.Item2.BucketType, t.Item2.Bucket, t.Item2.Key, c, t.Item1.Value.vclock)).ToList();
                    }

                    return RiakResult<RiakObject>.Success(finalResult);
                }

                return RiakResult<RiakObject>.Error(t.Item1.ResultCode, t.Item1.ErrorMessage, t.Item1.NodeOffline);
            });
        }

        /// <inheritdoc/>
        public RiakResult Delete(RiakObject riakObject, RiakDeleteOptions options = null)
        {
            return Delete(riakObject.BucketType, riakObject.Bucket, riakObject.Key, options);
        }

        /// <inheritdoc/>
        public RiakResult Delete(string bucket, string key, RiakDeleteOptions options = null)
        {
            return Delete(null, bucket, key, options);
        }

        /// <inheritdoc/>
        public RiakResult Delete(string bucketType, string bucket, string key, RiakDeleteOptions options = null)
        {
            return Delete(new RiakObjectId(bucketType, bucket, key), options);
        }

        /// <inheritdoc/>
        public RiakResult Delete(RiakObjectId objectId, RiakDeleteOptions options = null)
        {
            options = options ?? new RiakDeleteOptions();

            var request = new RpbDelReq
            {
                type = objectId.BucketType.ToRiakString(),
                bucket = objectId.Bucket.ToRiakString(),
                key = objectId.Key.ToRiakString()
            };

            options.Populate(request);
            var result = UseConnection(conn => conn.PbcWriteRead(request, MessageCode.RpbDelResp));

            return result;
        }

        /// <inheritdoc/>
        public IEnumerable<RiakResult> Delete(IEnumerable<RiakObjectId> objectIds, RiakDeleteOptions options = null)
        {
            var results = UseConnection(conn => Delete(conn, objectIds, options));
            return results.Value;
        }

        /// <inheritdoc/>
        public RiakResult<RiakMapReduceResult> MapReduce(RiakMapReduceQuery query)
        {
            var request = query.ToMessage();
            var response = UseConnection(conn => conn.PbcWriteRead<RpbMapRedReq, RpbMapRedResp>(request, r => r.IsSuccess && !r.Value.done));

            if (response.IsSuccess)
            {
                return RiakResult<RiakMapReduceResult>.Success(new RiakMapReduceResult(response.Value));
            }

            return RiakResult<RiakMapReduceResult>.Error(response.ResultCode, response.ErrorMessage, response.NodeOffline);
        }

        /// <inheritdoc/>
        public RiakResult<RiakSearchResult> Search(RiakSearchRequest search)
        {
            var request = search.ToMessage();
            var response = UseConnection(conn => conn.PbcWriteRead<RpbSearchQueryReq, RpbSearchQueryResp>(request));

            if (response.IsSuccess)
            {
                return RiakResult<RiakSearchResult>.Success(new RiakSearchResult(response.Value));
            }

            return RiakResult<RiakSearchResult>.Error(response.ResultCode, response.ErrorMessage, response.NodeOffline);
        }

        /// <inheritdoc/>
        public RiakResult<RiakStreamedMapReduceResult> StreamMapReduce(RiakMapReduceQuery query)
        {
            var request = query.ToMessage();
            var response = UseDelayedConnection((conn, onFinish) =>
                conn.PbcWriteStreamRead<RpbMapRedReq, RpbMapRedResp>(request, r => r.IsSuccess && !r.Value.done, onFinish));

            if (response.IsSuccess)
            {
                return RiakResult<RiakStreamedMapReduceResult>.Success(new RiakStreamedMapReduceResult(response.Value));
            }

            return RiakResult<RiakStreamedMapReduceResult>.Error(response.ResultCode, response.ErrorMessage, response.NodeOffline);
        }

        /// <inheritdoc/>
        public RiakResult<IEnumerable<string>> ListBuckets()
        {
            return ListBuckets(RiakConstants.DefaultBucketType);
        }

        /// <inheritdoc/>
        public RiakResult<IEnumerable<string>> ListBuckets(string bucketType)
        {
            WarnAboutListBuckets();

            var listBucketReq = new RpbListBucketsReq
            {
                type = bucketType.ToRiakString()
            };

            var result = UseConnection(conn => conn.PbcWriteRead<RpbListBucketsReq, RpbListBucketsResp>(listBucketReq));

            if (result.IsSuccess)
            {
                var buckets = result.Value.buckets.Select(b => b.FromRiakString());
                return RiakResult<IEnumerable<string>>.Success(buckets);
            }

            return RiakResult<IEnumerable<string>>.Error(result.ResultCode, result.ErrorMessage, result.NodeOffline);
        }

        /// <inheritdoc/>
        public RiakResult<IEnumerable<string>> StreamListBuckets()
        {
            var listBucketsRequest = new RpbListBucketsReq { stream = true };
            var result = UseDelayedConnection(
                (conn, onFinish) => conn.PbcWriteStreamRead<RpbListBucketsReq, RpbListBucketsResp>(
                    listBucketsRequest, lbr => lbr.IsSuccess && !lbr.Value.done, onFinish));

            if (result.IsSuccess)
            {
                var buckets = result.Value.Where(r => r.IsSuccess).SelectMany(r => r.Value.buckets).Select(k => k.FromRiakString());
                return RiakResult<IEnumerable<string>>.Success(buckets);
            }

            return RiakResult<IEnumerable<string>>.Error(result.ResultCode, result.ErrorMessage, result.NodeOffline);
        }

        /// <inheritdoc/>
        public RiakResult<IEnumerable<string>> ListKeys(string bucket)
        {
            return UseConnection(conn => ListKeys(conn, null, bucket));
        }

        /// <inheritdoc/>
        public RiakResult<IEnumerable<string>> ListKeys(string bucketType, string bucket)
        {
            return UseConnection(conn => ListKeys(conn, bucketType, bucket));
        }

        /// <inheritdoc/>
        public RiakResult<IEnumerable<string>> StreamListKeys(string bucket)
        {
            return StreamListKeys(null, bucket);
        }

        /// <inheritdoc/>
        public RiakResult<IEnumerable<string>> StreamListKeys(string bucketType, string bucket)
        {
            WarnAboutListKeys();

            var listKeysRequest = new RpbListKeysReq
            {
                type = bucketType.ToRiakString(),
                bucket = bucket.ToRiakString()
            };

            var result = UseDelayedConnection(
                (conn, onFinish) => conn.PbcWriteStreamRead<RpbListKeysReq, RpbListKeysResp>(
                    listKeysRequest, lkr => lkr.IsSuccess && !lkr.Value.done, onFinish));

            if (result.IsSuccess)
            {
                var keys = result.Value.Where(r => r.IsSuccess).SelectMany(r => r.Value.keys).Select(k => k.FromRiakString());
                return RiakResult<IEnumerable<string>>.Success(keys);
            }

            return RiakResult<IEnumerable<string>>.Error(result.ResultCode, result.ErrorMessage, result.NodeOffline);
        }

        /// <inheritdoc/>
        public RiakResult<IList<string>> ListKeysFromIndex(string bucket)
        {
            return ListKeysFromIndex(null, bucket);
        }

        /// <inheritdoc/>
        public RiakResult<IList<string>> ListKeysFromIndex(string bucketType, string bucket)
        {
            var result = GetSecondaryIndex(new RiakIndexId(bucketType, bucket, RiakConstants.SystemIndexKeys.RiakBucketIndex), bucket);
            return RiakResult<IList<string>>.Success(result.Value.IndexKeyTerms.Select(ikt => ikt.Key).ToList());
        }

        /// <inheritdoc/>
        public RiakResult<RiakBucketProperties> GetBucketProperties(string bucket)
        {
            return GetBucketProperties(null, bucket);
        }

        /// <inheritdoc/>
        public RiakResult<RiakBucketProperties> GetBucketProperties(string bucketType, string bucket)
        {
            var getBucketRequest = new RpbGetBucketReq { type = bucketType.ToRiakString(), bucket = bucket.ToRiakString() };

            var result = UseConnection(conn => conn.PbcWriteRead<RpbGetBucketReq, RpbGetBucketResp>(getBucketRequest));

            if (result.IsSuccess)
            {
                var props = new RiakBucketProperties(result.Value.props);
                return RiakResult<RiakBucketProperties>.Success(props);
            }

            return RiakResult<RiakBucketProperties>.Error(result.ResultCode, result.ErrorMessage, result.NodeOffline);
        }

        /// <inheritdoc/>
        public RiakResult SetBucketProperties(string bucket, RiakBucketProperties properties, bool useHttp = false)
        {
            return useHttp ? SetHttpBucketProperties(bucket, properties) : SetBucketProperties(null, bucket, properties);
        }

        /// <inheritdoc/>
        public RiakResult SetBucketProperties(string bucketType, string bucket, RiakBucketProperties properties)
        {
            return SetPbcBucketProperties(bucketType, bucket, properties);
        }

        /// <inheritdoc/>
        [Obsolete("This overload will be removed in the next version.")]
        public RiakResult ResetBucketProperties(string bucket, bool useHttp = false)
        {
            return useHttp ? ResetHttpBucketProperties(bucket) : ResetPbcBucketProperties(null, bucket);
        }

        /// <inheritdoc/>
        public RiakResult ResetBucketProperties(string bucketType, string bucket)
        {
            return ResetPbcBucketProperties(bucketType, bucket);
        }

        /// <inheritdoc/>
        [Obsolete("Linkwalking has been depreciated as of Riak 2.0. This method will be removed in the next major version.")]
        public RiakResult<IList<RiakObject>> WalkLinks(RiakObject riakObject, IList<RiakLink> riakLinks)
        {
            System.Diagnostics.Debug.Assert(riakLinks.Count > 0, "Link walking requires at least one link");

            var input = new RiakBucketKeyInput()
                .Add(riakObject.ToRiakObjectId());

            var query = new RiakMapReduceQuery()
                .Inputs(input);

            var lastLink = riakLinks.Last();

            foreach (var riakLink in riakLinks)
            {
                var link = riakLink;
                var keep = ReferenceEquals(link, lastLink);

                query.Link(l => l.FromRiakLink(link).Keep(keep));
            }

            var result = MapReduce(query);

            if (result.IsSuccess)
            {
                var linkResults = result.Value.PhaseResults.GroupBy(r => r.Phase).Where(g => g.Key == riakLinks.Count - 1);
                var linkResultStrings = linkResults.SelectMany(lr => lr.ToList(), (lr, r) => new { lr, r })
                    .SelectMany(@t => @t.r.Values, (@t, s) => s.FromRiakString());

                // var linkResultStrings = linkResults.SelectMany(g => g.Select(r => r.Values.Value.FromRiakString()));
                var rawLinks = linkResultStrings.SelectMany(RiakLink.ParseArrayFromJsonString).Distinct();
                var objectIds = rawLinks.Select(l => new RiakObjectId(l.Bucket, l.Key)).ToList();

                var objects = Get(objectIds, new RiakGetOptions());

                // FIXME
                // we could be discarding results here. Not good?
                // This really should be a multi-phase map/reduce
                return RiakResult<IList<RiakObject>>.Success(objects.Where(r => r.IsSuccess).Select(r => r.Value).ToList());
            }

            return RiakResult<IList<RiakObject>>.Error(result.ResultCode, result.ErrorMessage, result.NodeOffline);
        }

        /// <inheritdoc/>
        public RiakResult<RiakServerInfo> GetServerInfo()
        {
            var result = UseConnection(conn => conn.PbcWriteRead<RpbGetServerInfoResp>(MessageCode.RpbGetServerInfoReq));

            if (result.IsSuccess)
            {
                return RiakResult<RiakServerInfo>.Success(new RiakServerInfo(result.Value));
            }

            return RiakResult<RiakServerInfo>.Error(result.ResultCode, result.ErrorMessage, result.NodeOffline);
        }

        /// <inheritdoc/>
        public RiakResult<RiakStreamedIndexResult> StreamGetSecondaryIndex(RiakIndexId index, BigInteger value, RiakIndexGetOptions options = null)
        {
            var intIndex = index.ToIntIndexId();
            return StreamGetSecondaryIndexEquals(intIndex, value.ToString(), options);
        }

        /// <inheritdoc/>
        public RiakResult<RiakStreamedIndexResult> StreamGetSecondaryIndex(RiakIndexId index, string value, RiakIndexGetOptions options = null)
        {
            var binIndex = index.ToBinIndexId();
            return StreamGetSecondaryIndexEquals(binIndex, value, options);
        }

        /// <inheritdoc/>
        public RiakResult<RiakStreamedIndexResult> StreamGetSecondaryIndex(RiakIndexId index, BigInteger min, BigInteger max, RiakIndexGetOptions options = null)
        {
            var intIndex = index.ToIntIndexId();
            return StreamGetSecondaryIndexRange(intIndex, min.ToString(), max.ToString(), options);
        }

        /// <inheritdoc/>
        public RiakResult<RiakStreamedIndexResult> StreamGetSecondaryIndex(RiakIndexId index, string min, string max, RiakIndexGetOptions options = null)
        {
            var binIndex = index.ToBinIndexId();
            return StreamGetSecondaryIndexRange(binIndex, min, max, options);
        }

        /// <inheritdoc/>
        public RiakResult<RiakIndexResult> GetSecondaryIndex(RiakIndexId index, BigInteger min, BigInteger max, RiakIndexGetOptions options = null)
        {
            var intIndex = index.ToIntIndexId();
            return GetSecondaryIndexRange(intIndex, min.ToString(), max.ToString(), options);
        }

        /// <inheritdoc/>
        public RiakResult<RiakIndexResult> GetSecondaryIndex(RiakIndexId index, string min, string max, RiakIndexGetOptions options = null)
        {
            var binIndex = index.ToBinIndexId();
            return GetSecondaryIndexRange(binIndex, min, max, options);
        }

        /// <inheritdoc/>
        public RiakResult<RiakIndexResult> GetSecondaryIndex(RiakIndexId index, BigInteger value, RiakIndexGetOptions options = null)
        {
            var intIndex = index.ToIntIndexId();
            return GetSecondaryIndexEquals(intIndex, value.ToString(), options);
        }

        /// <inheritdoc/>
        public RiakResult<RiakIndexResult> GetSecondaryIndex(RiakIndexId index, string value, RiakIndexGetOptions options = null)
        {
            var binIndex = index.ToBinIndexId();
            return GetSecondaryIndexEquals(binIndex, value, options);
        }

        /// <inheritdoc/>
        public void Batch(Action<IRiakBatchClient> batchAction)
        {
            Batch<object>(c => { batchAction(c); return null; });
        }

        /// <inheritdoc />
        public T Batch<T>(Func<IRiakBatchClient, T> batchFunction)
        {
            var funResult = default(T);

            Func<IRiakConnection, Action, RiakResult<IEnumerable<RiakResult<object>>>> helperBatchFun = (conn, onFinish) =>
            {
                try
                {
                    funResult = batchFunction(new RiakClient(conn));
                    return RiakResult<IEnumerable<RiakResult<object>>>.Success(null);
                }
                catch (Exception ex)
                {
                    return RiakResult<IEnumerable<RiakResult<object>>>.Error(
                        ResultCode.BatchException,
                        string.Format("{0}\n{1}", ex.Message, ex.StackTrace),
                        true);
                }
                finally
                {
                    onFinish();
                }
            };

            var result = endPoint.UseDelayedConnection(helperBatchFun, RetryCount);

            if (!result.IsSuccess && result.ResultCode == ResultCode.BatchException)
            {
                throw new Exception(result.ErrorMessage);
            }

            return funResult;
        }

        /// <inheritdoc/>
        public RiakCounterResult DtFetchCounter(
            string bucketType, string bucket, string key, RiakDtFetchOptions options = null)
        {
            return DtFetchCounter(new RiakObjectId(bucketType, bucket, key), options);
        }

        /// <inheritdoc/>
        public RiakCounterResult DtFetchCounter(RiakObjectId objectId, RiakDtFetchOptions options = null)
        {
            var message = new DtFetchReq
            {
                type = objectId.BucketType.ToRiakString(),
                bucket = objectId.Bucket.ToRiakString(),
                key = objectId.Key.ToRiakString()
            };

            options = options ?? new RiakDtFetchOptions();

            options.Populate(message);

            var result = UseConnection(conn => conn.PbcWriteRead<DtFetchReq, DtFetchResp>(message));

            if (!result.IsSuccess)
            {
                return new RiakCounterResult(RiakResult<RiakObject>.Error(result.ResultCode, result.ErrorMessage, result.NodeOffline), null);
            }

            var o = new RiakObject(objectId, result.Value.value);

            var rcr = new RiakCounterResult(RiakResult<RiakObject>.Success(o));

            if (result.Value.value != null)
            {
                rcr.Value = result.Value.value.counter_value;
            }

            return rcr;
        }

        /// <inheritdoc/>
        public RiakCounterResult DtUpdateCounter(
            string bucketType, string bucket, string key, long amount, RiakDtUpdateOptions options = null)
        {
            return DtUpdateCounter(new RiakObjectId(bucketType, bucket, key), amount, options);
        }

        /// <inheritdoc/>
        public RiakCounterResult DtUpdateCounter(
            RiakObjectId objectId, long amount, RiakDtUpdateOptions options = null)
        {
            var request = new DtUpdateReq
            {
                type = objectId.BucketType.ToRiakString(),
                bucket = objectId.Bucket.ToRiakString(),
                key = objectId.Key.ToRiakString(),
                op = new CounterOperation(amount).ToDtOp()
            };

            options = options ?? new RiakDtUpdateOptions();
            options.Populate(request);

            var result = UseConnection(conn => conn.PbcWriteRead<DtUpdateReq, DtUpdateResp>(request));

            if (!result.IsSuccess)
            {
                return new RiakCounterResult(RiakResult<RiakObject>.Error(result.ResultCode, result.ErrorMessage, result.NodeOffline), null);
            }

            var o = new RiakObject(objectId, result.Value.counter_value);

            var rcr = new RiakCounterResult(RiakResult<RiakObject>.Success(o));

            if (options.ReturnBody)
            {
                rcr.Value = result.Value.counter_value;
            }

            return rcr;
        }

        /// <inheritdoc/>
        public RiakDtSetResult DtFetchSet(string bucketType, string bucket, string key, RiakDtFetchOptions options = null)
        {
            return DtFetchSet(new RiakObjectId(bucketType, bucket, key), options);
        }

        /// <inheritdoc/>
        public RiakDtSetResult DtFetchSet(RiakObjectId objectId, RiakDtFetchOptions options = null)
        {
            var message = new DtFetchReq
            {
                type = objectId.BucketType.ToRiakString(),
                bucket = objectId.Bucket.ToRiakString(),
                key = objectId.Key.ToRiakString()
            };

            options = options ?? new RiakDtFetchOptions();

            options.Populate(message);

            var result = UseConnection(conn => conn.PbcWriteRead<DtFetchReq, DtFetchResp>(message));

            if (!result.IsSuccess)
            {
                return new RiakDtSetResult(RiakResult<RiakObject>.Error(
                                result.ResultCode,
                                result.ErrorMessage,
                                result.NodeOffline));
            }

            var rsr =
                new RiakDtSetResult(RiakResult<RiakObject>.Success(new RiakObject(objectId, result.Value)));

            if (options.IncludeContext)
            {
                rsr.Context = result.Value.context;
            }

            if (result.Value.value != null)
            {
                rsr.Values = result.Value.value.set_value;
            }

            return rsr;
        }

        /// <inheritdoc/>
        public RiakDtSetResult DtUpdateSet<T>(
            string bucketType,
            string bucket,
            string key,
            SerializeObjectToByteArray<T> serialize,
            byte[] context,
            List<T> adds = null,
            List<T> removes = null,
            RiakDtUpdateOptions options = null)
        {
            return DtUpdateSet(new RiakObjectId(bucketType, bucket, key), serialize, context, adds, removes, options);
        }

        /// <inheritdoc/>
        public RiakDtSetResult DtUpdateSet<T>(
            RiakObjectId objectId,
            SerializeObjectToByteArray<T> serialize,
            byte[] context,
            List<T> adds = null,
            List<T> removes = null,
            RiakDtUpdateOptions options = null)
        {
            if (EnumerableUtil.NotNullOrEmpty(removes) && context == null)
            {
                throw new ArgumentNullException("context", "Set item removal specified, but context was null");
            }

            var request = new DtUpdateReq
            {
                type = objectId.BucketType.ToRiakString(),
                bucket = objectId.Bucket.ToRiakString(),
                key = objectId.Key.ToRiakString(),
                context = context,
                op = new SetOperation().ToDtOp()
            };

            options = options ?? new RiakDtUpdateOptions();
            options.Populate(request);

            if (adds != null)
            {
                request.op.set_op.adds.AddRange(adds.Select(a => serialize(a)));
            }

            if (removes != null)
            {
                request.op.set_op.removes.AddRange(removes.Select(r => serialize(r)));
            }

            var response = UseConnection(conn => conn.PbcWriteRead<DtUpdateReq, DtUpdateResp>(request));

            var resultSet =
                new RiakDtSetResult(RiakResult<RiakObject>.Success(new RiakObject(objectId, response.Value)));

            if (options.IncludeContext && response.Value != null)
            {
                resultSet.Context = response.Value.context;
            }

            if (options.ReturnBody && response.Value != null)
            {
                resultSet.Values = response.Value.set_value;
            }

            return resultSet;
        }

        /// <inheritdoc/>
        public RiakDtMapResult DtFetchMap(
            string bucket,
            string key,
            string bucketType = null,
            RiakDtFetchOptions options = null)
        {
            return DtFetchMap(new RiakObjectId(bucketType, bucket, key), options);
        }

        public RiakDtMapResult DtFetchMap(RiakObjectId objectId, RiakDtFetchOptions options = null)
        {
            var message = new DtFetchReq
            {
                bucket = objectId.Bucket.ToRiakString(),
                type = objectId.BucketType.ToRiakString(),
                key = objectId.Key.ToRiakString()
            };

            options = options ?? new RiakDtFetchOptions();

            options.Populate(message);

            var result = UseConnection(conn => conn.PbcWriteRead<DtFetchReq, DtFetchResp>(message));

            if (!result.IsSuccess)
            {
                return new RiakDtMapResult(RiakResult<RiakObject>
                               .Error(result.ResultCode, result.ErrorMessage, result.NodeOffline));
            }

            var riakSuccessResult = RiakResult<RiakObject>.Success(new RiakObject(objectId.Bucket, objectId.Key, result.Value.value));
            var riakMapResult = new RiakDtMapResult(riakSuccessResult);

            if (options.IncludeContext)
            {
                riakMapResult.Context = result.Value.context;
            }

            if (result.Value.value != null)
            {
                riakMapResult.Values = result.Value.value.map_value.Select(mapEntry => new RiakDtMapEntry(mapEntry)).ToList();
            }

            return riakMapResult;
        }

        /// <inheritdoc/>
        public RiakDtMapResult DtUpdateMap<T>(
            string bucketType,
            string bucket,
            string key,
            SerializeObjectToByteArray<T> serialize,
            byte[] context,
            List<RiakDtMapField> removes = null,
            /* Is this the right way to represent updates?
             * It seems like there should be something better, but it requires data
             * structures that track themselves and my guess is building the update
             * should be handled long before we get to calling DtUpdateMap<T>
             */
            List<MapUpdate> updates = null,
            RiakDtUpdateOptions options = null)
        {
            return DtUpdateMap(
                new RiakObjectId(bucketType, bucket, key),
                serialize,
                context,
                removes,
                updates,
                options);
        }

        // TODO: We don't use the serialize parameter, remove it.

        /// <inheritdoc/>
        public RiakDtMapResult DtUpdateMap<T>(
            RiakObjectId objectId,
            SerializeObjectToByteArray<T> serialize,
            byte[] context,
            List<RiakDtMapField> removes = null,
            List<MapUpdate> updates = null,
            RiakDtUpdateOptions options = null)
        {
            if (EnumerableUtil.NotNullOrEmpty(removes) && context == null)
            {
                throw new ArgumentNullException("context", "Map field removal specified, but context was null");
            }

            if (context == null && AnyNestedRemovalsIn(updates))
            {
                throw new ArgumentNullException("context", "Map field removal specified, but context was null");
            }

            var request = new DtUpdateReq
            {
                type = objectId.BucketType.ToRiakString(),
                bucket = objectId.Bucket.ToRiakString(),
                key = objectId.Key.ToRiakString(),
                op = new MapOperation().ToDtOp(),
            };

            options = options ?? new RiakDtUpdateOptions();
            options.Populate(request);

            if (removes != null)
            {
                request.op.map_op.removes.AddRange(removes.Select(a => a.ToMapField()));
            }

            if (updates != null)
            {
                request.op.map_op.updates.AddRange(updates);
            }

            if (context != null)
            {
                request.context = context;
            }

            var result = UseConnection(conn => conn.PbcWriteRead<DtUpdateReq, DtUpdateResp>(request));

            if (!result.IsSuccess)
            {
                return new RiakDtMapResult(RiakResult<RiakObject>
                               .Error(result.ResultCode, result.ErrorMessage, result.NodeOffline));
            }

            var riakMapResult =
                new RiakDtMapResult(RiakResult<RiakObject>.Success(new RiakObject(objectId, result.Value.map_value)));

            if (options.IncludeContext)
            {
                riakMapResult.Context = result.Value.context;
            }

            if (options.ReturnBody)
            {
                riakMapResult.Values = result.Value.map_value.Select(mv => new RiakDtMapEntry(mv)).ToList();
            }

            return riakMapResult;
        }

        /// <inheritdoc/>
        public RiakResult<SearchIndexResult> GetSearchIndex(string indexName)
        {
            var request = new RpbYokozunaIndexGetReq { name = indexName.ToRiakString() };
            var result =
                UseConnection(conn => conn.PbcWriteRead<RpbYokozunaIndexGetReq, RpbYokozunaIndexGetResp>(request));

            if (!result.IsSuccess)
            {
                return RiakResult<SearchIndexResult>.Error(result.ResultCode, result.ErrorMessage, result.NodeOffline);
            }

            return RiakResult<SearchIndexResult>.Success(new SearchIndexResult(result.Value));
        }

        /// <inheritdoc/>
        public RiakResult PutSearchIndex(SearchIndex searchIndex)
        {
            var request = new RpbYokozunaIndexPutReq { index = searchIndex.ToMessage() };
            return UseConnection(conn => conn.PbcWriteRead(request, MessageCode.RpbPutResp));
        }

        /// <inheritdoc/>
        public RiakResult DeleteSearchIndex(string indexName)
        {
            var request = new RpbYokozunaIndexDeleteReq { name = indexName.ToRiakString() };
            return UseConnection(conn => conn.PbcWriteRead(request, MessageCode.RpbDelResp));
        }

        /// <inheritdoc/>
        public RiakResult<SearchSchema> GetSearchSchema(string schemaName)
        {
            var request = new RpbYokozunaSchemaGetReq { name = schemaName.ToRiakString() };
            var result =
                UseConnection(conn => conn.PbcWriteRead<RpbYokozunaSchemaGetReq, RpbYokozunaSchemaGetResp>(request));

            if (!result.IsSuccess)
            {
                return RiakResult<SearchSchema>.Error(result.ResultCode, result.ErrorMessage, result.NodeOffline);
            }

            return RiakResult<SearchSchema>.Success(new SearchSchema(result.Value.schema));
        }

        /// <inheritdoc/>
        public RiakResult PutSearchSchema(SearchSchema searchSchema)
        {
            var request = new RpbYokozunaSchemaPutReq { schema = searchSchema.ToMessage() };
            return UseConnection(conn => conn.PbcWriteRead(request, MessageCode.RpbPutResp));
        }

        /// <inheritdoc/>
        public RiakResult<string> GetServerStatus()
        {
            var request = new RiakRestRequest(RiakConstants.Rest.Uri.StatsRoot, RiakConstants.Rest.HttpMethod.Get);
            var result = UseConnection(conn => conn.RestRequest(request));
            if (!result.IsSuccess || result.Value.StatusCode != HttpStatusCode.OK)
            {
                return RiakResult<string>.Error(
                    ResultCode.InvalidResponse,
                    string.Format("Unexpected Status Code: {0} ({1})", result.Value.StatusCode, (int)result.Value.StatusCode),
                    result.NodeOffline);
            }

            return RiakResult<string>.Success(result.Value.Body);
        }

        internal RiakResult SetHttpBucketProperties(string bucket, RiakBucketProperties properties)
        {
            var request = new RiakRestRequest(ToBucketUri(bucket), RiakConstants.Rest.HttpMethod.Put)
            {
                Body = properties.ToJsonString().ToRiakString(),
                ContentType = RiakConstants.ContentTypes.ApplicationJson
            };

            var result = UseConnection(conn => conn.RestRequest(request));
            if (result.IsSuccess && result.Value.StatusCode != HttpStatusCode.NoContent)
            {
                return RiakResult.Error(
                    ResultCode.InvalidResponse,
                    string.Format("Unexpected Status Code: {0} ({1})", result.Value.StatusCode, (int)result.Value.StatusCode),
                    result.NodeOffline);
            }

            return result;
        }

        internal RiakResult ResetPbcBucketProperties(string bucketType, string bucket)
        {
            var request = new RpbResetBucketReq
            {
                type = bucketType.ToRiakString(),
                bucket = bucket.ToRiakString()
            };
            var result = UseConnection(conn => conn.PbcWriteRead(request, MessageCode.RpbResetBucketResp));
            return result;
        }

        internal RiakResult ResetHttpBucketProperties(string bucket)
        {
            var request = new RiakRestRequest(ToBucketPropsUri(bucket), RiakConstants.Rest.HttpMethod.Delete);

            var result = UseConnection(conn => conn.RestRequest(request));
            if (result.IsSuccess)
            {
                switch (result.Value.StatusCode)
                {
                    case HttpStatusCode.NoContent:
                        return result;
                    case HttpStatusCode.NotFound:
                        return RiakResult.Error(ResultCode.NotFound, string.Format("Bucket {0} not found.", bucket), false);
                    default:
                        return RiakResult.Error(
                            ResultCode.InvalidResponse,
                            string.Format("Unexpected Status Code: {0} ({1})", result.Value.StatusCode, (int)result.Value.StatusCode),
                            result.NodeOffline);
                }
            }

            return result;
        }

        internal RiakResult SetPbcBucketProperties(string bucketType, string bucket, RiakBucketProperties properties)
        {
            var request = new RpbSetBucketReq
            {
                type = bucketType.ToRiakString(),
                bucket = bucket.ToRiakString(),
                props = properties.ToMessage()
            };
            var result = UseConnection(conn => conn.PbcWriteRead(request, MessageCode.RpbSetBucketResp));

            return result;
        }

        private static RiakResult<IEnumerable<RiakResult>> Delete(
            IRiakConnection conn,
            IEnumerable<RiakObjectId> objectIds,
            RiakDeleteOptions options = null)
        {
            options = options ?? new RiakDeleteOptions();

            var responses = objectIds.Select(id =>
            {
                var req = new RpbDelReq { bucket = id.Bucket.ToRiakString(), key = id.Key.ToRiakString() };
                options.Populate(req);
                return conn.PbcWriteRead(req, MessageCode.RpbDelResp);
            }).ToList();

            return RiakResult<IEnumerable<RiakResult>>.Success(responses);
        }

        private static Func<RiakResult<RpbListBucketsResp>, bool> ListBucketsRepeatRead()
        {
            return lbr =>
                lbr.IsSuccess && !lbr.Value.done;
        }

        private static void WarnAboutListBuckets()
        {
            if (DisableListBucketsWarning)
            {
                return;
            }

            System.Diagnostics.Debug.Write(ListBucketsWarning);
            System.Diagnostics.Trace.TraceWarning(ListBucketsWarning);
            Console.WriteLine(ListBucketsWarning);
        }

        private static RiakResult<IEnumerable<string>> ListKeys(IRiakConnection conn, string bucketType, string bucket)
        {
            WarnAboutListKeys();

            var listKeysRequest = new RpbListKeysReq
            {
                type = bucketType.ToRiakString(),
                bucket = bucket.ToRiakString()
            };

            var result = conn.PbcWriteRead<RpbListKeysReq, RpbListKeysResp>(listKeysRequest, ListKeysRepeatRead());

            if (result.IsSuccess)
            {
                var keys = result.Value.Where(r => r.IsSuccess).SelectMany(r => r.Value.keys).Select(k => k.FromRiakString()).Distinct().ToList();
                return RiakResult<IEnumerable<string>>.Success(keys);
            }

            return RiakResult<IEnumerable<string>>.Error(result.ResultCode, result.ErrorMessage, result.NodeOffline);
        }

        private static Func<RiakResult<RpbListKeysResp>, bool> ListKeysRepeatRead()
        {
            return lkr =>
                lkr.IsSuccess && !lkr.Value.done;
        }

        private static void WarnAboutListKeys()
        {
            if (DisableListKeysWarning)
            {
                return;
            }

            System.Diagnostics.Debug.Write(ListKeysWarning);
            System.Diagnostics.Trace.TraceWarning(ListKeysWarning);
            Console.WriteLine(ListKeysWarning);
        }

        private static bool ReturnTerms(RiakIndexGetOptions options)
        {
            return options.ReturnTerms != null && options.ReturnTerms.Value;
        }

        private static string ToBucketUri(string bucket)
        {
            return string.Format("{0}/{1}", RiakConstants.Rest.Uri.RiakRoot, HttpUtility.UrlEncode(bucket));
        }

        private static string ToBucketPropsUri(string bucket)
        {
            return string.Format(RiakConstants.Rest.Uri.BucketPropsFmt, HttpUtility.UrlEncode(bucket));
        }

        private RiakResult<RiakStreamedIndexResult> StreamGetSecondaryIndexEquals(RiakIndexId index, string value, RiakIndexGetOptions options = null)
        {
            var message = new RpbIndexReq
            {
                type = index.BucketType.ToRiakString(),
                bucket = index.BucketName.ToRiakString(),
                index = index.IndexName.ToRiakString(),
                qtype = RpbIndexReq.IndexQueryType.eq,
                key = value.ToRiakString(),
                stream = true
            };

            return StreamingIndexRead(message, options);
        }

        private RiakResult<RiakStreamedIndexResult> StreamGetSecondaryIndexRange(
            RiakIndexId index,
            string min,
            string max,
            RiakIndexGetOptions options = null)
        {
            var message = new RpbIndexReq
            {
                type = index.BucketType.ToRiakString(),
                bucket = index.BucketName.ToRiakString(),
                index = index.IndexName.ToRiakString(),
                qtype = RpbIndexReq.IndexQueryType.range,
                range_min = min.ToRiakString(),
                range_max = max.ToRiakString(),
                stream = true
            };

            return StreamingIndexRead(message, options);
        }

        private RiakResult<RiakStreamedIndexResult> StreamingIndexRead(RpbIndexReq message, RiakIndexGetOptions options)
        {
            options = options ?? new RiakIndexGetOptions();
            options.Populate(message);
            var result = UseDelayedConnection((conn, onFinish) => conn.PbcWriteStreamRead<RpbIndexReq, RpbIndexResp>(message, lbr => lbr.IsSuccess && !lbr.Value.done, onFinish));
            if (result.IsSuccess)
            {
                return RiakResult<RiakStreamedIndexResult>.Success(new RiakStreamedIndexResult(ReturnTerms(options), result.Value));
            }

            return RiakResult<RiakStreamedIndexResult>.Error(result.ResultCode, result.ErrorMessage, result.NodeOffline);
        }

        private RiakResult<RiakIndexResult> GetSecondaryIndexRange(RiakIndexId index, string minValue, string maxValue, RiakIndexGetOptions options = null)
        {
            var message = new RpbIndexReq
            {
                type = index.BucketType.ToRiakString(),
                bucket = index.BucketName.ToRiakString(),
                index = index.IndexName.ToRiakString(),
                qtype = RpbIndexReq.IndexQueryType.range,
                range_min = minValue.ToRiakString(),
                range_max = maxValue.ToRiakString()
            };

            options = options ?? new RiakIndexGetOptions();
            options.Populate(message);

            var result = UseConnection(conn => conn.PbcWriteRead<RpbIndexReq, RpbIndexResp>(message));

            if (result.IsSuccess)
            {
                var r = RiakResult<RiakIndexResult>.Success(new RiakIndexResult(ReturnTerms(options), result));

                if (result.Done.HasValue)
                {
                    r.SetDone(result.Done.Value);
                }

                if (result.Value.continuation != null)
                {
                    var continuation = result.Value.continuation.FromRiakString();

                    if (!string.IsNullOrEmpty(continuation))
                    {
                        r.SetContinuation(continuation);
                    }
                }

                return r;
            }

            return RiakResult<RiakIndexResult>.Error(result.ResultCode, result.ErrorMessage, result.NodeOffline);
        }

        private RiakResult<RiakIndexResult> GetSecondaryIndexEquals(RiakIndexId index, string value, RiakIndexGetOptions options = null)
        {
            var message = new RpbIndexReq
            {
                type = index.BucketType.ToRiakString(),
                bucket = index.BucketName.ToRiakString(),
                index = index.IndexName.ToRiakString(),
                key = value.ToRiakString(),
                qtype = RpbIndexReq.IndexQueryType.eq
            };

            options = options ?? new RiakIndexGetOptions();
            options.Populate(message);

            var result = UseConnection(conn => conn.PbcWriteRead<RpbIndexReq, RpbIndexResp>(message));

            if (result.IsSuccess)
            {
                return RiakResult<RiakIndexResult>.Success(new RiakIndexResult(ReturnTerms(options), result));
            }

            return RiakResult<RiakIndexResult>.Error(result.ResultCode, result.ErrorMessage, result.NodeOffline);
        }

        private RiakResult UseConnection(Func<IRiakConnection, RiakResult> op)
        {
            return batchConnection != null ? op(batchConnection) : endPoint.UseConnection(op, RetryCount);
        }

        private RiakResult<TResult> UseConnection<TResult>(Func<IRiakConnection, RiakResult<TResult>> op)
        {
            return batchConnection != null ? op(batchConnection) : endPoint.UseConnection(op, RetryCount);
        }

        private RiakResult<IEnumerable<RiakResult<TResult>>> UseDelayedConnection<TResult>(
            Func<IRiakConnection, Action, RiakResult<IEnumerable<RiakResult<TResult>>>> op)
        {
            return batchConnection != null
                ? op(batchConnection, () => { })
                : endPoint.UseDelayedConnection(op, RetryCount);
        }

        private bool AnyNestedRemovalsIn(IEnumerable<MapUpdate> updates)
        {
            foreach (var mapUpdate in updates)
            {
                if (mapUpdate.map_op != null && EnumerableUtil.NotNullOrEmpty(mapUpdate.map_op.removes))
                {
                    return true;
                }

                if (mapUpdate.set_op != null && EnumerableUtil.NotNullOrEmpty(mapUpdate.set_op.removes))
                {
                    return true;
                }

                if (mapUpdate.map_op != null)
                {
                    return AnyNestedRemovalsIn(mapUpdate.map_op.updates);
                }
            }

            return false;
        }
    }
}
