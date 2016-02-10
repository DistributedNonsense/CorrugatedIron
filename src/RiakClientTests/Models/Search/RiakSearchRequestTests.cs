namespace RiakClientTests.Models.Search
{
    using NUnit.Framework;
    using RiakClient.Models.Search;

    [TestFixture, UnitTest]
    public class RiakSearchRequestTests
    {
        [Test]
        public void UsingFluentQueryProducesSameQueryAsString()
        {
            string index = "index";
            string field = "data_s";
            string search = "frazzle";
            string solrQuery = string.Format("{0}:{1}", field, search);

            var fluentSearch = new RiakFluentSearch(index, field).Search(search).Build();
            var s1 = new RiakSearchRequest { Query = fluentSearch };
            var s2 = new RiakSearchRequest(index, solrQuery);

            Assert.AreEqual(s1, s2);
        }

        [Test]
        public void UsingFluentQueryWithFilterProducesSameQueryAsString()
        {
            string index = "index";
            string field = "data_s";
            string search = "frazzle";
            string solrQuery = string.Format("{0}:{1}", field, search);
            string solrFilter = string.Format("{0}:[10 TO 20]", field);

            var fluentSearch = new RiakFluentSearch(index, field).Search(search).Build();
            var fluentFilter = new RiakFluentSearch(index, field).Between("10", "20", true).Build();
            var s1 = new RiakSearchRequest { Query = fluentSearch, Filter = fluentFilter };
            var s2 = new RiakSearchRequest(index, solrQuery, solrFilter);

            Assert.AreEqual(s1, s2);
        }
    }
}
