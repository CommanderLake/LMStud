using System.Windows.Forms;
using LMStud;
using Microsoft.VisualStudio.TestTools.UnitTesting;
namespace LM_Stud.Tests{
	[TestClass]
	public class LVISorterTests{
		[TestMethod]
		public void Constructor_InitializesWithDefaults(){
			var sorter = new LVISorter();
			Assert.AreEqual(0, sorter.ColumnIndex, "Default column index should be 0.");
			Assert.AreEqual(SortOrder.Ascending, sorter.SortOrder, "Default sort order should be Ascending.");
			Assert.AreEqual(SortDataType.String, sorter.DataType, "Default data type should be String.");
		}
		[TestMethod]
		public void Constructor_WithParameters_SetsValues(){
			var sorter = new LVISorter(2, SortDataType.Integer, SortOrder.Descending);
			Assert.AreEqual(2, sorter.ColumnIndex, "Column index should be set.");
			Assert.AreEqual(SortOrder.Descending, sorter.SortOrder, "Sort order should be set.");
			Assert.AreEqual(SortDataType.Integer, sorter.DataType, "Data type should be set.");
		}
		[TestMethod]
		public void Compare_StringType_SortsAlphabetically(){
			var sorter = new LVISorter(0, SortDataType.String);
			var item1 = new ListViewItem("Apple");
			var item2 = new ListViewItem("Banana");
			var result = sorter.Compare(item1, item2);
			Assert.IsTrue(result < 0, "Apple should come before Banana.");
		}
		[TestMethod]
		public void Compare_StringType_Descending_ReversesOrder(){
			var sorter = new LVISorter(0, SortDataType.String, SortOrder.Descending);
			var item1 = new ListViewItem("Apple");
			var item2 = new ListViewItem("Banana");
			var result = sorter.Compare(item1, item2);
			Assert.IsTrue(result > 0, "In descending order, Apple should come after Banana.");
		}
		[TestMethod]
		public void Compare_IntegerType_SortsNumerically(){
			var sorter = new LVISorter(0, SortDataType.Integer);
			var item1 = new ListViewItem("10");
			var item2 = new ListViewItem("2");
			var result = sorter.Compare(item1, item2);
			Assert.IsTrue(result > 0, "10 should come after 2 numerically.");
		}
		[TestMethod]
		public void Compare_IntegerType_HandlesInvalidValues(){
			var sorter = new LVISorter(0, SortDataType.Integer);
			var item1 = new ListViewItem("abc");
			var item2 = new ListViewItem("10");
			var result = sorter.Compare(item1, item2);
			Assert.IsTrue(result < 0, "Non-numeric values should come before numeric values.");
		}
		[TestMethod]
		public void Compare_DoubleType_SortsDecimalValues(){
			var sorter = new LVISorter(0, SortDataType.Double);
			var item1 = new ListViewItem("3.14");
			var item2 = new ListViewItem("2.71");
			var result = sorter.Compare(item1, item2);
			Assert.IsTrue(result > 0, "3.14 should come after 2.71.");
		}
		[TestMethod]
		public void Compare_DateTimeType_SortsChronologically(){
			var sorter = new LVISorter(0, SortDataType.DateTime);
			var item1 = new ListViewItem("2023-01-01");
			var item2 = new ListViewItem("2024-01-01");
			var result = sorter.Compare(item1, item2);
			Assert.IsTrue(result < 0, "Earlier date should come first.");
		}
		[TestMethod]
		public void Compare_BooleanType_SortsFalseBeforeTrue(){
			var sorter = new LVISorter(0, SortDataType.Boolean);
			var item1 = new ListViewItem("false");
			var item2 = new ListViewItem("true");
			var result = sorter.Compare(item1, item2);
			Assert.IsTrue(result < 0, "False should come before True.");
		}
		[TestMethod]
		public void Compare_WithSubItems_UsesCorrectColumn(){
			var sorter = new LVISorter(1, SortDataType.Integer);
			var item1 = new ListViewItem("A");
			item1.SubItems.Add("5");
			var item2 = new ListViewItem("B");
			item2.SubItems.Add("3");
			var result = sorter.Compare(item1, item2);
			Assert.IsTrue(result > 0, "Should compare column 1: 5 > 3.");
		}
		[TestMethod]
		public void Compare_EmptyValues_HandlesGracefully(){
			var sorter = new LVISorter(0, SortDataType.String);
			var item1 = new ListViewItem("");
			var item2 = new ListViewItem("Text");
			var result = sorter.Compare(item1, item2);
			Assert.IsTrue(result < 0, "Empty string should come before text.");
		}
		[TestMethod]
		public void Compare_NonListViewItem_ReturnsZero(){
			var sorter = new LVISorter();
			var result = sorter.Compare("not an item", "also not an item");
			Assert.AreEqual(0, result, "Non-ListViewItem comparisons should return 0.");
		}
		[TestMethod]
		public void Compare_ColumnIndexOutOfRange_ReturnsZero(){
			var sorter = new LVISorter(5, SortDataType.String);
			var item1 = new ListViewItem("Text1");
			var item2 = new ListViewItem("Text2");
			var result = sorter.Compare(item1, item2);
			Assert.AreEqual(0, result, "Out of range column index should return 0.");
		}
		[TestMethod]
		public void ClearCache_AllowsReParsingAfterChange(){
			var sorter = new LVISorter(0, SortDataType.Integer);
			var item1 = new ListViewItem("10");
			var item2 = new ListViewItem("20");
			var firstResult = sorter.Compare(item1, item2);
			Assert.IsTrue(firstResult < 0, "10 should be less than 20.");
			sorter.ClearCache();
			var secondResult = sorter.Compare(item1, item2);
			Assert.IsTrue(secondResult < 0, "Comparison should remain valid after clearing cache.");
		}
		[TestMethod]
		public void ChangingColumnIndex_ClearsCacheAndUsesNewColumn(){
			var sorter = new LVISorter(0, SortDataType.String);
			var item1 = new ListViewItem("A");
			item1.SubItems.Add("20");
			var item2 = new ListViewItem("B");
			item2.SubItems.Add("10");

			// initial comparison on column 0 -> A vs B
			var firstResult = sorter.Compare(item1, item2);
			Assert.IsTrue(firstResult < 0, "Column 0 comparison should use the primary text.");
			sorter.ColumnIndex = 1;
			sorter.DataType = SortDataType.Integer;
			var secondResult = sorter.Compare(item1, item2);
			Assert.IsTrue(secondResult > 0, "After switching to column 1 the numeric values should be compared.");
		}
	}
}