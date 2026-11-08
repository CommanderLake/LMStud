using System;
using System.IO;
using System.Linq;
using System.Text;
using LMStud;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LM_Stud.Tests{
	[TestClass]
	[DoNotParallelize]
	public class DirectoryListingToolTests{
		private const string Slot = "directory-listing-tests";
		private string _directory;

		[TestInitialize]
		public void Setup(){
			_directory = Path.Combine(Path.GetTempPath(), "lmstud-list-" + Guid.NewGuid().ToString("N"));
			Directory.CreateDirectory(_directory);
			NativeMethods.SetFileBaseDir(_directory);
			NativeMethods.RegisterTools(Slot, false, false, false, true, false, false, false, false);
		}
		[TestCleanup]
		public void Cleanup(){
			NativeMethods.ClearTools(Slot);
			NativeMethods.SetFileBaseDir(Common.FileBaseDir);
			Directory.Delete(_directory, true);
		}

		[TestMethod]
		public void DefaultBudgetReturnsPreviewWarningAndNextPage(){
			for(var i = 0; i < 250; i++) Write("file-" + i.ToString("D3") + ".txt");
			var first = List();
			Assert.AreEqual(200, first["entries"].Count);
			Assert.AreEqual(200, first.GetInt("returned_count"));
			Assert.AreEqual(true, first.GetBool("truncated"));
			Assert.AreEqual(200, first.GetInt("next_offset"));
			Assert.AreEqual(201, first.GetInt("entries_seen"));
			Assert.IsTrue(first["total_entries"].IsNull, "A preview must not claim an exact total without finishing traversal.");
			StringAssert.Contains(first.GetString("warning"), "offset=200");
			var second = List(offset: first.GetInt("next_offset").Value);
			Assert.AreEqual(50, second["entries"].Count);
			Assert.AreEqual(false, second.GetBool("truncated"));
			Assert.AreEqual(250, second.GetInt("total_entries"));
			Assert.IsTrue(second["next_offset"].IsNull);
			Assert.AreEqual(250, Names(first).Concat(Names(second)).Distinct().Count());
			Assert.AreEqual(0, List(offset: 999)["entries"].Count);
		}

		[DataTestMethod]
		[DataRow(0)]
		[DataRow(1)]
		[DataRow(200)]
		public void ExactAndEmptyListingsAreNotMarkedTruncated(int count){
			for(var i = 0; i < count; i++) Write("file-" + i + ".txt");
			var result = List();
			Assert.AreEqual(count, result.GetInt("returned_count"));
			Assert.AreEqual(count, result.GetInt("total_entries"));
			Assert.AreEqual(false, result.GetBool("truncated"));
			Assert.IsFalse(result["warning"].Exists);
		}

		[TestMethod]
		public void ByteBudgetBoundsLongNamesEvenWithLargerEntryBudget(){
			for(var i = 0; i < 200; i++) Write(i.ToString("D3") + new string('x', 125) + ".txt");
			var first = List(maxEntries: 1000);
			Assert.IsTrue(first["entries"].Count > 0 && first["entries"].Count < 200);
			Assert.IsTrue(Encoding.UTF8.GetByteCount(first["entries"].ToJson()) <= 16384);
			Assert.AreEqual(true, first.GetBool("truncated"));
			var second = List(maxEntries: 1000, offset: first.GetInt("next_offset").Value);
			Assert.AreEqual(200, Names(first).Concat(Names(second)).Distinct().Count());
		}

		[TestMethod]
		public void CallerCanExplicitlyRaiseEntryBudget(){
			for(var i = 0; i < 250; i++) Write(i + ".txt");
			var result = List(maxEntries: 300);
			Assert.AreEqual(250, result.GetInt("returned_count"));
			Assert.AreEqual(false, result.GetBool("truncated"));
		}

		[DataTestMethod]
		[DataRow("packages")]
		[DataRow("PACKAGES")]
		[DataRow("node_modules")]
		[DataRow("vendor")]
		[DataRow("bin")]
		[DataRow("obj")]
		[DataRow("Debug")]
		[DataRow("Release")]
		[DataRow(".vs")]
		[DataRow(".venv")]
		public void RecursiveListingShowsGeneratedFoldersWithoutTheirContents(string folder){
			Write(folder + "/nested/dependency.txt");
			Write("src/real.cs");
			var result = List(recursive: true);
			CollectionAssert.AreEquivalent(new[]{folder, "src", "src/real.cs"}, Names(result));
			Assert.AreEqual(1, result.GetInt("excluded_directory_count"));
			StringAssert.Contains(result.GetString("warning"), "include_generated=true");
			Assert.IsTrue(Names(List(recursive: true, includeGenerated: true)).Contains(folder + "/nested/dependency.txt"));
			Assert.IsTrue(Names(List(path: folder, recursive: true)).Contains(folder + "/nested/dependency.txt"), "A specifically requested folder remains browsable.");
		}

		[DataTestMethod]
		[DataRow(1, 1)]
		[DataRow(2, 2)]
		[DataRow(3, 3)]
		[DataRow(4, 4)]
		public void DepthLimitReportsPrunedDirectories(int depth, int count){
			Write("a/b/c/leaf.txt");
			var result = List(recursive: true, maxDepth: depth);
			Assert.AreEqual(count, result.GetInt("returned_count"));
			Assert.AreEqual(depth < 4 ? 1 : 0, result.GetInt("depth_limited_directory_count"));
			Assert.AreEqual(depth < 4, result["warning"].Exists);
		}

		[TestMethod]
		public void ShallowListingShowsImmediateChildrenAndGitRemainsExcluded(){
			Write(".git/objects/hidden.txt");
			Write("packages/nested/dependency.txt");
			Write("src/real.cs");
			CollectionAssert.AreEquivalent(new[]{"packages", "src"}, Names(List()));
			Assert.IsFalse(Names(List(recursive: true, includeGenerated: true)).Any(name => name.StartsWith(".git")));
		}

		[TestMethod]
		public void DriveRootsUseTheSameBudgetAndMetadata(){
			NativeMethods.SetFileBaseDir("");
			var all = List();
			Assert.IsTrue(all["entries"].Count > 0);
			var first = List(maxEntries: 1);
			Assert.AreEqual(1, first.GetInt("returned_count"));
			Assert.AreEqual(all["entries"].Count > 1, first.GetBool("truncated"));
			Assert.AreEqual(all["entries"][0].AsString(), first["entries"][0].AsString());
		}

		[DataTestMethod]
		[DataRow("max_entries", "0")]
		[DataRow("max_entries", "1001")]
		[DataRow("max_entries", "1.5")]
		[DataRow("max_entries", "999999999999999")]
		[DataRow("max_depth", "0")]
		[DataRow("max_depth", "33")]
		[DataRow("offset", "-1")]
		[DataRow("offset", "2147483648")]
		[DataRow("include_generated", "\"yes\"")]
		public void RejectsInvalidOrUnboundedOptions(string key, string value){
			var result = Execute("{\"" + key + "\":" + value + "}");
			StringAssert.Contains(result.GetString("error"), key);
		}

		[TestMethod]
		public void SchemaAdvertisesBudgetsAndPaging(){
			var tools = Json.Parse(NativeMethods.ReadUtf8(NativeMethods.GetToolsJson(Slot, out var length)));
			var properties = tools.Single(tool => tool["function"].GetString("name") == "list_directory")["function"]["parameters"]["properties"];
			Assert.AreEqual(200, properties["max_entries"].GetInt("default"));
			Assert.AreEqual(1000, properties["max_entries"].GetInt("maximum"));
			Assert.AreEqual(4, properties["max_depth"].GetInt("default"));
			Assert.AreEqual(false, properties["include_generated"].GetBool("default"));
			Assert.IsTrue(properties["offset"].Exists);
		}

		private void Write(string name){
			var path = Path.Combine(_directory, name.Replace('/', Path.DirectorySeparatorChar));
			Directory.CreateDirectory(Path.GetDirectoryName(path));
			File.WriteAllText(path, "content");
		}
		private static string[] Names(JsonNode result){ return result["entries"].Select(entry => entry.AsString()).ToArray(); }
		private static JsonNode List(string path = ".", bool recursive = false, int? maxEntries = null, int offset = 0, int maxDepth = 4, bool includeGenerated = false){
			var args = Json.ObjectBuilder(Json.P("path", path), Json.P("recursive", recursive), Json.P("offset", offset), Json.P("max_depth", maxDepth), Json.P("include_generated", includeGenerated));
			if(maxEntries.HasValue) args.Set("max_entries", maxEntries.Value);
			return Execute(args.ToJson());
		}
		private static JsonNode Execute(string args){ return Json.Parse(NativeMethods.ReadUtf8AndFree(NativeMethods.ExecuteTool(Slot, "list_directory", args))); }
	}
}
