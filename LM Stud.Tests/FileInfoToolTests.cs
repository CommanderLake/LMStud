using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using LMStud;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LM_Stud.Tests{
	[TestClass]
	[DoNotParallelize]
	public class FileInfoToolTests{
		private const string Slot = "file-info-tests";
		private string _directory;

		[TestInitialize]
		public void Setup(){
			_directory = Path.Combine(Path.GetTempPath(), "lmstud-file-info-" + Guid.NewGuid().ToString("N"));
			Directory.CreateDirectory(_directory);
			NativeMethods.SetFileBaseDir(_directory);
			NativeMethods.RegisterTools(Slot, false, false, false, false, false, true, false, false);
		}

		[TestCleanup]
		public void Cleanup(){
			NativeMethods.ClearTools(Slot);
			NativeMethods.SetFileBaseDir(Common.FileBaseDir);
			foreach(var path in Directory.GetFiles(_directory)) File.SetAttributes(path, FileAttributes.Normal);
			Directory.Delete(_directory, true);
		}

		[TestMethod]
		public void ReadSettingRegistersFileInfoWithRequiredPath(){
			var tools = Json.Parse(NativeMethods.ReadUtf8(NativeMethods.GetToolsJson(Slot, out var length)));
			var info = tools.Single(tool => tool["function"].GetString("name") == "get_file_info")["function"];
			Assert.AreEqual("path", info["parameters"]["required"][0].AsString());
			Assert.AreEqual("boolean", info["parameters"]["properties"]["include_text_stats"].GetString("type"));
			NativeMethods.RegisterTools(Slot, false, false, false, true, false, false, false, false);
			Assert.IsTrue(Info("sample.txt")["error"].Exists, "Reading metadata/text statistics requires the Read File setting.");
		}

		[TestMethod]
		public void ReportsUnicodePathsSizeDatesAndAttributes(){
			const string name = "\u6587\u4ef6 sample.TXT";
			var path = Write(name, "one\r\n\r\nlast");
			var modified = new DateTime(2024, 2, 3, 4, 5, 6, DateTimeKind.Utc);
			File.SetLastWriteTimeUtc(path, modified);
			File.SetAttributes(path, FileAttributes.ReadOnly | FileAttributes.Hidden);
			var info = Info(name);
			Assert.AreEqual(true, info.GetBool("exists"));
			Assert.AreEqual(name, info.GetString("name"));
			Assert.AreEqual(path.Replace('\\', '/'), info.GetString("path"));
			Assert.AreEqual(".TXT", info.GetString("extension"));
			Assert.AreEqual("file", info.GetString("type"));
			Assert.AreEqual(new FileInfo(path).Length, info.GetLong("size_bytes"));
			Assert.AreEqual("11 B", info.GetString("size_display"));
			Assert.AreEqual(true, info.GetBool("read_only"));
			Assert.AreEqual(true, info.GetBool("hidden"));
			Assert.AreEqual(modified, DateTime.Parse(info.GetString("modified_utc"), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind));
			StringAssert.EndsWith(info.GetString("created_utc"), "Z");
			StringAssert.EndsWith(info.GetString("accessed_utc"), "Z");
		}

		[DataTestMethod]
		[DataRow("", 0, 0, "none", false)]
		[DataRow("one", 1, 0, "none", false)]
		[DataRow("one\n", 1, 0, "LF", true)]
		[DataRow("one\r\n", 1, 0, "CRLF", true)]
		[DataRow("one\r", 1, 0, "CR", true)]
		[DataRow("\n", 1, 1, "LF", true)]
		[DataRow("\r\n", 1, 1, "CRLF", true)]
		[DataRow("one\n\n", 2, 1, "LF", true)]
		[DataRow("one\r\n\r\nlast", 3, 1, "CRLF", false)]
		[DataRow("one\n \t\nlast", 3, 1, "LF", false)]
		[DataRow("one\r\nnext\nlast\r", 3, 0, "mixed", true)]
		public void CountsLogicalLinesAndNewlineStyles(string content, int lines, int blanks, string style, bool finalNewline){
			Write("lines.txt", content);
			var text = Info("lines.txt")["text"];
			Assert.AreEqual("complete", text.GetString("status"));
			Assert.AreEqual(lines, text.GetInt("line_count"));
			Assert.AreEqual(blanks, text.GetInt("blank_line_count"));
			Assert.AreEqual(style, text.GetString("newline_style"));
			Assert.AreEqual(finalNewline, text.GetBool("ends_with_newline"));
			Assert.AreEqual(content.Length, text.GetInt("character_count"));
		}

		[DataTestMethod]
		[DataRow("utf8", "UTF-8")]
		[DataRow("utf16le", "UTF-16LE")]
		[DataRow("utf16be", "UTF-16BE")]
		[DataRow("utf32le", "UTF-32LE")]
		[DataRow("utf32be", "UTF-32BE")]
		public void CountsUnicodeCodepointsInBomMarkedFiles(string format, string label){
			var encoding = format == "utf8" ? (Encoding)new UTF8Encoding(true) : format == "utf16le" ? Encoding.Unicode : format == "utf16be" ? Encoding.BigEndianUnicode : new UTF32Encoding(format == "utf32be", true);
			File.WriteAllText(Path.Combine(_directory, "unicode.txt"), "A\U0001F600 \u4e2d\r\n\nend", encoding);
			var text = Info("unicode.txt")["text"];
			Assert.AreEqual(label, text.GetString("encoding"));
			Assert.AreEqual(true, text.GetBool("has_bom"));
			Assert.AreEqual(3, text.GetInt("line_count"));
			Assert.AreEqual(1, text.GetInt("blank_line_count"));
			Assert.AreEqual(10, text.GetInt("character_count"));
			Assert.AreEqual(3, text.GetInt("word_count"));
			Assert.AreEqual(4, text.GetInt("longest_line_characters"));
			Assert.AreEqual(1, text["newline_counts"].GetInt("crlf"));
			Assert.AreEqual(1, text["newline_counts"].GetInt("lf"));
			Assert.AreEqual(0, text["newline_counts"].GetInt("cr"));
		}

		[TestMethod]
		public void AnalyzesUtf8WithoutBomByDefault(){
			Write("unicode.txt", "\u00e9\u00a0hi\n");
			var text = ExecuteInfo("{\"path\":\"unicode.txt\"}")["text"];
			Assert.AreEqual("UTF-8", text.GetString("encoding"));
			Assert.AreEqual(false, text.GetBool("has_bom"));
			Assert.AreEqual(5, text.GetInt("character_count"));
			Assert.AreEqual(2, text.GetInt("word_count"));
			Assert.AreEqual(1, text.GetInt("line_count"));
		}

		[TestMethod]
		public void HandlesBomOnlyLegacyAndInvalidEncodedFiles(){
			File.WriteAllBytes(Path.Combine(_directory, "bom.txt"), Encoding.UTF8.GetPreamble());
			Assert.AreEqual(0, Info("bom.txt")["text"].GetInt("line_count"));
			File.WriteAllBytes(Path.Combine(_directory, "legacy.txt"), new byte[]{0xE9, 13, 10, 0xE9});
			var legacy = Info("legacy.txt")["text"];
			Assert.AreEqual("unknown-8-bit", legacy.GetString("encoding"));
			Assert.AreEqual(2, legacy.GetInt("line_count"));
			Assert.IsTrue(legacy["character_count"].IsNull);
			Assert.IsTrue(legacy["word_count"].IsNull);
			File.WriteAllBytes(Path.Combine(_directory, "invalid.txt"), new byte[]{0xFF, 0xFE, 0, 0xD8});
			Assert.AreEqual("invalid_encoding", Info("invalid.txt")["text"].GetString("reason"));
		}

		[DataTestMethod]
		[DataRow("README")]
		[DataRow("Dockerfile")]
		[DataRow(".gitignore")]
		[DataRow(".env.local")]
		public void RecognizesExtensionlessTextAndConfigFiles(string name){
			Write(name, "first\nsecond");
			Assert.AreEqual(2, Info(name)["text"].GetInt("line_count"));
		}

		[TestMethod]
		public void SkipsLargeUnknownAndBinaryFilesWithoutLosingMetadata(){
			using(var large = File.Create(Path.Combine(_directory, "large.txt"))) large.SetLength(16777217);
			var info = Info("large.txt");
			Assert.AreEqual(16777217L, info.GetLong("size_bytes"));
			Assert.AreEqual("size_limit", info["text"].GetString("reason"));
			Write("model.gguf", "contents");
			Assert.AreEqual("unrecognized_text_file", Info("model.gguf")["text"].GetString("reason"));
			File.WriteAllBytes(Path.Combine(_directory, "binary.txt"), new byte[]{65, 0, 66});
			Assert.AreEqual("binary_content", Info("binary.txt")["text"].GetString("reason"));
		}

		[TestMethod]
		public void MetadataOnlyWorksWhenContentCannotBeOpened(){
			var path = Write("locked.txt", "contents");
			using(var locked = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None)){
				Assert.AreEqual("disabled", Info("locked.txt", false)["text"].GetString("reason"));
				var info = Info("locked.txt");
				Assert.AreEqual(true, info.GetBool("exists"));
				Assert.AreEqual(8L, info.GetLong("size_bytes"));
				Assert.AreEqual("open_failed", info["text"].GetString("reason"));
			}
		}

		[TestMethod]
		public void ReportsDirectoriesAndMissingPathsAndEnforcesScope(){
			var directory = Info(".");
			Assert.AreEqual("directory", directory.GetString("type"));
			Assert.IsTrue(directory["size_bytes"].IsNull);
			Assert.AreEqual("not_a_regular_file", directory["text"].GetString("reason"));
			Assert.AreEqual(false, Info("missing.txt").GetBool("exists"));
			Assert.AreEqual("invalid path", Info("../outside.txt").GetString("error"));
			Assert.AreEqual("invalid path", Info(".git/config").GetString("error"));
			Assert.AreEqual("path is required", Info("").GetString("error"));
			Assert.AreEqual("invalid path", ExecuteInfo("{\"path\":\"bad\\u0000name.txt\"}").GetString("error"));
			Assert.AreEqual("include_text_stats must be a boolean", ExecuteInfo("{\"path\":\".\",\"include_text_stats\":\"maybe\"}").GetString("error"));
			NativeMethods.SetFileBaseDir("");
			Assert.AreEqual("invalid path", Info("relative.txt").GetString("error"));
			Assert.AreEqual("directory", Info(_directory).GetString("type"));
		}

		[TestMethod]
		public void DoesNotOpenSymbolicLinksForTextStatistics(){
			var target = Write("target.txt", "contents");
			var link = Path.Combine(_directory, "link.txt");
			if(!CreateSymbolicLink(link, target, 0)) Assert.Inconclusive("Creating a symbolic link requires the symlink privilege.");
			try{
				var info = Info("link.txt");
				Assert.AreEqual("reparse_point", info.GetString("type"));
				Assert.AreEqual(true, info.GetBool("reparse_point"));
				Assert.AreEqual("not_a_regular_file", info["text"].GetString("reason"));
			} finally{ File.Delete(link); }
		}

		private string Write(string name, string text){
			var path = Path.Combine(_directory, name);
			File.WriteAllText(path, text, new UTF8Encoding(false));
			return path;
		}
		private static JsonNode Info(string path, bool includeText = true){
			var arguments = Json.Object(Json.P("path", path), Json.P("include_text_stats", includeText)).ToJson();
			return ExecuteInfo(arguments);
		}
		private static JsonNode ExecuteInfo(string arguments){ return Json.Parse(NativeMethods.ReadUtf8AndFree(NativeMethods.ExecuteTool(Slot, "get_file_info", arguments))); }
		[DllImport("kernel32.dll", EntryPoint = "CreateSymbolicLinkW", CharSet = CharSet.Unicode, SetLastError = true)]
		[return: MarshalAs(UnmanagedType.I1)]
		private static extern bool CreateSymbolicLink(string link, string target, int flags);
	}
}
