using System.Globalization;
using System.Windows.Forms;
using LMStud;
using Microsoft.VisualStudio.TestTools.UnitTesting;
namespace LM_Stud.Tests{
	[TestClass]
	public class LVISorterTests{
		private LVISorter _sorter;
		[TestMethod]
		public void Constructor_InitializesWithDefaults(){
			_sorter = new LVISorter();
			Assert.AreEqual(0, _sorter.ColumnIndex, "Default column index should be 0.");
			Assert.AreEqual(SortOrder.Ascending, _sorter.SortOrder, "Default sort order should be Ascending.");
			Assert.AreEqual(LVISorter.SortDataType.String, _sorter.DataType, "Default data type should be String.");
		}
		[TestMethod]
		public void Constructor_WithParameters_SetsValues(){
			_sorter = new LVISorter(2, SortOrder.Descending, LVISorter.SortDataType.Integer);
			Assert.AreEqual(2, _sorter.ColumnIndex, "Column index should be set.");
			Assert.AreEqual(SortOrder.Descending, _sorter.SortOrder, "Sort order should be set.");
			Assert.AreEqual(LVISorter.SortDataType.Integer, _sorter.DataType, "Data type should be set.");
		}
		[TestMethod]
		public void Compare_StringType_SortsAlphabetically(){
			_sorter = new LVISorter(0, SortOrder.Ascending, LVISorter.SortDataType.String);
			var item1 = new ListViewItem("Apple");
			var item2 = new ListViewItem("Banana");
			var result = _sorter.Compare(item1, item2);
			Assert.IsTrue(result < 0, "Apple should come before Banana.");
		}
		[TestMethod]
		public void Compare_StringType_Descending_ReversesOrder(){
			_sorter = new LVISorter(0, SortOrder.Descending, LVISorter.SortDataType.String);
			var item1 = new ListViewItem("Apple");
			var item2 = new ListViewItem("Banana");
			var result = _sorter.Compare(item1, item2);
			Assert.IsTrue(result > 0, "In descending order, Apple should come after Banana.");
		}
		[TestMethod]
		public void Compare_IntegerType_SortsNumerically(){
			_sorter = new LVISorter(0, SortOrder.Ascending, LVISorter.SortDataType.Integer);
			var item1 = new ListViewItem("10");
			var item2 = new ListViewItem("2");
			var result = _sorter.Compare(item1, item2);
			Assert.IsTrue(result > 0, "10 should come after 2 numerically.");
		}
		[TestMethod]
		public void Compare_IntegerType_HandlesInvalidValues(){
			_sorter = new LVISorter(0, SortOrder.Ascending, LVISorter.SortDataType.Integer);
			var item1 = new ListViewItem("abc");
			var item2 = new ListViewItem("10");
			var result = _sorter.Compare(item1, item2);
			Assert.IsTrue(result < 0, "Non-numeric values should come before numeric values.");
		}
		[TestMethod]
		public void Compare_DoubleType_SortsDecimalValues(){
			_sorter = new LVISorter(0, SortOrder.Ascending, LVISorter.SortDataType.Double);
			var item1 = new ListViewItem("3.14");
			var item2 = new ListViewItem("2.71");
			var result = _sorter.Compare(item1, item2);
			Assert.IsTrue(result > 0, "3.14 should come after 2.71.");
		}
		[TestMethod]
		public void Compare_DateTimeType_SortsChronologically(){
			_sorter = new LVISorter(0, SortOrder.Ascending, LVISorter.SortDataType.DateTime);
			var item1 = new ListViewItem("2023-01-01");
			var item2 = new ListViewItem("2024-01-01");
			var result = _sorter.Compare(item1, item2);
			Assert.IsTrue(result < 0, "Earlier date should come first.");
		}
		[TestMethod]
		public void Compare_BooleanType_SortsFalseBeforeTrue(){
			_sorter = new LVISorter(0, SortOrder.Ascending, LVISorter.SortDataType.Boolean);
			var item1 = new ListViewItem("false");
			var item2 = new ListViewItem("true");
			var result = _sorter.Compare(item1, item2);
			Assert.IsTrue(result < 0, "False should come before True.");
		}
		[TestMethod]
		public void Compare_WithSubItems_UsesCorrectColumn(){
			_sorter = new LVISorter(1, SortOrder.Ascending, LVISorter.SortDataType.Integer);
			var item1 = new ListViewItem("A");
			item1.SubItems.Add("5");
			var item2 = new ListViewItem("B");
			item2.SubItems.Add("3");
			var result = _sorter.Compare(item1, item2);
			Assert.IsTrue(result > 0, "Should compare column 1: 5 > 3.");
		}
		[TestMethod]
		public void Compare_NullValues_HandlesGracefully(){
			_sorter = new LVISorter(0, SortOrder.Ascending, LVISorter.SortDataType.String);
			var item1 = new ListViewItem("");
			var item2 = new ListViewItem("Text");
			var result = _sorter.Compare(item1, item2);
			Assert.IsTrue(result < 0, "Empty string should come before text.");
		}
		[TestMethod]
		public void Compare_NonListViewItem_ReturnsZero(){
			_sorter = new LVISorter();
			var result = _sorter.Compare("not an item", "also not an item");
			Assert.AreEqual(0, result, "Non-ListViewItem comparisons should return 0.");
		}
		[TestMethod]
		public void Compare_ColumnIndexOutOfRange_ReturnsZero(){
			_sorter = new LVISorter(5, SortOrder.Ascending, LVISorter.SortDataType.String);
			var item1 = new ListViewItem("Text1");
			var item2 = new ListViewItem("Text2");
			var result = _sorter.Compare(item1, item2);
			Assert.AreEqual(0, result, "Out of range column index should return 0.");
		}
		[TestMethod]
		public void ClearCache_RemovesCachedValues(){
			_sorter = new LVISorter(0, SortOrder.Ascending, LVISorter.SortDataType.Integer);
			var item1 = new ListViewItem("10");
			var item2 = new ListViewItem("20");

			// First comparison caches values
			_sorter.Compare(item1, item2);

			// Clear cache
			_sorter.ClearCache();

			// Should still work after cache clear
			var result = _sorter.Compare(item1, item2);
			Assert.IsTrue(result < 0, "Should still compare correctly after cache clear.");
		}
		[TestMethod]
		public void Culture_SetValue_AffectsComparison(){
			_sorter = new LVISorter(0, SortOrder.Ascending, LVISorter.SortDataType.Double);
			_sorter.Culture = new CultureInfo("fr-FR");// French uses comma for decimal
			var item1 = new ListViewItem("1,5");// 1.5 in French format
			var item2 = new ListViewItem("1,2");// 1.2 in French format
			var result = _sorter.Compare(item1, item2);
			Assert.IsTrue(result > 0, "1.5 should be greater than 1.2.");
		}
		[TestMethod]
		public void NumberStyle_SetValue_AffectsParsing(){
			_sorter = new LVISorter(0, SortOrder.Ascending, LVISorter.SortDataType.Integer);
			_sorter.NumberStyle = NumberStyles.AllowThousands;
			var item1 = new ListViewItem("1,000");
			var item2 = new ListViewItem("999");
			var result = _sorter.Compare(item1, item2);
			Assert.IsTrue(result > 0, "1000 should be greater than 999.");
		}
		[TestMethod]
		public void DateTimeStyle_SetValue_AffectsParsing(){
			_sorter = new LVISorter(0, SortOrder.Ascending, LVISorter.SortDataType.DateTime);
			_sorter.DateTimeStyle = DateTimeStyles.AssumeUniversal;
			var item1 = new ListViewItem("2024-01-01T00:00:00Z");
			var item2 = new ListViewItem("2024-01-01T01:00:00Z");
			var result = _sorter.Compare(item1, item2);
			Assert.IsTrue(result < 0, "Earlier time should come first.");
		}
	}
}