namespace RiakClientTests.Live.Search
{
    using System;
    using System.Linq;
    using NUnit.Framework;
    using RiakClient;
    using RiakClient.Models.Search;

    [TestFixture, IntegrationTest, SkipMono]
    public class TestSearchAdminOperations : LiveRiakConnectionTestBase
    {
        [SetUp]
        public override void SetUp()
        {
            base.SetUp();
        }

        [Test]
        public void TestFetchAndStoreDefaultSchema()
        {
            // Get Default schema
            var getSchemaResult = Client.GetSearchSchema("_yz_default");
            Assert.True(getSchemaResult.IsSuccess, getSchemaResult.ErrorMessage);

            var defaultSchema = getSchemaResult.Value;

            defaultSchema.Name.ShouldNotBeNull();
            defaultSchema.Content.ShouldNotBeNull();

            // Store as new schema
            var newSchemaName = "test_schema" + Random.Next();
            const string randomComment = "<!-- Random Comment -->";
            var newSchemaContent = defaultSchema.Content + randomComment;
            var newSchema = new SearchSchema(newSchemaName, newSchemaContent);

            var putSchemaResult = Client.PutSearchSchema(newSchema);
            putSchemaResult.IsSuccess.ShouldBeTrue(putSchemaResult.ErrorMessage);

            // Fetch new schema and compare
            var getSchemaResult2 = Client.GetSearchSchema(newSchemaName);
            var fetchedNewSchema = getSchemaResult2.Value;

            Assert.AreEqual(newSchemaName, fetchedNewSchema.Name);
            Assert.AreNotEqual(defaultSchema.Content, fetchedNewSchema.Content); // Should differ by the added comment
            Assert.AreEqual(newSchemaContent, fetchedNewSchema.Content);
            Assert.IsTrue(fetchedNewSchema.Content.Contains(randomComment));
        }

        [Test]
        public void TestStoreAndFetchIndex()
        {
            var indexName = "index" + Random.Next();
            var index = new SearchIndex(indexName, RiakConstants.Defaults.YokozunaIndex.DefaultSchemaName, (NVal)2);

            var putIndexResult = Client.PutSearchIndex(index);

            Assert.True(putIndexResult.IsSuccess, "Index Not Created: {0}", putIndexResult.ErrorMessage);
            Func<RiakResult<SearchIndexResult>> func = () => Client.GetSearchIndex(indexName);
            var getIndexResult = func.WaitUntil();

            Assert.True(getIndexResult.IsSuccess, "Index Not Fetched: {0}", getIndexResult.ErrorMessage);
            Assert.AreEqual(1, getIndexResult.Value.Indexes.Count);
            var fetchedIndex = getIndexResult.Value.Indexes.First();
            Assert.AreEqual(indexName, fetchedIndex.Name);
            Assert.AreEqual(2, fetchedIndex.NVal);
        }


        [Test]
        public void TestDeleteIndex()
        {
            var indexName = "index" + Random.Next();
            var index = new SearchIndex(indexName);
            var putIndexResult = Client.PutSearchIndex(index);
            Assert.True(putIndexResult.IsSuccess, "Index Not Created: {0}", putIndexResult.ErrorMessage);

            // Wait until index can be fetched, or else you can run into a race condition where the index is not found on another node. 
            Func<RiakResult<SearchIndexResult>> fetchIndex = () => Client.GetSearchIndex(indexName);
            var fetchIndexResult = fetchIndex.WaitUntil();
            Assert.True(fetchIndexResult.IsSuccess, "Index Not Found After Creation: {0}", fetchIndexResult.ErrorMessage);

            Func<RiakResult> deleteIndex = () => Client.DeleteSearchIndex(indexName);
            var deleteIndexResult = deleteIndex.WaitUntil();

            Assert.True(deleteIndexResult.IsSuccess, "Index Not Deleted: {0}", deleteIndexResult.ErrorMessage);

        }
    }
}
