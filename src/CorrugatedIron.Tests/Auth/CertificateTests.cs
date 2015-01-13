﻿// Copyright (c) 2015 Basho Technologies, Inc.
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
using System.IO;
using System.Security.Cryptography.X509Certificates;
using CorrugatedIron.Config;
using CorrugatedIron.Util;
using NUnit.Framework;

namespace CorrugatedIron.Tests.X509
{
    [TestFixture]
    public class CertificateTests
    {
        private static readonly string testCertsDir;
        private static readonly string rootCaCertFile;
        private static readonly string riakUserClientCertFileRelativePath;
        private static readonly string riakUserClientCertFile;
        private const string riakUserClientCertSubject =
            @"E=riakuser@myorg.com, CN=riakuser, OU=Development, O=Basho Technologies, S=WA, C=US";

        static CertificateTests()
        {
            var currentDir = Environment.CurrentDirectory;
            string[] testCertsDirRelativePathAry = new string[] {
                "..", "..", "..", "..", "tools", "test-ca", "certs"
            };
            string testCertsDirRelativePath = Path.Combine(testCertsDirRelativePathAry);
            testCertsDir = Path.Combine(currentDir, testCertsDirRelativePath);

            rootCaCertFile = Path.Combine(testCertsDir, "cacert.pem");

            riakUserClientCertFileRelativePath = Path.Combine(testCertsDirRelativePath, "riakuser-client-cert.pfx");
            riakUserClientCertFile = Path.Combine(currentDir, riakUserClientCertFileRelativePath);
        }

        [TestFixtureSetUp]
        public void ImportTestCertificates()
        {
            if (MonoUtil.IsRunningOnMono)
            {
                Assert.Ignore("Running on Mono, X509 Certificate tests will be skipped.");
            }

            Assert.True(Directory.Exists(testCertsDir));

            /*
             * NB: the first time this is run, the user WILL get a popup asking if they should import the root
             *     cert. There is no way around this if the user is running as a normal user.
             */
            Assert.True(File.Exists(rootCaCertFile));
            var rootCaCert = new X509Certificate2(rootCaCertFile);

            Assert.True(File.Exists(riakUserClientCertFile));
            var riakUserClientCert = new X509Certificate2(riakUserClientCertFile);

            SaveToStore(rootCaCert, StoreName.Root);
            SaveToStore(riakUserClientCert, StoreName.My);
        }

        [Test]
        public void RiakConfigurationCanSpecifyX509Certificates()
        {
            var config = RiakClusterConfiguration.LoadFromConfig("riakConfiguration");
            Assert.IsNotNull(config);

            var authConfig = config.Authentication;
            Assert.IsNotNull(authConfig);

            Assert.AreEqual("riakpass", authConfig.Username);
            Assert.AreEqual("Test1234", authConfig.Password);
            Assert.AreEqual(riakUserClientCertFileRelativePath, authConfig.ClientCertificateFile);
            Assert.AreEqual(riakUserClientCertSubject, authConfig.ClientCertificateSubject);
        }

        private static void SaveToStore(X509Certificate2 cert, StoreName storeName)
        {
            X509Store x509Store = null;
            try
            {
                x509Store = new X509Store(storeName, StoreLocation.CurrentUser);
                x509Store.Open(OpenFlags.OpenExistingOnly | OpenFlags.ReadWrite);
                x509Store.Add(cert);
            }
            finally
            {
                x509Store.Close();
            }
        }
    }
}