using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LMStud.Properties;
namespace LMStud{
	internal static class HuggingFaceDownload{
		internal static string Download(string repositoryDirectory, string[] files, Func<string, string> getUrl, Func<double, bool> reportProgress){
			var destinations = files.Select(file => GetDestinationPath(repositoryDirectory, file)).ToArray();
			var staged = destinations.Select(path => Path.Combine(Path.GetDirectoryName(path), Guid.NewGuid().ToString("N") + ".part")).ToArray();
			try{
				for(var index = 0; index < files.Length; index++){
					var part = index;
					if(!reportProgress((double)part/files.Length)) throw new OperationCanceledException();
					Directory.CreateDirectory(Path.GetDirectoryName(staged[part]));
					var result = NativeMethods.DownloadFileWithProgress(getUrl(files[part]), staged[part], (total, received) => {
						var fraction = total > 0 ? Math.Min(1.0, (double)received/total) : 0.0;
						return reportProgress((part + fraction)/files.Length) ? 0 : 1;
					});
					if(result != 0) throw new IOException(files[part] + "\r\n" + string.Format(Resources.Download_failed_with_error_code___0_, result));
					if(files[part].EndsWith(".gguf", StringComparison.OrdinalIgnoreCase))
						using(var reader = new BinaryReader(File.OpenRead(staged[part])))
							if(reader.BaseStream.Length < 24 || reader.ReadUInt32() != GGUFMetadataManager.Magic)
								throw new InvalidDataException(string.Format(Resources.Download_Invalid_GGUF, files[part]));
				}
				if(!reportProgress(1.0)) throw new OperationCanceledException();
				InstallFiles(destinations, staged);
				return destinations[0];
			} finally{
				foreach(var path in staged) TryDelete(path);
			}
		}
		private static string GetDestinationPath(string repositoryDirectory, string relativePath){
			var directory = Path.GetFullPath(repositoryDirectory).TrimEnd('\\', '/') + Path.DirectorySeparatorChar;
			var path = Path.GetFullPath(Path.Combine(directory, relativePath));
			if(Path.IsPathRooted(relativePath) || !path.StartsWith(directory, StringComparison.OrdinalIgnoreCase))
				throw new InvalidDataException(string.Format(Resources.Download_Invalid_Path, relativePath));
			return path;
		}
		private static void InstallFiles(string[] destinations, string[] staged){
			var backups = new string[destinations.Length];
			var installed = new bool[destinations.Length];
			try{
				for(var index = 0; index < destinations.Length; index++){
					if(!File.Exists(destinations[index])) continue;
					var backup = staged[index] + ".previous";
					File.Move(destinations[index], backup);
					backups[index] = backup;
				}
				for(var index = destinations.Length - 1; index >= 0; index--){
					File.Move(staged[index], destinations[index]);
					installed[index] = true;
				}
			} catch(Exception installError){
				var errors = new List<Exception>{ installError };
				for(var index = destinations.Length - 1; index >= 0; index--){
					try{
						if(installed[index]) File.Move(destinations[index], staged[index]);
						if(backups[index] != null) File.Move(backups[index], destinations[index]);
					} catch(Exception rollbackError){ errors.Add(rollbackError); }
				}
				if(errors.Count > 1) throw new AggregateException(errors);
				throw;
			}
			foreach(var backup in backups) if(backup != null) TryDelete(backup);
		}
		private static void TryDelete(string path){
			try{ File.Delete(path); }
			catch(IOException){} catch(UnauthorizedAccessException){}
		}
	}
}
