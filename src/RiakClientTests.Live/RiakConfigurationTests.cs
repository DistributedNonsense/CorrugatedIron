// <copyright file="RiakConfigurationTests.cs" company="Basho Technologies, Inc.">
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

namespace RiakClientTests.Live.RiakConfigurationTests
{
    using System.IO;
    using NUnit.Framework;
    using RiakClient;
    using RiakClient.Config;

    [TestFixture]
    public class WhenLoadingFromExternalConfiguration
    {
        private const string SampleConfig = @"<?xml version=""1.0"" encoding=""utf-8"" ?>
            <configuration>
              <configSections>
                <section name=""riakConfig"" type=""RiakClient.Config.RiakClusterConfiguration, RiakClient"" />
              </configSections>
              <riakConfig nodePollTime=""5000"" defaultRetryWaitTime=""200"" defaultRetryCount=""3"">
                <nodes>
                  <node name=""node1"" hostAddress=""host1"" pbcPort=""8081"" poolSize=""5"" />
                  <node name=""node2"" hostAddress=""host2"" pbcPort=""8081"" poolSize=""6""
                        networkReadTimeout=""5000"" networkWriteTimeout=""5000"" networkConnectTimeout=""5000"" />
                </nodes>
              </riakConfig>
            </configuration>
            ";

        [Test]
        public void ConfigurationLoadsProperly()
        {
            int twoHundredMillis = 200;
            int fourSecsAsMillis = 4000;
            int fiveSecsAsMillis = 5000;

            var fileName = Path.GetTempFileName();
            try
            {
                File.WriteAllText(fileName, SampleConfig);

                var config = RiakClusterConfiguration.LoadFromConfig("riakConfig", fileName);
                config.DefaultRetryCount.ShouldEqual(3);
                config.DefaultRetryWaitTime.ShouldEqual((Timeout)twoHundredMillis);
                config.NodePollTime.ShouldEqual((Timeout)fiveSecsAsMillis);
                config.RiakNodes.Count.ShouldEqual(2);

                IRiakNodeConfiguration node1 = config.RiakNodes[0];
                node1.Name.ShouldEqual("node1");
                node1.HostAddress.ShouldEqual("host1");
                node1.PbcPort.ShouldEqual(8081);
                node1.PoolSize.ShouldEqual(5);
                node1.NetworkConnectTimeout.ShouldEqual((Timeout)fourSecsAsMillis);
                node1.NetworkReadTimeout.ShouldEqual((Timeout)fourSecsAsMillis);
                node1.NetworkWriteTimeout.ShouldEqual((Timeout)fourSecsAsMillis);

                IRiakNodeConfiguration node2 = config.RiakNodes[1];
                node2.Name.ShouldEqual("node2");
                node2.HostAddress.ShouldEqual("host2");
                node2.PbcPort.ShouldEqual(8081);
                node2.PoolSize.ShouldEqual(6);
                node2.NetworkConnectTimeout.ShouldEqual((Timeout)fiveSecsAsMillis);
                node2.NetworkReadTimeout.ShouldEqual((Timeout)fiveSecsAsMillis);
                node2.NetworkWriteTimeout.ShouldEqual((Timeout)fiveSecsAsMillis);
            }
            finally
            {
                File.Delete(fileName);
            }
        }
    }
}
