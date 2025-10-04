using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using LMStud;
using Microsoft.VisualStudio.TestTools.UnitTesting;
namespace LM_Stud.Tests{
	[TestClass]
	public class GGUFMetadataManagerTests{
		private string _testFilePath;
		[TestInitialize]
		public void Setup(){_testFilePath = Path.Combine(Path.GetTempPath(), $"gguf-test-{Guid.NewGuid():N}.bin");}
		[TestCleanup]
		public void Cleanup(){
			if(File.Exists(_testFilePath)) File.Delete(_testFilePath);
		}
		[TestMethod]
		public void LoadGGUFMetadata_WithValidFile_ReturnsMetadataEntries(){
			CreateMinimalGGUFFile(_testFilePath);
			var metadata = GGUFMetadataManager.LoadGGUFMetadata(_testFilePath);
			Assert.IsNotNull(metadata, "Metadata list should be returned for a valid file.");
			Assert.AreEqual(2, metadata.Count, "Expected two metadata entries.");
			Assert.AreEqual("test.key1", metadata[0].Key, "First key should match the written value.");
			Assert.AreEqual("test value 1", metadata[0].Val.Value as string, "String value should be read correctly.");
			Assert.AreEqual("test.key2", metadata[1].Key, "Second key should match the written value.");
			Assert.AreEqual((uint)42, metadata[1].Val.Value, "Integer value should be read correctly.");
		}
		[TestMethod]
		public void LoadGGUFMetadata_WithInvalidMagic_ReturnsNull(){
			File.WriteAllBytes(_testFilePath, BitConverter.GetBytes(0x12345678));
			var metadata = GGUFMetadataManager.LoadGGUFMetadata(_testFilePath);
			Assert.IsNull(metadata, "Invalid GGUF header should return null.");
		}
		[TestMethod]
		[ExpectedException(typeof(FileNotFoundException))]
		public void LoadGGUFMetadata_WithMissingFile_Throws(){GGUFMetadataManager.LoadGGUFMetadata(Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".gguf"));}
		[TestMethod]
		public void GetGGUFCtxMax_WithContextEntry_ReturnsValue(){
			var metadata = new List<GGUFMetadataManager.GGUFMetadataEntry>{
				new GGUFMetadataManager.GGUFMetadataEntry{ Key = "model.context_length", Val = new GGUFMetadataManager.GGUFMetaValue{ Type = GGUFMetadataManager.GGUFType.UINT32, Value = (uint)4096 } }
			};
			var result = GGUFMetadataManager.GetGGUFCtxMax(metadata);
			Assert.AreEqual(4096, result, "Context length should be returned when present as uint.");
		}
		[TestMethod]
		public void GetGGUFCtxMax_WithoutContextEntry_ReturnsMinusOne(){
			var metadata = new List<GGUFMetadataManager.GGUFMetadataEntry>();
			var result = GGUFMetadataManager.GetGGUFCtxMax(metadata);
			Assert.AreEqual(-1, result, "When context length is missing the method should return -1.");
		}
		private static void CreateMinimalGGUFFile(string path){
			using(var stream = new FileStream(path, FileMode.Create, FileAccess.Write)){
				using(var writer = new BinaryWriter(stream, Encoding.UTF8, false)){
					writer.Write(0x46554747);// GGUF magic
					writer.Write(0x00000003);// version
					writer.Write((ulong)0);// tensor count
					writer.Write((ulong)2);// metadata count
					WriteString(writer, "test.key1");
					writer.Write((uint)GGUFMetadataManager.GGUFType.STRING);
					WriteString(writer, "test value 1");
					WriteString(writer, "test.key2");
					writer.Write((uint)GGUFMetadataManager.GGUFType.UINT32);
					writer.Write((uint)42);
				}
			}
		}
		private static void WriteString(BinaryWriter writer, string value){
			var bytes = Encoding.UTF8.GetBytes(value);
			writer.Write((ulong)bytes.Length);
			writer.Write(bytes);
		}
	}
}