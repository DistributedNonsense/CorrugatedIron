﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CorrugatedIron.Models;
using CorrugatedIron.Tests.Extensions;
using NUnit.Framework;

namespace CorrugatedIron.Tests.Models
{
    [TestFixture]
    public class RiakObjectTests
    {
        private const string Bucket = "bucket";
        private const string Key = "key";

        [Test]
        public void ToRiakObjectIdProducesAValidRiakObjectId()
        {
            var riakObject = new RiakObject(Bucket, Key, "value");
            var riakObjectId = riakObject.ToRiakObjectId();

            riakObjectId.Bucket.ShouldEqual(Bucket);
            riakObjectId.Key.ShouldEqual(Key);
        }
        
        [Test]
        public void RiakIndexNameManglingIsHandledAutomatically()
        {
            var riakObject = new RiakObject(Bucket, Key, "value");
            riakObject.AddBinIndex("name", "jeremiah");
            riakObject.AddBinIndex("state_bin", "oregon");
            riakObject.AddIntIndex("age", 32);
            riakObject.AddIntIndex("cats_int", 2);
            
            riakObject.Indexes.Keys.Contains("name").ShouldBeFalse();
            riakObject.Indexes.Keys.Contains("age").ShouldBeFalse();
            riakObject.Indexes.Keys.Contains("state").ShouldBeFalse();
            riakObject.Indexes.Keys.Contains("state_bin_bin").ShouldBeFalse();
            riakObject.Indexes.Keys.Contains("cats").ShouldBeFalse();
            riakObject.Indexes.Keys.Contains("cats_int_int").ShouldBeFalse();
            
            riakObject.Indexes.Keys.Contains("name_bin").ShouldBeTrue();
            riakObject.Indexes.Keys.Contains("age_int").ShouldBeTrue();
            riakObject.Indexes.Keys.Contains("state_bin").ShouldBeTrue();
            riakObject.Indexes.Keys.Contains("cats_int").ShouldBeTrue();
        }
    }
}
