﻿// Copyright (c) Microsoft Corporation
// All rights reserved.
// 
// Licensed under the Apache License, Version 2.0 (the "License"); you may not
// use this file except in compliance with the License.  You may obtain a copy
// of the License at http://www.apache.org/licenses/LICENSE-2.0
// 
// THIS CODE IS PROVIDED *AS IS* BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY
// KIND, EITHER EXPRESS OR IMPLIED, INCLUDING WITHOUT LIMITATION ANY IMPLIED
// WARRANTIES OR CONDITIONS OF TITLE, FITNESS FOR A PARTICULAR PURPOSE,
// MERCHANTABLITY OR NON-INFRINGEMENT.
// 
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.

namespace Microsoft.HBase.Client.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Text;
    using Microsoft.HBase.Client.Filters;
    using Microsoft.HBase.Client.Tests.Utilities;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using org.apache.hadoop.hbase.rest.protobuf.generated;

    // ReSharper disable InconsistentNaming

    [TestClass]
    public class FilterTests : DisposableContextSpecification
    {
        private const string TableNamePrefix = "marlintest";

        private const string ColumnFamilyName1 = "first";
        private const string ColumnFamilyName2 = "second";
        private const string LineNumberColumnName = "line";
        private const string ColumnNameA = "a";
        private const string ColumnNameB = "b";

        private readonly List<FilterTestRecord> _allExpectedRecords = new List<FilterTestRecord>();

        private ClusterCredentials _credentials;
        private readonly Encoding _encoding = Encoding.UTF8;
        private string _tableName;
        private TableSchema _tableSchema;

        protected override void Context()
        {
            _credentials = ClusterCredentialsFactory.CreateFromFile(@".\credentials.txt");
            var client = new HBaseClient(_credentials);

            // ensure tables from previous tests are cleaned up
            TableList tables = client.ListTables();
            foreach (string name in tables.name)
            {
                if (name.StartsWith(TableNamePrefix, StringComparison.Ordinal))
                {
                    client.DeleteTable(name);
                }
            }

            AddTable();
            PopulateTable();
        }

        [TestCleanup]
        public override void TestCleanup()
        {
            var client = new HBaseClient(_credentials);
            client.DeleteTable(_tableName);
        }

        [TestMethod]
        [TestCategory(TestRunMode.CheckIn)]
        public void When_I_Scan_all_I_get_the_expected_results()
        {
            var client = new HBaseClient(_credentials);
            var scan = new Scanner();
            ScannerInformation scanInfo = client.CreateScanner(_tableName, scan);

            List<FilterTestRecord> actualRecords = RetrieveResults(scanInfo).ToList();

            actualRecords.ShouldContainOnly(_allExpectedRecords);
        }

        [TestMethod]
        [TestCategory(TestRunMode.CheckIn)]
        public void When_I_Scan_with_a_ColumnCountGetFilter_I_get_the_expected_results()
        {
            // B column should not be returned, so set the value to null.
            List<FilterTestRecord> expectedRecords = (from r in _allExpectedRecords select r.WithBValue(null)).ToList();

            var client = new HBaseClient(_credentials);
            var scanner = new Scanner();
            var filter = new ColumnCountGetFilter(2);
            scanner.filter = filter.ToEncodedString();
            ScannerInformation scanInfo = client.CreateScanner(_tableName, scanner);

            List<FilterTestRecord> actualRecords = RetrieveResults(scanInfo).ToList();
            actualRecords.ShouldContainOnly(expectedRecords);
        }


        [TestMethod]
        [TestCategory(TestRunMode.CheckIn)]
        public void When_I_Scan_with_a_ColumnPaginationFilter_I_get_the_expected_results()
        {
            // only grabbing the LineNumber column with (1, 1)
            List<FilterTestRecord> expectedRecords = (from r in _allExpectedRecords select r.WithAValue(null).WithBValue(null)).ToList();

            var client = new HBaseClient(_credentials);
            var scanner = new Scanner();
            var filter = new ColumnPaginationFilter(1, 1);
            scanner.filter = filter.ToEncodedString();
            ScannerInformation scanInfo = client.CreateScanner(_tableName, scanner);

            List<FilterTestRecord> actualRecords = RetrieveResults(scanInfo).ToList();
            actualRecords.ShouldContainOnly(expectedRecords);
        }

        [TestMethod]
        [TestCategory(TestRunMode.CheckIn)]
        public void When_I_Scan_with_a_ColumnPrefixFilter_I_get_the_expected_results()
        {
            List<FilterTestRecord> expectedRecords = (from r in _allExpectedRecords select r.WithAValue(null).WithBValue(null)).ToList();

            var client = new HBaseClient(_credentials);
            var scanner = new Scanner();
            var filter = new ColumnPrefixFilter(Encoding.UTF8.GetBytes(LineNumberColumnName));
            scanner.filter = filter.ToEncodedString();
            ScannerInformation scanInfo = client.CreateScanner(_tableName, scanner);

            List<FilterTestRecord> actualRecords = RetrieveResults(scanInfo).ToList();
            actualRecords.ShouldContainOnly(expectedRecords);
        }

        [TestMethod]
        [TestCategory(TestRunMode.CheckIn)]
        public void When_I_Scan_with_a_ColumnRangeFilter_I_get_the_expected_results()
        {
            List<FilterTestRecord> expectedRecords = (from r in _allExpectedRecords select r.WithLineNumberValue(0).WithBValue(null)).ToList();

            var client = new HBaseClient(_credentials);
            var scanner = new Scanner();
            var filter = new ColumnRangeFilter(Encoding.UTF8.GetBytes(ColumnNameA), true, Encoding.UTF8.GetBytes(ColumnNameB), false);
            scanner.filter = filter.ToEncodedString();
            ScannerInformation scanInfo = client.CreateScanner(_tableName, scanner);

            List<FilterTestRecord> actualRecords = RetrieveResults(scanInfo).ToList();
            actualRecords.ShouldContainOnly(expectedRecords);
        }

        [TestMethod]
        [TestCategory(TestRunMode.CheckIn)]
        public void When_I_Scan_with_a_DependentColumnFilter_and_a_BinaryComparator_with_the_operator_equal_I_get_the_expected_results()
        {
            List<FilterTestRecord> expectedRecords = (from r in _allExpectedRecords where r.LineNumber == 1 select r).ToList();

            var client = new HBaseClient(_credentials);
            var scanner = new Scanner();
            var filter = new DependentColumnFilter(
                Encoding.UTF8.GetBytes(ColumnFamilyName1),
                Encoding.UTF8.GetBytes(LineNumberColumnName),
                false,
                CompareFilter.CompareOp.Equal,
                new BinaryComparator(BitConverter.GetBytes(1)));
            scanner.filter = filter.ToEncodedString();
            ScannerInformation scanInfo = client.CreateScanner(_tableName, scanner);

            List<FilterTestRecord> actualRecords = RetrieveResults(scanInfo).ToList();
            actualRecords.ShouldContainOnly(expectedRecords);
        }
        
        [TestMethod]
        [TestCategory(TestRunMode.CheckIn)]
        public void When_I_Scan_with_a_FamilyFilter_I_get_the_expected_results()
        {
            // B is in column family 2
            List<FilterTestRecord> expectedRecords = (from r in _allExpectedRecords select r.WithBValue(null)).ToList();

            var client = new HBaseClient(_credentials);
            var scanner = new Scanner();
            var filter = new FamilyFilter(CompareFilter.CompareOp.Equal, new BinaryComparator(Encoding.UTF8.GetBytes(ColumnFamilyName1)));
            scanner.filter = filter.ToEncodedString();
            ScannerInformation scanInfo = client.CreateScanner(_tableName, scanner);

            List<FilterTestRecord> actualRecords = RetrieveResults(scanInfo).ToList();
            actualRecords.ShouldContainOnly(expectedRecords);
        }
        
        [TestMethod]
        [TestCategory(TestRunMode.CheckIn)]
        public void When_I_Scan_with_a_FirstKeyOnlyFilter_I_get_the_expected_results()
        {
            // a first key only filter does not return column values
            List<FilterTestRecord> expectedRecords =
                (from r in _allExpectedRecords select new FilterTestRecord(r.RowKey, 0, string.Empty, string.Empty)).ToList();

            var client = new HBaseClient(_credentials);
            var scanner = new Scanner();

            var filter = new KeyOnlyFilter();

            scanner.filter = filter.ToEncodedString();

            ScannerInformation scanInfo = client.CreateScanner(_tableName, scanner);

            List<FilterTestRecord> actualRecords = RetrieveResults(scanInfo).ToList();

            actualRecords.ShouldContainOnly(expectedRecords);
        }

        [TestMethod]
        [TestCategory(TestRunMode.CheckIn)]
        public void When_I_Scan_with_a_KeyOnlyFilter_I_get_the_expected_results()
        {
            // a key only filter does not return column values
            List<FilterTestRecord> expectedRecords =
                (from r in _allExpectedRecords select new FilterTestRecord(r.RowKey, 0, string.Empty, string.Empty)).ToList();

            var client = new HBaseClient(_credentials);
            var scanner = new Scanner();

            var filter = new KeyOnlyFilter();

            scanner.filter = filter.ToEncodedString();

            ScannerInformation scanInfo = client.CreateScanner(_tableName, scanner);

            List<FilterTestRecord> actualRecords = RetrieveResults(scanInfo).ToList();

            actualRecords.ShouldContainOnly(expectedRecords);
        }

        [TestMethod]
        [TestCategory(TestRunMode.CheckIn)]
        public void When_I_Scan_with_a_SingleColumnValueExcludeFilter_and_a_BinaryComparator_with_the_operator_equal_I_get_the_expected_results()
        {
            string bValue = (from r in _allExpectedRecords select r.B).First();

            // B column should not be returned, so set the value to null.
            List<FilterTestRecord> expectedRecords = (from r in _allExpectedRecords where r.B == bValue select r.WithBValue(null)).ToList();

            var client = new HBaseClient(_credentials);
            var scanner = new Scanner();

            var filter = new SingleColumnValueExcludeFilter(
                Encoding.UTF8.GetBytes(ColumnFamilyName2),
                Encoding.UTF8.GetBytes(ColumnNameB),
                CompareFilter.CompareOp.Equal,
                Encoding.UTF8.GetBytes(bValue));

            scanner.filter = filter.ToEncodedString();

            ScannerInformation scanInfo = client.CreateScanner(_tableName, scanner);

            List<FilterTestRecord> actualRecords = RetrieveResults(scanInfo).ToList();

            actualRecords.ShouldContainOnly(expectedRecords);
        }

        [TestMethod]
        [TestCategory(TestRunMode.CheckIn)]
        public void When_I_Scan_with_a_SingleColumnValueFilter_and_a_BinaryComparator_with_the_operator_equal_I_get_the_expected_results()
        {
            List<FilterTestRecord> expectedRecords = (from r in _allExpectedRecords where r.LineNumber == 1 select r).ToList();

            var client = new HBaseClient(_credentials);
            var scanner = new Scanner();

            var filter = new SingleColumnValueFilter(
                Encoding.UTF8.GetBytes(ColumnFamilyName1),
                Encoding.UTF8.GetBytes(LineNumberColumnName),
                CompareFilter.CompareOp.Equal,
                BitConverter.GetBytes(1),
                filterIfMissing: true);

            scanner.filter = filter.ToEncodedString();

            ScannerInformation scanInfo = client.CreateScanner(_tableName, scanner);

            List<FilterTestRecord> actualRecords = RetrieveResults(scanInfo).ToList();

            actualRecords.ShouldContainOnly(expectedRecords);
        }

        [TestMethod]
        [TestCategory(TestRunMode.CheckIn)]
        public void When_I_Scan_with_a_SingleColumnValueFilter_and_a_BinaryComparator_with_the_operator_greater_than_I_get_the_expected_results()
        {
            List<FilterTestRecord> expectedRecords = (from r in _allExpectedRecords where r.LineNumber > 1 select r).ToList();

            var client = new HBaseClient(_credentials);
            var scanner = new Scanner();

            var filter = new SingleColumnValueFilter(
                Encoding.UTF8.GetBytes(ColumnFamilyName1),
                Encoding.UTF8.GetBytes(LineNumberColumnName),
                CompareFilter.CompareOp.GreaterThan,
                BitConverter.GetBytes(1));

            scanner.filter = filter.ToEncodedString();

            ScannerInformation scanInfo = client.CreateScanner(_tableName, scanner);

            List<FilterTestRecord> actualRecords = RetrieveResults(scanInfo).ToList();

            actualRecords.ShouldContainOnly(expectedRecords);
        }

        [TestMethod]
        [TestCategory(TestRunMode.CheckIn)]
        public void
            When_I_Scan_with_a_SingleColumnValueFilter_and_a_BinaryComparator_with_the_operator_greater_than_or_equal_I_get_the_expected_results()
        {
            List<FilterTestRecord> expectedRecords = (from r in _allExpectedRecords where r.LineNumber >= 1 select r).ToList();

            var client = new HBaseClient(_credentials);
            var scanner = new Scanner();

            var filter = new SingleColumnValueFilter(
                Encoding.UTF8.GetBytes(ColumnFamilyName1),
                Encoding.UTF8.GetBytes(LineNumberColumnName),
                CompareFilter.CompareOp.GreaterThanOrEqualTo,
                BitConverter.GetBytes(1));

            scanner.filter = filter.ToEncodedString();

            ScannerInformation scanInfo = client.CreateScanner(_tableName, scanner);

            List<FilterTestRecord> actualRecords = RetrieveResults(scanInfo).ToList();

            actualRecords.ShouldContainOnly(expectedRecords);
        }

        [TestMethod]
        [TestCategory(TestRunMode.CheckIn)]
        public void When_I_Scan_with_a_SingleColumnValueFilter_and_a_BinaryComparator_with_the_operator_less_than_I_get_the_expected_results()
        {
            List<FilterTestRecord> expectedRecords = (from r in _allExpectedRecords where r.LineNumber < 1 select r).ToList();

            var client = new HBaseClient(_credentials);
            var scanner = new Scanner();

            var filter = new SingleColumnValueFilter(
                Encoding.UTF8.GetBytes(ColumnFamilyName1),
                Encoding.UTF8.GetBytes(LineNumberColumnName),
                CompareFilter.CompareOp.LessThan,
                BitConverter.GetBytes(1));

            scanner.filter = filter.ToEncodedString();

            ScannerInformation scanInfo = client.CreateScanner(_tableName, scanner);

            List<FilterTestRecord> actualRecords = RetrieveResults(scanInfo).ToList();

            actualRecords.ShouldContainOnly(expectedRecords);
        }

        [TestMethod]
        [TestCategory(TestRunMode.CheckIn)]
        public void When_I_Scan_with_a_SingleColumnValueFilter_and_a_BinaryComparator_with_the_operator_less_than_or_equal_I_get_the_expected_results(
            )
        {
            List<FilterTestRecord> expectedRecords = (from r in _allExpectedRecords where r.LineNumber <= 1 select r).ToList();

            var client = new HBaseClient(_credentials);
            var scanner = new Scanner();

            var filter = new SingleColumnValueFilter(
                Encoding.UTF8.GetBytes(ColumnFamilyName1),
                Encoding.UTF8.GetBytes(LineNumberColumnName),
                CompareFilter.CompareOp.LessThanOrEqualTo,
                BitConverter.GetBytes(1));

            scanner.filter = filter.ToEncodedString();

            ScannerInformation scanInfo = client.CreateScanner(_tableName, scanner);

            List<FilterTestRecord> actualRecords = RetrieveResults(scanInfo).ToList();

            actualRecords.ShouldContainOnly(expectedRecords);
        }

        [TestMethod]
        [TestCategory(TestRunMode.CheckIn)]
        public void When_I_Scan_with_a_SingleColumnValueFilter_and_a_BinaryComparator_with_the_operator_no_op_I_get_the_expected_results()
        {
            var expectedRecords = new List<FilterTestRecord>();

            var client = new HBaseClient(_credentials);
            var scanner = new Scanner();

            var filter = new SingleColumnValueFilter(
                Encoding.UTF8.GetBytes(ColumnFamilyName1),
                Encoding.UTF8.GetBytes(LineNumberColumnName),
                CompareFilter.CompareOp.NoOperation,
                BitConverter.GetBytes(1));

            scanner.filter = filter.ToEncodedString();

            ScannerInformation scanInfo = client.CreateScanner(_tableName, scanner);

            List<FilterTestRecord> actualRecords = RetrieveResults(scanInfo).ToList();

            actualRecords.ShouldContainOnly(expectedRecords);
        }


        [TestMethod]
        [TestCategory(TestRunMode.CheckIn)]
        public void When_I_Scan_with_a_SingleColumnValueFilter_and_a_BinaryComparator_with_the_operator_not_equal_I_get_the_expected_results()
        {
            List<FilterTestRecord> expectedRecords = (from r in _allExpectedRecords where r.LineNumber != 1 select r).ToList();

            var client = new HBaseClient(_credentials);
            var scanner = new Scanner();

            var filter = new SingleColumnValueFilter(
                Encoding.UTF8.GetBytes(ColumnFamilyName1),
                Encoding.UTF8.GetBytes(LineNumberColumnName),
                CompareFilter.CompareOp.NotEqual,
                BitConverter.GetBytes(1));

            scanner.filter = filter.ToEncodedString();

            ScannerInformation scanInfo = client.CreateScanner(_tableName, scanner);

            List<FilterTestRecord> actualRecords = RetrieveResults(scanInfo).ToList();

            actualRecords.ShouldContainOnly(expectedRecords);
        }

        [TestMethod]
        [TestCategory(TestRunMode.CheckIn)]
        public void When_I_Scan_with_a_SingleColumnValueFilter_and_a_BinaryPrefixComparator_with_the_operator_equal_I_get_the_expected_results()
        {
            List<FilterTestRecord> expectedRecords = (from r in _allExpectedRecords where r.LineNumber == 3 select r).ToList();

            var client = new HBaseClient(_credentials);
            var scanner = new Scanner();

            var comparer = new BinaryPrefixComparator(BitConverter.GetBytes(3));

            var filter = new SingleColumnValueFilter(
                Encoding.UTF8.GetBytes(ColumnFamilyName1),
                Encoding.UTF8.GetBytes(LineNumberColumnName),
                CompareFilter.CompareOp.Equal,
                comparer,
                filterIfMissing: false);

            scanner.filter = filter.ToEncodedString();

            ScannerInformation scanInfo = client.CreateScanner(_tableName, scanner);

            List<FilterTestRecord> actualRecords = RetrieveResults(scanInfo).ToList();

            actualRecords.ShouldContainOnly(expectedRecords);
        }

        [TestMethod]
        [TestCategory(TestRunMode.CheckIn)]
        public void
            When_I_Scan_with_a_SingleColumnValueFilter_and_a_BitComparator_with_the_operator_equal_and_the_bitop_XOR_I_get_the_expected_results()
        {
            List<FilterTestRecord> expectedRecords = (from r in _allExpectedRecords where r.LineNumber != 3 select r).ToList();

            var client = new HBaseClient(_credentials);
            var scanner = new Scanner();

            var comparer = new BitComparator(BitConverter.GetBytes(3), BitComparator.BitwiseOp.Xor);

            var filter = new SingleColumnValueFilter(
                Encoding.UTF8.GetBytes(ColumnFamilyName1),
                Encoding.UTF8.GetBytes(LineNumberColumnName),
                CompareFilter.CompareOp.Equal,
                comparer);

            scanner.filter = filter.ToEncodedString();

            ScannerInformation scanInfo = client.CreateScanner(_tableName, scanner);

            List<FilterTestRecord> actualRecords = RetrieveResults(scanInfo).ToList();

            actualRecords.ShouldContainOnly(expectedRecords);
        }

        [TestMethod]
        [TestCategory(TestRunMode.CheckIn)]
        public void When_I_Scan_with_a_SingleColumnValueFilter_and_a_NullComparator_with_the_operator_not_equal_I_get_the_expected_results()
        {
            var expectedRecords = new List<FilterTestRecord>();

            var client = new HBaseClient(_credentials);
            var scanner = new Scanner();

            var comparer = new NullComparator();

            var filter = new SingleColumnValueFilter(
                Encoding.UTF8.GetBytes(ColumnFamilyName1),
                Encoding.UTF8.GetBytes(LineNumberColumnName),
                CompareFilter.CompareOp.NotEqual,
                comparer);

            scanner.filter = filter.ToEncodedString();

            ScannerInformation scanInfo = client.CreateScanner(_tableName, scanner);

            List<FilterTestRecord> actualRecords = RetrieveResults(scanInfo).ToList();

            actualRecords.ShouldContainOnly(expectedRecords);
        }

        [TestMethod]
        [TestCategory(TestRunMode.CheckIn)]
        public void When_I_Scan_with_a_SingleColumnValueFilter_and_a_SubstringComparator_with_the_operator_equal_I_get_the_expected_results()
        {
            // grab a substring that is guaranteed to match at least one record.
            string ss = _allExpectedRecords.First().A.Substring(1, 2);
            //Debug.WriteLine("The substring value is: " + ss);

            List<FilterTestRecord> expectedRecords = (from r in _allExpectedRecords where r.A.Contains(ss) select r).ToList();

            var client = new HBaseClient(_credentials);
            var scanner = new Scanner();

            var comparer = new SubstringComparator(ss);

            var filter = new SingleColumnValueFilter(
                Encoding.UTF8.GetBytes(ColumnFamilyName1),
                Encoding.UTF8.GetBytes(ColumnNameA),
                CompareFilter.CompareOp.Equal,
                comparer);

            scanner.filter = filter.ToEncodedString();

            ScannerInformation scanInfo = client.CreateScanner(_tableName, scanner);

            List<FilterTestRecord> actualRecords = RetrieveResults(scanInfo).ToList();

            actualRecords.ShouldContainOnly(expectedRecords);
        }

        private IEnumerable<FilterTestRecord> RetrieveResults(ScannerInformation scanInfo)
        {
            var rv = new List<FilterTestRecord>();

            var client = new HBaseClient(_credentials);
            CellSet next;

            while ((next = client.ScannerGetNext(scanInfo)) != null)
            {
                foreach (CellSet.Row row in next.rows)
                {
                    string rowKey = _encoding.GetString(row.key);
                    List<Cell> cells = row.values;

                    string a = null;
                    string b = null;
                    int lineNumber = 0;
                    foreach (Cell c in cells)
                    {
                        string columnName = ExtractColumnName(c.column);
                        switch (columnName)
                        {
                            case LineNumberColumnName:
                                lineNumber = c.data.Length > 0 ? BitConverter.ToInt32(c.data, 0) : 0;
                                break;

                            case ColumnNameA:
                                a = _encoding.GetString(c.data);
                                break;

                            case ColumnNameB:
                                b = _encoding.GetString(c.data);
                                break;

                            default:
                                throw new InvalidOperationException("Don't know what to do with column: " + columnName);
                        }
                    }

                    var rec = new FilterTestRecord(rowKey, lineNumber, a, b);
                    rv.Add(rec);
                }
            }

            return rv;
        }

        private void PopulateTable()
        {
            var client = new HBaseClient(_credentials);
            var cellSet = new CellSet();

            string id = Guid.NewGuid().ToString("N");
            for (int lineNumber = 0; lineNumber < 10; ++lineNumber)
            {
                string rowKey = string.Format(CultureInfo.InvariantCulture, "{0}-{1}", id, lineNumber);

                // add to expected records
                var rec = new FilterTestRecord(rowKey, lineNumber, Guid.NewGuid().ToString("N"), Guid.NewGuid().ToString("D"));
                _allExpectedRecords.Add(rec);

                // add to row
                var row = new CellSet.Row { key = _encoding.GetBytes(rec.RowKey) };

                var lineColumnValue = new Cell
                {
                    column = BuildCellColumn(ColumnFamilyName1, LineNumberColumnName),
                    data = BitConverter.GetBytes(rec.LineNumber)
                };
                row.values.Add(lineColumnValue);

                var paragraphColumnValue = new Cell { column = BuildCellColumn(ColumnFamilyName1, ColumnNameA), data = _encoding.GetBytes(rec.A) };
                row.values.Add(paragraphColumnValue);

                var columnValueB = new Cell { column = BuildCellColumn(ColumnFamilyName2, ColumnNameB), data = Encoding.UTF8.GetBytes(rec.B) };
                row.values.Add(columnValueB);

                cellSet.rows.Add(row);
            }

            client.StoreCells(_tableName, cellSet);
        }

        private Byte[] BuildCellColumn(string columnFamilyName, string columnName)
        {
            return _encoding.GetBytes(string.Format(CultureInfo.InvariantCulture, "{0}:{1}", columnFamilyName, columnName));
        }

        private string ExtractColumnName(Byte[] cellColumn)
        {
            string qualifiedColumnName = _encoding.GetString(cellColumn);
            string[] parts = qualifiedColumnName.Split(new[] { ':' }, 2);
            return parts[1];
        }

        private void AddTable()
        {
            // add a table specific to this test
            var client = new HBaseClient(_credentials);
            _tableName = TableNamePrefix + Guid.NewGuid().ToString("N");
            _tableSchema = new TableSchema { name = _tableName };
            _tableSchema.columns.Add(new ColumnSchema { name = ColumnFamilyName1 });
            _tableSchema.columns.Add(new ColumnSchema { name = ColumnFamilyName2 });
            client.CreateTable(_tableSchema);
        }
    }
}