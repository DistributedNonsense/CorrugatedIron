// Copyright (c) 2011 - OJ Reeves & Jeremiah Peschka
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

using System.Linq;
using CorrugatedIron.Comms;
using CorrugatedIron.Util;
using CorrugatedIron.Models;
using CorrugatedIron.Models.MapReduce;
using CorrugatedIron.Models.MapReduce.Inputs;
using CorrugatedIron.Tests.Extensions;
using CorrugatedIron.Extensions;
using NUnit.Framework;
using Newtonsoft.Json;

namespace CorrugatedIron.Tests.Live
{
    [TestFixture()]
    public class RiakSearchMapReduceInputTests : RiakMapReduceTests
    {
        // N.B. You need to install the search hooks on the riak_search_bucket first via `bin/search-cmd install riak_search_bucket`
        private const string _riakSearchKey = "a.hacker";
        private const string _riakSearchKey2 = "a.public";
        private const string _riakSearchDoc = "{\"name\":\"Alyssa P. Hacker\", \"bio\":\"I'm an engineer, making awesome things.\", \"favorites\":{\"book\":\"The Moon is a Harsh Mistress\",\"album\":\"Magical Mystery Tour\", }}";
        private const string _riakSearchDoc2 = "{\"name\":\"Alan Q. Public\", \"bio\":\"I'm an exciting mathematician\", \"favorites\":{\"book\":\"Prelude to Mathematics\",\"album\":\"The Fame Monster\"}}";

        public RiakSearchMapReduceInputTests () : base()
        {
            bucket = "riak_search_bucket";
        }
        
        [SetUp]
        public void SetUp() 
        {
            Cluster = new RiakCluster(ClusterConfig, new RiakConnectionFactory());
            ClientGenerator = () => new RiakClient(Cluster);
            Client = ClientGenerator();
            
            var props = Client.GetBucketProperties(bucket, true).Value;
            props.SetSearch(true);
            Client.SetBucketProperties(bucket, props);
        }
        
        [TearDown]
        public void TearDown()
        {
            Client.DeleteBucket(bucket);
        }
        
        [Test]
        public void SearchingByNameReturnsTheObjectId()
        {
            Client.Put(new RiakObject(bucket, _riakSearchKey, _riakSearchDoc, RiakConstants.ContentTypes.ApplicationJson));
            Client.Put(new RiakObject(bucket, _riakSearchKey2, _riakSearchDoc2, RiakConstants.ContentTypes.ApplicationJson));
            
            var mr = new RiakMapReduceQuery();
            
            var modFunArg = new RiakModuleFunctionArgInput
                                {
                Module = "riak_search",
                Function = "mapred_search",
                Arg = new[] {bucket, "name:Al*"}
            };
            
            mr.Inputs(modFunArg)
                .MapJs(m => 
                    m.Source(@"
function(value, keydata, arg) 
{
    return [value];
}")
                .Keep(true))
                .ReduceJs(r => r.Source(@"
function(values, arg)
{
    return values;
}
").Keep(true))
                    ;
            
            var result = Client.MapReduce(mr);
            result.IsSuccess.ShouldBeTrue();
            
            var mrResult = result.Value;
            mrResult.PhaseResults.Count().ShouldEqual(2);
            
            mrResult.PhaseResults.ElementAt(0).Values.ShouldNotBeNull();
            mrResult.PhaseResults.ElementAt(1).Values.ShouldNotBeNull();
            // TODO Add data introspection to test - need to verify the results, after all.
        }
    }
}

