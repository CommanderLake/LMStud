using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Windows.Forms;
using LMStud;
using Microsoft.VisualStudio.TestTools.UnitTesting;
namespace LM_Stud.Tests{
	[TestClass]
	public class GGUFMetadataManagerTests{
		private ListView _listView;
		private GGUFMetadataManager _manager;
		private string _testFilePath;
		[TestInitialize]
		public void TestInitialize(){
			_listView = new ListView();
			_listView.View = View.Details;
			_listView.Columns.Add("Key");
			_listView.Columns.Add("Value");
			_manager = new GGUFMetadataManager(_listView);
			_testFilePath = Path.GetTempFileName();
		}
		[TestCleanup]
		public void TestCleanup(){
			if(File.Exists(_testFilePath)) File.Delete(_testFilePath);
			_listView?.Dispose();
		}
		[TestMethod]
		public void Constructor_InitializesWithListView(){
			Assert.IsNotNull(_manager, "Manager should be created.");
			Assert.AreEqual(0, _listView.Items.Count, "ListView should be empty initially.");
		}
		[TestMethod]
		public void LoadMetadata_WithInvalidFile_ReturnsFalse(){
			// Create an invalid GGUF file
			File.WriteAllText(_testFilePath, "Not a GGUF file");
			var result = _manager.LoadMetadata(_testFilePath);
			Assert.IsFalse(result, "Should return false for invalid file.");
			Assert.AreEqual(0, _listView.Items.Count, "ListView should remain empty.");
		}
		[TestMethod]
		public void LoadMetadata_WithValidGGUFHeader_LoadsBasicInfo(){
			// Create a minimal valid GGUF file
			CreateMinimalGGUFFile(_testFilePath);
			var result = _manager.LoadMetadata(_testFilePath);
			Assert.IsTrue(result, "Should successfully load valid GGUF file.");
			Assert.IsTrue(_listView.Items.Count > 0, "Should add items to ListView.");
		}
		[TestMethod]
		public void LoadMetadata_WithNonExistentFile_ReturnsFalse(){
			var result = _manager.LoadMetadata("nonexistent.gguf");
			Assert.IsFalse(result, "Should return false for non-existent file.");
		}
		[TestMethod]
		public void Clear_RemovesAllItems(){
			// Add some test items
			_listView.Items.Add(new ListViewItem(new[]{ "key1", "value1" }));
			_listView.Items.Add(new ListViewItem(new[]{ "key2", "value2" }));
			Assert.AreEqual(2, _listView.Items.Count, "Should have 2 items.");
			_manager.Clear();
			Assert.AreEqual(0, _listView.Items.Count, "Should clear all items.");
		}
		[TestMethod]
		public void FormatValue_HandlesStringType(){
			var result = FormatTestValue(8, Encoding.UTF8.GetBytes("test"));
			Assert.AreEqual("test", result, "Should format string correctly.");
		}
		[TestMethod]
		public void FormatValue_HandlesUInt8Type(){
			var result = FormatTestValue(0, new byte[]{ 255 });
			Assert.AreEqual("255", result, "Should format uint8 correctly.");
		}
		[TestMethod]
		public void FormatValue_HandlesInt8Type(){
			var result = FormatTestValue(1, new byte[]{ 127 });
			Assert.AreEqual("127", result, "Should format int8 correctly.");
		}
		[TestMethod]
		public void FormatValue_HandlesUInt16Type(){
			var result = FormatTestValue(2, BitConverter.GetBytes((ushort)65535));
			Assert.AreEqual("65535", result, "Should format uint16 correctly.");
		}
		[TestMethod]
		public void FormatValue_HandlesInt16Type(){
			var result = FormatTestValue(3, BitConverter.GetBytes((short)32767));
			Assert.AreEqual("32767", result, "Should format int16 correctly.");
		}
		[TestMethod]
		public void FormatValue_HandlesUInt32Type(){
			var result = FormatTestValue(4, BitConverter.GetBytes(4294967295));
			Assert.AreEqual("4294967295", result, "Should format uint32 correctly.");
		}
		[TestMethod]
		public void FormatValue_HandlesInt32Type(){
			var result = FormatTestValue(5, BitConverter.GetBytes(2147483647));
			Assert.AreEqual("2147483647", result, "Should format int32 correctly.");
		}
		[TestMethod]
		public void FormatValue_HandlesFloat32Type(){
			var result = FormatTestValue(6, BitConverter.GetBytes(3.14f));
			Assert.IsTrue(result.StartsWith("3.14"), "Should format float32 correctly.");
		}
		[TestMethod]
		public void FormatValue_HandlesBoolType(){
			var resultTrue = FormatTestValue(7, new byte[]{ 1 });
			var resultFalse = FormatTestValue(7, new byte[]{ 0 });
			Assert.AreEqual("True", resultTrue, "Should format true correctly.");
			Assert.AreEqual("False", resultFalse, "Should format false correctly.");
		}
		[TestMethod]
		public void FormatValue_HandlesUInt64Type(){
			var result = FormatTestValue(10, BitConverter.GetBytes(18446744073709551615));
			Assert.AreEqual("18446744073709551615", result, "Should format uint64 correctly.");
		}
		[TestMethod]
		public void FormatValue_HandlesInt64Type(){
			var result = FormatTestValue(11, BitConverter.GetBytes(9223372036854775807L));
			Assert.AreEqual("9223372036854775807", result, "Should format int64 correctly.");
		}
		[TestMethod]
		public void FormatValue_HandlesFloat64Type(){
			var result = FormatTestValue(12, BitConverter.GetBytes(3.141592653589793));
			Assert.IsTrue(result.StartsWith("3.14159"), "Should format float64 correctly.");
		}
		[TestMethod]
		public void FormatValue_HandlesArrayType(){
			// Array type (9) with element type uint32 (4)
			var arrayData = new List<byte>();
			arrayData.Add(4);// Element type: uint32
			arrayData.AddRange(BitConverter.GetBytes((ulong)3));// Array length
			arrayData.AddRange(BitConverter.GetBytes((uint)1));
			arrayData.AddRange(BitConverter.GetBytes((uint)2));
			arrayData.AddRange(BitConverter.GetBytes((uint)3));
			var result = FormatTestValue(9, arrayData.ToArray());
			Assert.IsTrue(result.Contains("1") && result.Contains("2") && result.Contains("3"), "Should format array elements.");
		}
		[TestMethod]
		public void FormatValue_HandlesUnknownType(){
			var result = FormatTestValue(999, new byte[]{ 1, 2, 3, 4 });
			Assert.IsTrue(result.StartsWith("Unknown type"), "Should handle unknown type gracefully.");
		}
		private void CreateMinimalGGUFFile(string path){
			using(var stream = new FileStream(path, FileMode.Create))
			using(var writer = new BinaryWriter(stream)){
				// GGUF magic
				writer.Write(0x46554747);// "GGUF"
				writer.Write(0x00000003);// Version 3

				// Tensor count
				writer.Write((ulong)0);

				// Metadata KV count
				writer.Write((ulong)2);

				// Metadata entry 1: string key-value
				WriteString(writer, "test.key1");
				writer.Write((uint)8);// String type
				WriteString(writer, "test value 1");

				// Metadata entry 2: uint32 key-value
				WriteString(writer, "test.key2");
				writer.Write((uint)4);// UInt32 type
				writer.Write((uint)42);
			}
		}
		private void WriteString(BinaryWriter writer, string str){
			var bytes = Encoding.UTF8.GetBytes(str);
			writer.Write((ulong)bytes.Length);
			writer.Write(bytes);
		}
		private string FormatTestValue(uint type, byte[] data){
			// Use reflection to call the private FormatValue method
			var method = typeof(GGUFMetadataManager).GetMethod("FormatValue", BindingFlags.NonPublic | BindingFlags.Static);
			if(method == null) return "Method not found";
			using(var stream = new MemoryStream(data))
			using(var reader = new BinaryReader(stream)){ return (string)method.Invoke(null, new object[]{ type, reader }); }
		}
	}
}