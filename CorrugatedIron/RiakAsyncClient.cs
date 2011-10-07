﻿// Copyright (c) 2011 - OJ Reeves & Jeremiah Peschka
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

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CorrugatedIron.Models;
using CorrugatedIron.Models.MapReduce;
using CorrugatedIron.Util;

namespace CorrugatedIron
{
    public interface IRiakAsyncClient
    {
        void Ping(Action<RiakResult> callback);

        void Get(string bucket, string key, Action<RiakResult<RiakObject>> callback, uint rVal = RiakConstants.Defaults.RVal);
        void Get(RiakObjectId objectId, Action<RiakResult<RiakObject>> callback, uint rVal = RiakConstants.Defaults.RVal);
        void Get(IEnumerable<RiakObjectId> bucketKeyPairs, Action<IEnumerable<RiakResult<RiakObject>>> callback, uint rVal = RiakConstants.Defaults.RVal);

        void Put(RiakObject value, Action<RiakResult<RiakObject>> callback, RiakPutOptions options = null);
        void Put(IEnumerable<RiakObject> values, Action<IEnumerable<RiakResult<RiakObject>>> callback, RiakPutOptions options = null);

        void Delete(string bucket, string key, Action<RiakResult> callback, RiakDeleteOptions options = null);
        void Delete(RiakObjectId objectId, Action<RiakResult> callback, RiakDeleteOptions options = null);
        void Delete(IEnumerable<RiakObjectId> objectIds, Action<IEnumerable<RiakResult>> callback, RiakDeleteOptions options = null);

        void DeleteBucket(string bucket, Action<IEnumerable<RiakResult>> callback, uint rwVal = RiakConstants.Defaults.RVal);

        void MapReduce(RiakMapReduceQuery query, Action<RiakResult<RiakMapReduceResult>> callback);

        void StreamMapReduce(RiakMapReduceQuery query, Action<RiakResult<RiakStreamedMapReduceResult>> callback);

        void ListBuckets(Action<RiakResult<IEnumerable<string>>> callback);

        void ListKeys(string bucket, Action<RiakResult<IEnumerable<string>>> callback);

        void StreamListKeys(string bucket, Action<RiakResult<IEnumerable<string>>> callback);

        void GetBucketProperties(string bucket, Action<RiakResult<RiakBucketProperties>> callback, bool extended = false);

        void SetBucketProperties(string bucket, RiakBucketProperties properties, Action<RiakResult> callback);

        void WalkLinks(RiakObject riakObject, IList<RiakLink> riakLinks, Action<RiakResult<IList<RiakObject>>> callback);

        void GetServerInfo(Action<RiakResult<RiakServerInfo>> callback);

        void Batch(Action<IRiakBatchClient> batchAction);
    }

    internal class RiakAsyncClient : IRiakAsyncClient
    {
        private readonly IRiakClient _client;

        public RiakAsyncClient(IRiakClient client)
        {
            _client = client;
        }

        public void Ping(Action<RiakResult> callback)
        {
            ExecAsync(() => callback(_client.Ping()));
        }

        public void Get(string bucket, string key, Action<RiakResult<RiakObject>> callback, uint rVal = RiakConstants.Defaults.RVal)
        {
            ExecAsync(() => callback(_client.Get(bucket, key, rVal)));
        }

        public void Get(RiakObjectId objectId, Action<RiakResult<RiakObject>> callback, uint rVal = RiakConstants.Defaults.RVal)
        {
            ExecAsync(() => callback(_client.Get(objectId.Bucket, objectId.Key, rVal)));
        }

        public void Get(IEnumerable<RiakObjectId> bucketKeyPairs, Action<IEnumerable<RiakResult<RiakObject>>> callback, uint rVal = RiakConstants.Defaults.RVal)
        {
            ExecAsync(() => callback(_client.Get(bucketKeyPairs, rVal)));
        }

        public void Put(IEnumerable<RiakObject> values, Action<IEnumerable<RiakResult<RiakObject>>> callback, RiakPutOptions options)
        {
            ExecAsync(() => callback(_client.Put(values, options)));
        }

        public void Put(RiakObject value, Action<RiakResult<RiakObject>> callback, RiakPutOptions options)
        {
            ExecAsync(() => callback(_client.Put(value, options)));
        }

        public void Delete(string bucket, string key, Action<RiakResult> callback, RiakDeleteOptions options = null)
        {
            ExecAsync(() => callback(_client.Delete(bucket, key, options)));
        }

        public void Delete(RiakObjectId objectId, Action<RiakResult> callback, RiakDeleteOptions options = null)
        {
            ExecAsync(() => callback(_client.Delete(objectId.Bucket, objectId.Key, options)));
        }

        public void Delete(IEnumerable<RiakObjectId> objectIds, Action<IEnumerable<RiakResult>> callback, RiakDeleteOptions options = null)
        {
            ExecAsync(() => callback(_client.Delete(objectIds, options)));
        }

        public void DeleteBucket(string bucket, Action<IEnumerable<RiakResult>> callback, uint rwVal = RiakConstants.Defaults.RVal)
        {
            ExecAsync(() => callback(_client.DeleteBucket(bucket, rwVal)));
        }

        public void MapReduce(RiakMapReduceQuery query, Action<RiakResult<RiakMapReduceResult>> callback)
        {
            ExecAsync(() => callback(_client.MapReduce(query)));
        }

        public void StreamMapReduce(RiakMapReduceQuery query, Action<RiakResult<RiakStreamedMapReduceResult>> callback)
        {
            ExecAsync(() => callback(_client.StreamMapReduce(query)));
        }

        public void ListBuckets(Action<RiakResult<IEnumerable<string>>> callback)
        {
            ExecAsync(() => callback(_client.ListBuckets()));
        }

        public void ListKeys(string bucket, Action<RiakResult<IEnumerable<string>>> callback)
        {
            ExecAsync(() => callback(_client.ListKeys(bucket)));
        }

        public void StreamListKeys(string bucket, Action<RiakResult<IEnumerable<string>>> callback)
        {
            ExecAsync(() => callback(_client.StreamListKeys(bucket)));
        }

        public void GetBucketProperties(string bucket, Action<RiakResult<RiakBucketProperties>> callback, bool extended = false)
        {
            ExecAsync(() => callback(_client.GetBucketProperties(bucket, extended)));
        }

        public void SetBucketProperties(string bucket, RiakBucketProperties properties, Action<RiakResult> callback)
        {
            ExecAsync(() => callback(_client.SetBucketProperties(bucket, properties)));
        }

        public void WalkLinks(RiakObject riakObject, IList<RiakLink> riakLinks, Action<RiakResult<IList<RiakObject>>> callback)
        {
            ExecAsync(() => callback(_client.WalkLinks(riakObject, riakLinks)));
        }

        public void GetServerInfo(Action<RiakResult<RiakServerInfo>> callback)
        {
            ExecAsync(() => callback(_client.GetServerInfo()));
        }

        public void Batch(Action<IRiakBatchClient> batchAction)
        {
            ExecAsync(() => _client.Batch(batchAction));
        }

        private static void ExecAsync(Action action)
        {
            Task.Factory.StartNew(action);
        }
    }
}
