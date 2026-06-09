using System;
using System.IO;
using System.Text;
using LMStud;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LM_Stud.Tests{
	[TestClass]
	public class ModelFileTests{
		private string _testFilePath;

		[TestInitialize]
		public void Setup(){
			_testFilePath = Path.Combine(Path.GetTempPath(), $"gguf-test-{Guid.NewGuid():N}.bin");
		}

		[TestCleanup]
		public void Cleanup(){
			if(File.Exists(_testFilePath)) File.Delete(_testFilePath);
		}

		[TestMethod]
		public void ValidGgufFileLoadsItsMetadata(){
			CreateMinimalGgufFile(_testFilePath);
			var metadata = GGUFMetadataManager.LoadGGUFMetadata(_testFilePath);
			Assert.IsNotNull(metadata);
			Assert.AreEqual(2, metadata.Count);
			Assert.AreEqual("test value 1", metadata[0].Val.Value as string);
			Assert.AreEqual((uint)42, metadata[1].Val.Value);
		}

		[TestMethod]
		public void InvalidGgufFileIsRejected(){
			File.WriteAllBytes(_testFilePath, BitConverter.GetBytes(0x12345678));
			Assert.IsNull(GGUFMetadataManager.LoadGGUFMetadata(_testFilePath));
		}

		private static void CreateMinimalGgufFile(string path){
			using(var stream = new FileStream(path, FileMode.Create, FileAccess.Write))
			using(var writer = new BinaryWriter(stream, Encoding.UTF8, false)){
				writer.Write(0x46554747);
				writer.Write(0x00000003);
				writer.Write((ulong)0);
				writer.Write((ulong)2);
				WriteString(writer, "test.key1");
				writer.Write((uint)GGUFMetadataManager.GGUFType.STRING);
				WriteString(writer, "test value 1");
				WriteString(writer, "test.key2");
				writer.Write((uint)GGUFMetadataManager.GGUFType.UINT32);
				writer.Write((uint)42);
			}
		}

		private static void WriteString(BinaryWriter writer, string value){
			var bytes = Encoding.UTF8.GetBytes(value);
			writer.Write((ulong)bytes.Length);
			writer.Write(bytes);
		}
	}
}
