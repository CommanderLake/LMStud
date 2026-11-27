using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using LMStud.Properties;
namespace LMStud{
	[DataContract]
	internal sealed class ChatArchive{
		private const int CurrentVersion = 1;
		private const int MoveFileReplaceExisting = 0x01;
		private const int MoveFileWriteThrough = 0x08;
		[DataMember(Name = "version", IsRequired = true, Order = 0)]
		private int _version = CurrentVersion;
		[DataMember(Name = "draft", Order = 1)]
		internal string Draft;
		[DataMember(Name = "localHistory", EmitDefaultValue = false, Order = 3)]
		internal string LocalHistory;
		[DataMember(Name = "messages", IsRequired = true, Order = 2)]
		internal List<Message> Messages;
		[DataMember(Name = "partnerHistory", EmitDefaultValue = false, Order = 4)]
		internal string PartnerHistory;
		[DataMember(Name = "partnerTurn", EmitDefaultValue = false, Order = 5)]
		internal bool PartnerTurn;
		[DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
		[return: MarshalAs(UnmanagedType.Bool)]
		private static extern bool MoveFileEx(string existingFileName, string newFileName, int flags);
		internal void EmbedLocalImages(){RewriteImageSources(MarkdownImages.GetLocalImageDataUrl);}
		internal void RestoreEmbeddedImages(List<string> restoredFiles){
			RewriteImageSources(source => {
				var path = MarkdownImages.RestoreEmbeddedImage(source);
				if(path == null) return null;
				restoredFiles.Add(path);
				return new Uri(path).AbsoluteUri;
			});
		}
		private void RewriteImageSources(Func<string, string> rewriteSource){
			var replacements = new Dictionary<string, string>(StringComparer.Ordinal);
			string GetReplacement(string source){
				if(replacements.TryGetValue(source, out var replacement)) return replacement;
				replacement = rewriteSource(source);
				replacements.Add(source, replacement);
				return replacement;
			}
			Draft = MarkdownImages.ReplaceImageSources(Draft, GetReplacement);
			foreach(var message in Messages){
				if(message.Role == MessageRole.Tool) continue;
				message.Text = MarkdownImages.ReplaceImageSources(message.Text, GetReplacement);
				message.Reasoning = MarkdownImages.ReplaceImageSources(message.Reasoning, GetReplacement);
				if(message.Role == MessageRole.User) message.ApiContent = MarkdownImages.ReplaceImageSources(message.ApiContent, GetReplacement);
			}
		}
		internal static ChatArchive Read(string path){
			ChatArchive archive;
			using(var stream = File.OpenRead(path)){ archive = (ChatArchive)new DataContractJsonSerializer(typeof(ChatArchive)).ReadObject(stream); }
			if(archive == null) throw new InvalidDataException(Resources.Invalid_Chat_Archive);
			if(archive._version != CurrentVersion) throw new InvalidDataException(Resources.Unsupported_Chat_Archive_Version);
			if(archive.Messages == null || archive.Messages.Any(message => message == null || message.Text == null || !Enum.IsDefined(typeof(MessageRole), message.Role)))
				throw new InvalidDataException(Resources.Invalid_Chat_Archive);
			if(archive.Messages.Any(message => message.ApiOutputItems != null
												&& (message.Role != MessageRole.Assistant || string.IsNullOrEmpty(message.ApiReplayKey) || !Json.Parse(message.ApiOutputItems).IsArray)))
				throw new InvalidDataException(Resources.Invalid_Chat_Archive);
			if((archive.LocalHistory != null && !Json.Parse(archive.LocalHistory).IsArray) || (archive.PartnerHistory != null && !Json.Parse(archive.PartnerHistory).IsArray))
				throw new InvalidDataException(Resources.Invalid_Chat_Archive);
			return archive;
		}
		internal void Write(string path){
			path = Path.GetFullPath(path);
			var temporaryPath = Path.Combine(Path.GetDirectoryName(path), Guid.NewGuid().ToString("N") + ".tmp");
			try{
				using(var stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write)){
					new DataContractJsonSerializer(typeof(ChatArchive)).WriteObject(stream, this);
					stream.Flush(true);
				}
				if(!MoveFileEx(temporaryPath, path, MoveFileReplaceExisting | MoveFileWriteThrough)) throw new Win32Exception(Marshal.GetLastWin32Error());
			} finally{ File.Delete(temporaryPath); }
		}
		[DataContract]
		internal sealed class Message{
			[DataMember(Name = "apiContent", EmitDefaultValue = false, Order = 6)]
			internal string ApiContent;
			[DataMember(Name = "apiOutputItems", EmitDefaultValue = false, Order = 9)]
			internal string ApiOutputItems;
			[DataMember(Name = "apiReplayKey", EmitDefaultValue = false, Order = 10)]
			internal string ApiReplayKey;
			[DataMember(Name = "displayOnly", EmitDefaultValue = false, Order = 5)]
			internal bool DisplayOnly;
			[DataMember(Name = "reasoning", Order = 3)]
			internal string Reasoning;
			[DataMember(Name = "role", IsRequired = true, Order = 0)]
			internal MessageRole Role;
			[DataMember(Name = "showReasoning", EmitDefaultValue = false, Order = 4)]
			internal bool ShowReasoning;
			[DataMember(Name = "speaker", Order = 1)]
			internal string Speaker;
			[DataMember(Name = "text", IsRequired = true, Order = 2)]
			internal string Text;
			[DataMember(Name = "toolCallId", EmitDefaultValue = false, Order = 8)]
			internal string ToolCallId;
			[DataMember(Name = "toolCalls", EmitDefaultValue = false, Order = 7)]
			internal List<APIClient.ToolCall> ToolCalls;
			internal NativeChat.MessageSnapshot ToSnapshot(){return new NativeChat.MessageSnapshot(Role, Reasoning, Text, ApiContent, ToolCalls, ToolCallId, !DisplayOnly);}
		}
	}
}