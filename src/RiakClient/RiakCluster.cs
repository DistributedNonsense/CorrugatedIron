// <copyright file="RiakCluster.cs" company="Basho Technologies, Inc.">
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

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using RiakClient.Comms;
using RiakClient.Comms.LoadBalancing;
using RiakClient.Config;
using RiakClient.Messages;

namespace RiakClient
{
    public class RiakCluster : RiakEndPoint
    {
        private readonly RoundRobinStrategy loadBalancer;
        private readonly List<IRiakNode> nodes;
        private readonly ConcurrentQueue<IRiakNode> offlineNodes;
        private readonly TimeSpan nodePollTime;
        private readonly int defaultRetryCount;
        private bool disposing;

        public RiakCluster(IRiakClusterConfiguration clusterConfig, IRiakConnectionFactory connectionFactory)
        {
            nodePollTime = clusterConfig.NodePollTime;
            nodes = clusterConfig.RiakNodes.Select(rn =>
                new RiakNode(rn, clusterConfig.Authentication, connectionFactory)).Cast<IRiakNode>().ToList();
            loadBalancer = new RoundRobinStrategy();
            loadBalancer.Initialise(nodes);
            offlineNodes = new ConcurrentQueue<IRiakNode>();
            defaultRetryCount = clusterConfig.DefaultRetryCount;
            RetryWaitTime = clusterConfig.DefaultRetryWaitTime;

            // Task.Factory.StartNew(NodeMonitor);
        }

        protected override int DefaultRetryCount
        {
            get { return defaultRetryCount; }
        }

        /// <summary>
        /// Creates an instance of <see cref="IRiakClient"/> populated from from the configuration section
        /// specified by <paramref name="configSectionName"/>.
        /// </summary>
        /// <param name="configSectionName">The name of the configuration section to load the settings from.</param>
        /// <returns>A fully configured <see cref="IRiakEndPoint"/></returns>
        public static IRiakEndPoint FromConfig(string configSectionName)
        {
            return new RiakCluster(RiakClusterConfiguration.LoadFromConfig(configSectionName), new RiakConnectionFactory());
        }

        /// <summary>
        /// Creates an instance of <see cref="IRiakClient"/> populated from from the configuration section
        /// specified by <paramref name="configSectionName"/>.
        /// </summary>
        /// <param name="configSectionName">The name of the configuration section to load the settings from.</param>
        /// <param name="configFileName">The full path and name of the config file to load the configuration from.</param>
        /// <returns>A fully configured <see cref="IRiakEndPoint"/></returns>
        public static IRiakEndPoint FromConfig(string configSectionName, string configFileName)
        {
            return new RiakCluster(RiakClusterConfiguration.LoadFromConfig(configSectionName, configFileName), new RiakConnectionFactory());
        }

        protected override TRiakResult UseConnection<TRiakResult>(Func<IRiakConnection, TRiakResult> useFun,
            Func<ResultCode, string, bool, TRiakResult> onError, int retryAttempts)
        {
            if (retryAttempts < 0)
            {
                return onError(ResultCode.NoRetries, "Unable to access a connection on the cluster.", false);
            }

            if (disposing)
            {
                return onError(ResultCode.ShuttingDown, "System currently shutting down", true);
            }

            var node = loadBalancer.SelectNode();
            if (node != null)
            {
                var result = node.UseConnection(useFun);
                if (!result.IsSuccess)
                {
                    TRiakResult nextResult = null;
                    if (result.ResultCode == ResultCode.NoConnections)
                    {
                        Thread.Sleep(RetryWaitTime);
                        nextResult = UseConnection(useFun, onError, retryAttempts - 1);
                    }
                    else if (result.ResultCode == ResultCode.CommunicationError)
                    {
                        if (result.NodeOffline)
                        {
                            DeactivateNode(node);
                        }

                        Thread.Sleep(RetryWaitTime);
                        nextResult = UseConnection(useFun, onError, retryAttempts - 1);
                    }

                    // if the next result is successful then return that
                    if (nextResult != null && nextResult.IsSuccess)
                    {
                        return nextResult;
                    }

                    // otherwise we'll return the result that we had at this call to make sure that
                    // the correct/initial error is shown
                    return onError(result.ResultCode, result.ErrorMessage, result.NodeOffline);
                }
                return (TRiakResult)result;
            }
            return onError(ResultCode.ClusterOffline, "Unable to access functioning Riak node", true);
        }

        public override RiakResult<IEnumerable<TResult>> UseDelayedConnection<TResult>(Func<IRiakConnection, Action, RiakResult<IEnumerable<TResult>>> useFun, int retryAttempts)
        {
            if (retryAttempts < 0)
            {
                return RiakResult<IEnumerable<TResult>>.Error(ResultCode.NoRetries, "Unable to access a connection on the cluster.", false);
            }

            if (disposing)
            {
                return RiakResult<IEnumerable<TResult>>.Error(ResultCode.ShuttingDown, "System currently shutting down", true);
            }

            var node = loadBalancer.SelectNode();

            if (node != null)
            {
                var result = node.UseDelayedConnection(useFun);
                if (!result.IsSuccess)
                {
                    if (result.ResultCode == ResultCode.NoConnections)
                    {
                        Thread.Sleep(RetryWaitTime);
                        return UseDelayedConnection(useFun, retryAttempts - 1);
                    }

                    if (result.ResultCode == ResultCode.CommunicationError)
                    {
                        if (result.NodeOffline)
                        {
                            DeactivateNode(node);
                        }

                        Thread.Sleep(RetryWaitTime);
                        return UseDelayedConnection(useFun, retryAttempts - 1);
                    }
                }
                return result;
            }
            return RiakResult<IEnumerable<TResult>>.Error(ResultCode.ClusterOffline, "Unable to access functioning Riak node", true);
        }

        private void DeactivateNode(IRiakNode node)
        {
            lock (node)
            {
                if (!offlineNodes.Contains(node))
                {
                    loadBalancer.RemoveNode(node);
                    offlineNodes.Enqueue(node);
                }
            }
        }

        private void NodeMonitor()
        {
            while (!disposing)
            {
                var deadNodes = new List<IRiakNode>();
                IRiakNode node = null;
                while (offlineNodes.TryDequeue(out node) && !disposing)
                {
                    var result = node.UseConnection(c => c.PbcWriteRead(MessageCode.RpbPingReq, MessageCode.RpbPingResp));

                    if (result.IsSuccess)
                    {
                        loadBalancer.AddNode(node);
                    }
                    else
                    {
                        deadNodes.Add(node);
                    }
                }

                if (!disposing)
                {
                    foreach (var deadNode in deadNodes)
                    {
                        offlineNodes.Enqueue(deadNode);
                    }

                    Thread.Sleep(nodePollTime);
                }
            }
        }

        public override void Dispose()
        {
            disposing = true;

            nodes.ForEach(n => n.Dispose());
        }
    }
}
