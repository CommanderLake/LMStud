using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
namespace LMStud{
	internal sealed class GGUFSplit{
		private static readonly Regex FilePattern = new Regex(@"^(.+)-([0-9]{5})(-of-([0-9]{5})\.gguf)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
		private readonly string _prefix;
		private readonly string _suffix;
		internal readonly int Number;
		internal readonly int Count;
		private GGUFSplit(string prefix, string suffix, int number, int count){
			_prefix = prefix;
			_suffix = suffix;
			Number = number;
			Count = count;
		}
		internal static GGUFSplit Parse(string path){
			if(string.IsNullOrEmpty(path)) return null;
			var match = FilePattern.Match(path);
			if(!match.Success) return null;
			var number = int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture);
			var count = int.Parse(match.Groups[4].Value, CultureInfo.InvariantCulture);
			return count > 1 && number >= 1 && number <= count ? new GGUFSplit(match.Groups[1].Value, match.Groups[3].Value, number, count) : null;
		}
		private string GetPath(int number){ return _prefix + "-" + number.ToString("D5", CultureInfo.InvariantCulture) + _suffix; }
		internal IEnumerable<string> Paths(){
			for(var number = 1; number <= Count; number++) yield return GetPath(number);
		}
		internal static string FirstPath(string path){ return Parse(path)?.GetPath(1) ?? path; }
		internal static string FindMissingPart(string path){
			var split = Parse(path);
			return split == null ? File.Exists(path) ? null : path : split.Paths().FirstOrDefault(part => !File.Exists(part));
		}
		internal static bool IsCompleteModel(string path){
			var split = Parse(path);
			return split == null || (split.Number == 1 && split.Paths().All(File.Exists));
		}
	}
}
