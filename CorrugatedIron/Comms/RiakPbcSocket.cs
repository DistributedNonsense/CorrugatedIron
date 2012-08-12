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
using System.IO;
using System.Net;
using System.Net.Sockets;
using CorrugatedIron.Exceptions;
using CorrugatedIron.Extensions;
using CorrugatedIron.Messages;
using ProtoBuf;

namespace CorrugatedIron.Comms
{
    internal class RiakPbcSocket : IDisposable
    {
        private readonly string _server;
        private readonly int _port;
        private readonly int _receiveTimeout;
        private readonly int _sendTimeout;
        private static readonly Dictionary<MessageCode, Type> MessageCodeToTypeMap;
        private static readonly Dictionary<Type, MessageCode> TypeToMessageCodeMap;
        private Socket _pbcSocket;

        private Socket PbcSocket
        {
            get
            {
                if(_pbcSocket == null)
                {
                    var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                    socket.NoDelay = true;
                    socket.Connect(_server, _port);

                    if(!socket.Connected)
                    {
                        throw new RiakException("Unable to connect to remote server: {0}:{1}".Fmt(_server, _port));
                    }
                    socket.ReceiveTimeout = _receiveTimeout;
                    socket.SendTimeout = _sendTimeout;

                    _pbcSocket = socket;

                }
                return _pbcSocket;
            }
        }

        public bool IsConnected
        {
            get { return _pbcSocket != null && _pbcSocket.Connected; }
        }

        static RiakPbcSocket()
        {
            MessageCodeToTypeMap = new Dictionary<MessageCode, Type>
            {
                { MessageCode.ErrorResp, typeof(RpbErrorResp) },
                { MessageCode.PingReq, typeof(RpbPingReq) },
                { MessageCode.PingResp, typeof(RpbPingResp) },
                { MessageCode.GetClientIdReq, typeof(RpbGetClientIdReq) },
                { MessageCode.GetClientIdResp, typeof(RpbGetClientIdResp) },
                { MessageCode.SetClientIdReq, typeof(RpbSetClientIdReq) },
                { MessageCode.SetClientIdResp, typeof(RpbSetClientIdResp) },
                { MessageCode.GetServerInfoReq, typeof(RpbGetServerInfoReq) },
                { MessageCode.GetServerInfoResp, typeof(RpbGetServerInfoResp) },
                { MessageCode.GetReq, typeof(RpbGetReq) },
                { MessageCode.GetResp, typeof(RpbGetResp) },
                { MessageCode.PutReq, typeof(RpbPutReq) },
                { MessageCode.PutResp, typeof(RpbPutResp) },
                { MessageCode.DelReq, typeof(RpbDelReq) },
                { MessageCode.DelResp, typeof(RpbDelResp) },
                { MessageCode.ListBucketsReq, typeof(RpbListBucketsReq) },
                { MessageCode.ListBucketsResp, typeof(RpbListBucketsResp) },
                { MessageCode.ListKeysReq, typeof(RpbListKeysReq) },
                { MessageCode.ListKeysResp, typeof(RpbListKeysResp) },
                { MessageCode.GetBucketReq, typeof(RpbGetBucketReq) },
                { MessageCode.GetBucketResp, typeof(RpbGetBucketResp) },
                { MessageCode.SetBucketReq, typeof(RpbSetBucketReq) },
                { MessageCode.SetBucketResp, typeof(RpbSetBucketResp) },
                { MessageCode.MapRedReq, typeof(RpbMapRedReq) },
                { MessageCode.MapRedResp, typeof(RpbMapRedResp) }
            };

            TypeToMessageCodeMap = new Dictionary<Type, MessageCode>();

            foreach(var item in MessageCodeToTypeMap)
            {
                TypeToMessageCodeMap.Add(item.Value, item.Key);
            }
        }

        public RiakPbcSocket(string server, int port, int receiveTimeout, int sendTimeout)
        {
            _server = server;
            _port = port;
            _receiveTimeout = receiveTimeout;
            _sendTimeout = sendTimeout;
        }

        public void Write<T>(T message)
        {
            const int sizeSize = sizeof(int);
            const int codeSize = sizeof(byte);
            const int headerSize = sizeSize + codeSize;
            const int sendBufferSize = 1024 * 16;
            byte[] messageBody;
            long messageLength = 0;

            using(var memStream = new MemoryStream())
            {
                // add a buffer to the start of the array to put the size and message code
                memStream.Position += headerSize;
                Serializer.Serialize(memStream, message);
                messageBody = memStream.GetBuffer();
                messageLength = memStream.Position;
            }

            // check to make sure something was written, otherwise we'll have to create a new array
            if(messageLength == headerSize)
            {
                messageBody = new byte[headerSize];
            }

            var messageCode = TypeToMessageCodeMap[typeof(T)];
            var size = BitConverter.GetBytes(IPAddress.HostToNetworkOrder((int)messageLength - headerSize + 1));
            Array.Copy(size, messageBody, sizeSize);
            messageBody[sizeSize] = (byte)messageCode;

            int bytesToSend = (int)messageLength;
            int position = 0;

            while (bytesToSend > 0)
            {
                int sent = PbcSocket.Send(messageBody, position, bytesToSend <= sendBufferSize ? bytesToSend : sendBufferSize, SocketFlags.None);
                if (sent == 0)
                {
                    throw new RiakException("Failed to send data to server - Timed Out: {0}:{1}".Fmt(_server, _port));
                }
                position += sent;
                bytesToSend -= sent;
            }
        }

        public T Read<T>() where T : new()
        {
            var header = ReceiveAll(new byte[5]);

            var size = IPAddress.NetworkToHostOrder(BitConverter.ToInt32(header, 0));

            var messageCode = (MessageCode)header[sizeof(int)];
            if(messageCode == MessageCode.ErrorResp)
            {
                var error = DeserializeInstance<RpbErrorResp>(size);
                throw new RiakException(error.ErrorCode, error.ErrorMessage.FromRiakString());
            }

            if(!MessageCodeToTypeMap.ContainsKey(messageCode))
            {
                throw new RiakInvalidDataException((byte)messageCode);
            }
#if DEBUG
            // This message code validation is here to make sure that the caller
            // is getting exactly what they expect. This "could" be removed from
            // production code, but it's a good thing to have in here for dev.
            if(MessageCodeToTypeMap[messageCode] != typeof(T))
            {
                throw new InvalidOperationException(string.Format("Attempt to decode message to type '{0}' when received type '{1}'.", typeof(T).Name, MessageCodeToTypeMap[messageCode].Name));
            }
#endif
            return DeserializeInstance<T>(size);
        }

        private byte[] ReceiveAll(byte[] resultBuffer)
        {
            int totalBytesReceived = 0;
            int lengthToReceive = resultBuffer.Length;
            while(lengthToReceive > 0)
            {
                int bytesReceived = PbcSocket.Receive(resultBuffer, totalBytesReceived, lengthToReceive, 0);
                if(bytesReceived == 0)
                {
                    throw new RiakException("Unable to read data from the source stream - Timed Out.");
                }
                totalBytesReceived += bytesReceived;
                lengthToReceive -= bytesReceived;
            }
            return resultBuffer;
        }

        private T DeserializeInstance<T>(int size)
            where T : new()
        {
            if(size <= 1)
            {
                return new T();
            }

            var resultBuffer = ReceiveAll(new byte[size - 1]);

            using(var memStream = new MemoryStream(resultBuffer))
            {
                return Serializer.Deserialize<T>(memStream);
            }
        }

        public void Disconnect()
        {
            if(_pbcSocket != null)
            {
                _pbcSocket.Disconnect(false);
                _pbcSocket.Dispose();
                _pbcSocket = null;
            }
        }

        public void Dispose()
        {
            Disconnect();
        }
    }
}
