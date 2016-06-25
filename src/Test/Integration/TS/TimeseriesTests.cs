namespace Test.Integration.TS
{
    using System;
    using System.Linq;
    using NUnit.Framework;
    using RiakClient;
    using RiakClient.Commands.TS;
    using RiakClient.Util;

    public class TimeseriesTests : TestBase
    {
        private static readonly RiakString Table = new RiakString("GeoCheckin");

        private static readonly long NowMs = 1443796900987;
        private static readonly DateTime Now = DateTimeUtil.FromUnixTimeMillis(NowMs);
        private static readonly DateTime FiveMinsAgo = Now.AddMinutes(-5);
        private static readonly DateTime TenMinsAgo = FiveMinsAgo.AddMinutes(-5);
        private static readonly DateTime FifteenMinsAgo = TenMinsAgo.AddMinutes(-5);
        private static readonly DateTime TwentyMinsAgo = FifteenMinsAgo.AddMinutes(-5);

        private static readonly Cell[] Cells0 = new Cell[]
        {
            new Cell<string>("hash1"),
            new Cell<string>("user2"),
            new Cell<DateTime>(TwentyMinsAgo),
            new Cell<string>("hurricane"),
            new Cell<double>(82.3)
        };

        private static readonly Cell[] Cells1 = new Cell[]
        {
            new Cell<string>("hash1"),
            new Cell<string>("user2"),
            new Cell<DateTime>(FifteenMinsAgo),
            new Cell<string>("rain"),
            new Cell<double>(79.0)
        };

        private static readonly Cell[] Cells2 = new Cell[]
        {
            new Cell<string>("hash1"),
            new Cell<string>("user2"),
            new Cell<DateTime>(FiveMinsAgo),
            new Cell<string>("wind"),
            Cell.Null
        };

        private static readonly Cell[] Cells3 = new Cell[]
        {
            new Cell<string>("hash1"),
            new Cell<string>("user2"),
            new Cell<DateTime>(Now),
            new Cell<string>("snow"),
            new Cell<double>(20.1)
        };

        private static readonly Row[] Rows = new Row[]
        {
            new Row(Cells0),
            new Row(Cells1),
            new Row(Cells2),
            new Row(Cells3)
        };

        private static readonly Cell[] KeyCells1 = new Cell[]
        {
            new Cell<string>("hash1"),
            new Cell<string>("user2"),
            new Cell<DateTime>(FifteenMinsAgo)
        };

        private static readonly Row KeyToDelete = new Row(KeyCells1);

        private static readonly Cell[] KeyCells = new Cell[]
        {
            new Cell<string>("hash1"),
            new Cell<string>("user2"),
            new Cell<DateTime>(FiveMinsAgo)
        };

        private static readonly Row Key = new Row(KeyCells);
        private static readonly Row RowToRestore = new Row(Cells1);

        private static readonly Column[] Columns = new[]
        {
            new Column("geohash",     ColumnType.Varchar),
            new Column("user",        ColumnType.Varchar),
            new Column("time",        ColumnType.Timestamp),
            new Column("weather",     ColumnType.Varchar),
            new Column("temperature", ColumnType.Double)
        };

        /*
         * TODO NB: timeseries does not work with security yet
         */
        public TimeseriesTests()
            : base(auth: false)
        {
        }

        public override void TestFixtureSetUp()
        {
            var cmd = new Store.Builder()
                    .WithTable(Table)
                    .WithColumns(Columns)
                    .WithRows(Rows)
                    .Build();

            RiakResult rslt = client.Execute(cmd);
            Assert.IsTrue(rslt.IsSuccess, rslt.ErrorMessage);
        }

        [Test]
        public void Get_Returns_One_Row()
        {
            var cmd = new Get.Builder()
                .WithTable(Table)
                .WithKey(Key)
                .Build();

            RiakResult rslt = client.Execute(cmd);
            Assert.IsTrue(rslt.IsSuccess, rslt.ErrorMessage);

            Get get = (Get)cmd;
            GetResponse rsp = get.Response;

            Row[] rows = rsp.Value.ToArray();
            Assert.AreEqual(1, rows.Length);

            Cell[] cells = rows[0].Cells.ToArray();
            CollectionAssert.AreEqual(Cells2, cells);
        }

        [Test]
        public void Delete_One_Row()
        {
            var cmd = new Delete.Builder()
                    .WithTable(Table)
                    .WithKey(KeyToDelete)
                    .Build();

            RiakResult rslt = client.Execute(cmd);
            Assert.IsTrue(rslt.IsSuccess, rslt.ErrorMessage);
            Delete del = (Delete)cmd;
            Assert.IsFalse(del.Response.NotFound);

            cmd = new Get.Builder()
                .WithTable(Table)
                .WithKey(KeyToDelete)
                .Build();

            rslt = client.Execute(cmd);
            Assert.IsTrue(rslt.IsSuccess, rslt.ErrorMessage);
            Get get = (Get)cmd;
            Assert.IsTrue(get.Response.NotFound);

            cmd = new Store.Builder()
                    .WithTable(Table)
                    .WithRow(RowToRestore)
                    .Build();

            rslt = client.Execute(cmd);
            Assert.IsTrue(rslt.IsSuccess, rslt.ErrorMessage);
        }

        [Test]
        public void Query_Create_Table()
        {
            string tableName = Guid.NewGuid().ToString();
            string sqlFmt = string.Format(
                @"CREATE TABLE RTS-{0} (geohash varchar not null,
                                        user varchar not null,
                                        time timestamp not null,
                                        weather varchar not null,
                                        temperature double,
                  PRIMARY KEY((geohash, user, quantum(time, 15, m)), geohash, user, time))",
                tableName);
            var cmd = new Query.Builder()
                .WithTable(tableName)
                .WithQuery(sqlFmt)
                .Build();

            RiakResult rslt = client.Execute(cmd);
            Assert.IsTrue(rslt.IsSuccess, rslt.ErrorMessage);

            Query q = (Query)cmd;
            QueryResponse rsp = q.Response;
            Assert.IsFalse(rsp.NotFound);
            CollectionAssert.IsEmpty(rsp.Columns);
            CollectionAssert.IsEmpty(rsp.Value);
        }

        [Test]
        public void Query_Table_Description()
        {
            var cmd = new Query.Builder()
                .WithTable("GeoCheckin")
                .WithQuery("DESCRIBE GeoCheckin")
                .Build();

            RiakResult rslt = client.Execute(cmd);
            Assert.IsTrue(rslt.IsSuccess, rslt.ErrorMessage);

            Query q = (Query)cmd;
            QueryResponse rsp = q.Response;
            Assert.IsFalse(rsp.NotFound);
            CollectionAssert.IsNotEmpty(rsp.Columns);
            CollectionAssert.IsNotEmpty(rsp.Value);

            Assert.AreEqual(Columns.Length, rsp.Columns.Count());
            Assert.AreEqual(Columns.Length, rsp.Value.Count());
            foreach (Row row in rsp.Value)
            {
                Assert.AreEqual(Columns.Length, row.Cells.Count());
            }
        }

        [Test]
        public void Query_Matching_No_Data()
        {
            var qry = "SELECT * from GeoCheckin WHERE time > 0 and time < 10 and geohash = 'hash1' and user = 'user1'";
            var cmd = new Query.Builder()
                .WithTable("GeoCheckin")
                .WithQuery(qry)
                .Build();

            RiakResult rslt = client.Execute(cmd);
            Assert.IsTrue(rslt.IsSuccess, rslt.ErrorMessage);

            Query q = (Query)cmd;
            QueryResponse rsp = q.Response;
            Assert.IsFalse(rsp.NotFound);

            CollectionAssert.IsEmpty(rsp.Columns);
            CollectionAssert.IsEmpty(rsp.Value);
        }

        [Test]
        public void Query_Matching_Some_Data()
        {
            var qfmt = "SELECT * FROM GeoCheckin WHERE time > {0} and time < {1} and geohash = 'hash1' and user = 'user2'";
            var qry = string.Format(
                qfmt,
                DateTimeUtil.ToUnixTimeMillis(TenMinsAgo),
                DateTimeUtil.ToUnixTimeMillis(Now));

            var cmd = new Query.Builder()
                .WithTable("GeoCheckin")
                .WithQuery(qry)
                .Build();

            RiakResult rslt = client.Execute(cmd);
            Assert.IsTrue(rslt.IsSuccess, rslt.ErrorMessage);

            Query q = (Query)cmd;
            QueryResponse rsp = q.Response;
            Assert.IsFalse(rsp.NotFound);

            Assert.AreEqual(Columns.Length, rsp.Columns.Count());
            Assert.AreEqual(1, rsp.Value.Count());
        }

        [Test]
        public void Query_Matching_All_Data()
        {
            var qfmt = "SELECT * FROM GeoCheckin WHERE time >= {0} and time <= {1} and geohash = 'hash1' and user = 'user2'";
            var qry = string.Format(
                qfmt,
                DateTimeUtil.ToUnixTimeMillis(TwentyMinsAgo),
                DateTimeUtil.ToUnixTimeMillis(Now));

            var cmd = new Query.Builder()
                .WithTable("GeoCheckin")
                .WithQuery(qry)
                .Build();

            RiakResult rslt = client.Execute(cmd);
            Assert.IsTrue(rslt.IsSuccess, rslt.ErrorMessage);

            Query q = (Query)cmd;
            QueryResponse rsp = q.Response;
            Assert.IsFalse(rsp.NotFound);

            Assert.AreEqual(Columns.Length, rsp.Columns.Count());
            Assert.AreEqual(Rows.Length, rsp.Value.Count());
        }

        [Test]
        public void Query_Streaming_Matching_All_Data()
        {
            var qfmt = "SELECT * FROM GeoCheckin WHERE time >= {0} and time <= {1} and geohash = 'hash1' and user = 'user2'";
            var qry = string.Format(
                qfmt,
                DateTimeUtil.ToUnixTimeMillis(TwentyMinsAgo),
                DateTimeUtil.ToUnixTimeMillis(Now));

            ushort i = 0;
            Action<QueryResponse> cb = (QueryResponse qr) =>
            {
                i++;
                Assert.AreEqual(Columns.Length, qr.Columns.Count());
                CollectionAssert.IsNotEmpty(qr.Value);
            };

            var cmd = new Query.Builder()
                .WithTable("GeoCheckin")
                .WithQuery(qry)
                .WithCallback(cb)
                .Build();

            RiakResult rslt = client.Execute(cmd);
            Assert.IsTrue(rslt.IsSuccess, rslt.ErrorMessage);

            Query q = (Query)cmd;
            QueryResponse rsp = q.Response;
            Assert.IsFalse(rsp.NotFound);
            Assert.Greater(i, 0);
        }

        [Test]
        public void List_Keys_In_Table()
        {
            TestFixtureSetUp();

            int i = 0;
            Action<ListKeysResponse> cb = (ListKeysResponse qr) =>
            {
                i += qr.Value.Count();
            };

            var cmd = new ListKeys.Builder()
                .WithTable("GeoCheckin")
                .WithCallback(cb)
                .Build();

            RiakResult rslt = client.Execute(cmd);
            Assert.IsTrue(rslt.IsSuccess, rslt.ErrorMessage);

            ListKeys lk = (ListKeys)cmd;
            ListKeysResponse rsp = lk.Response;
            Assert.IsFalse(rsp.NotFound);
            Assert.AreEqual(4, i);
        }
    }
}
