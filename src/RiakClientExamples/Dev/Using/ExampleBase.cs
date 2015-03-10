﻿// <copyright file="ExampleBase.cs" company="Basho Technologies, Inc.">
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
// </copyright>

namespace RiakClientExamples
{
    using System;
    using NUnit.Framework;
    using RiakClient;
    using RiakClient.Models;

    public abstract class ExampleBase : IDisposable
    {
        private readonly IRiakEndPoint endpoint;

        protected IRiakClient client;
        protected RiakObjectId id;

        public ExampleBase()
        {
            endpoint = RiakCluster.FromConfig("riakConfig");
        }

        [SetUp]
        public void CreateClient()
        {
            client = endpoint.CreateClient();
        }

        [TearDown]
        public void TearDown()
        {
            if (id != null)
            {
                var rslt = client.Delete(id);
                Assert.IsTrue(rslt.IsSuccess);
            }
        }

        public void Dispose()
        {
            Dispose(true);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing && endpoint != null)
            {
                endpoint.Dispose();
            }
        }
    }
}
