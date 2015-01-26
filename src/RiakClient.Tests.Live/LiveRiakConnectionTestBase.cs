﻿// Copyright (c) 2011 - OJ Reeves & Jeremiah Peschka
// Copyright (c) 2015 - Basho Technologies, Inc.
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
using CorrugatedIron.Config;
using CorrugatedIron.Extensions;
using CorrugatedIron.Util;
using NUnit.Framework;

namespace CorrugatedIron.Tests.Live
{
    public abstract class LiveRiakConnectionTestBase
    {
        public static class BucketTypeNames
        {
            public const string Sets = "sets";
            public const string Counters = "counters";
            public const string Maps = "maps";
        }

        protected const string TestBucketType = "leveldb_type";
        protected const string TestBucket = "test_bucket";
        protected const string TestKey = "test_json";
        protected static readonly string TestJson;
        protected const string MapReduceBucket = "map_reduce_bucket";

        // NB: allow_mult/last_write_wins set in devrel setup script
        protected const string MultiBucket = "test_multi_bucket";

        protected const string MultiKey = "test_multi_key";
        protected const string MultiBodyOne = @"{""dishes"": 9}";
        protected const string MultiBodyTwo = @"{""dishes"": 11}";
        protected const string PropertiesTestBucket = @"propertiestestbucket";
        protected readonly Random Random = new Random();

        protected IRiakEndPoint Cluster;
        protected IRiakClient Client;
        protected IRiakClusterConfiguration ClusterConfig;

        static LiveRiakConnectionTestBase()
        {
            RiakClient.DisableListKeysWarning = true;
            TestJson = new
            {
                @string = "value",
                @int = 100,
                @float = 2.34,
                array = new[] { 1, 2, 3 },
                dict = new Dictionary<string, string> {
                    { "foo", "bar" }
                }
            }.ToJson();
        }

        public LiveRiakConnectionTestBase()
        {
#if NOAUTH
            Cluster = RiakCluster.FromConfig("riak1NodeNoAuthConfiguration");
#else
            if (MonoUtil.IsRunningOnMono)
            {
                Cluster = RiakCluster.FromConfig("riak1NodeNoAuthConfiguration");
            }
            else
            {
                Cluster = RiakCluster.FromConfig("riak1NodeConfiguration");
            }
#endif
        }

        [SetUp]
        public virtual void SetUp()
        {
            Client = Cluster.CreateClient();
        }
    }
}

