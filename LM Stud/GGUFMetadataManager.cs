using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
namespace LMStud{
	public static class GGUFMetadataManager{
		public enum GGUFType : uint{
			UINT8 = 0,
			INT8 = 1,
			UINT16 = 2,
			INT16 = 3,
			UINT32 = 4,
			INT32 = 5,
			FLOAT32 = 6,
			BOOL = 7,
			STRING = 8,
			ARRAY = 9,
			UINT64 = 10,
			INT64 = 11,
			FLOAT64 = 12
		}
		internal const uint Magic = 0x46554747;
		internal const int ArrayPreviewLimit = 32;
		public static List<GGUFMetadataEntry> LoadGGUFMetadata(string filename){return LoadGGUFMetadata(filename, null, out _);}
		internal static List<GGUFMetadataEntry> LoadGGUFMetadataPreview(string filename, out bool checkMtpHead){return LoadGGUFMetadata(filename, new byte[1024], out checkMtpHead);}
		private static List<GGUFMetadataEntry> LoadGGUFMetadata(string filename, byte[] skipBuffer, out bool checkMtpHead){
			checkMtpHead = false;
			var metadata = new List<GGUFMetadataEntry>();
			using(var fs = new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.Read, 64*1024)){
				using(var br = new BinaryReader(fs)){
					var magic = br.ReadUInt32();
					if(magic != Magic) return null;
					br.ReadUInt32();
					var tensorCount = br.ReadUInt64();
					var metadataCount = br.ReadUInt64();
					metadata.Capacity = (int)metadataCount;
					for(ulong i = 0; i < metadataCount; i++){
						var entry = ReadMetadataEntry(br, skipBuffer);
						metadata.Add(entry);
					}
					if(skipBuffer != null) checkMtpHead = NeedsMtpHeadCheck(br, metadata, tensorCount, skipBuffer);
				}
			}
			return metadata;
		}
		private static bool NeedsMtpHeadCheck(BinaryReader br, List<GGUFMetadataEntry> metadata, ulong tensorCount, byte[] buffer){
			var architecture = metadata.Find(entry => entry.Key == "general.architecture").Val.Value as string;
			if(string.IsNullOrEmpty(architecture)) return false;
			var key = architecture + ".nextn_predict_layers";
			var value = metadata.Find(entry => entry.Key == key).Val.Value;
			var headBlocks = value is uint n ? n : value is ushort shortCount ? shortCount : 0u;
			if(headBlocks == 0) return false;
			// Block zero proves this file includes the trunk when some blocks precede the head.
			// Let the native check handle head-only files and splits without that block here.
			if(GetGGUFLayerCount(metadata) <= headBlocks) return true;
			try{
				for(ulong i = 0; i < tensorCount; i++){
					var length = br.ReadUInt64();
					if(length > (ulong)buffer.Length) break;
					var name = Encoding.UTF8.GetString(br.ReadBytes((int)length));
					if(name.StartsWith("blk.0.", StringComparison.Ordinal)) return false;
					var dimensions = br.ReadUInt32();
					SkipBytes(br, dimensions*8UL + 12, buffer);
				}
			} catch(EndOfStreamException){}
			return true;
		}
		public static int GetGGUFCtxMax(List<GGUFMetadataEntry> metadata){
			foreach(var entry in metadata.Where(entry => entry.Key.EndsWith(".context_length")))
				switch(entry.Val.Value){
					case uint uval: return (int)uval;
					case int ival: return ival;
					default: return -1;
				}
			return -1;
		}
		public static int GetGGUFLayerCount(List<GGUFMetadataEntry> metadata){
			var architecture = metadata.Find(entry => entry.Key == "general.architecture").Val.Value as string;
			if(string.IsNullOrEmpty(architecture)) return 0;
			var key = architecture + ".block_count";
			switch(metadata.Find(entry => entry.Key == key).Val.Value){
				case uint count when count < int.MaxValue: return (int)count;
				case int count when count > 0 && count < int.MaxValue: return count;
				case ulong count when count < int.MaxValue: return (int)count;
				case long count when count > 0 && count < int.MaxValue: return (int)count;
				default: return 0;
			}
		}
		private static GGUFMetadataEntry ReadMetadataEntry(BinaryReader br, byte[] skipBuffer){
			var key = ReadLengthPrefixedString(br);
			var type = (GGUFType)br.ReadUInt32();
			var value = ReadMetaValue(br, type, skipBuffer, out var arrayCount);
			var val = new GGUFMetaValue{ Type = type, Value = value, ArrayCount = arrayCount };
			return new GGUFMetadataEntry{ Key = key, Val = val };
		}
		private static object ReadMetaValue(BinaryReader br, GGUFType type, byte[] skipBuffer, out ulong arrayCount){
			arrayCount = 0;
			switch(type){
				case GGUFType.UINT8: return br.ReadByte();
				case GGUFType.INT8: return (sbyte)br.ReadByte();
				case GGUFType.UINT16: return br.ReadUInt16();
				case GGUFType.INT16: return br.ReadInt16();
				case GGUFType.UINT32: return br.ReadUInt32();
				case GGUFType.INT32: return br.ReadInt32();
				case GGUFType.FLOAT32: return br.ReadSingle();
				case GGUFType.UINT64: return br.ReadUInt64();
				case GGUFType.INT64: return br.ReadInt64();
				case GGUFType.FLOAT64: return br.ReadDouble();
				case GGUFType.BOOL: return br.ReadByte() != 0;
				case GGUFType.STRING: return ReadLengthPrefixedString(br);
				case GGUFType.ARRAY:
					var subtype = (GGUFType)br.ReadUInt32();
					arrayCount = br.ReadUInt64();
					var retained = skipBuffer == null ? arrayCount : Math.Min(arrayCount, ArrayPreviewLimit);
					var arr = new List<object>();
					for(ulong i = 0; i < retained; i++) arr.Add(ReadMetaValue(br, subtype, skipBuffer, out _));
					if(retained < arrayCount) SkipValues(br, subtype, arrayCount - retained, skipBuffer);
					return arr;
				default: throw new Exception("Unknown GGUF type code: " + type);
			}
		}
		private static void SkipValues(BinaryReader br, GGUFType type, ulong count, byte[] buffer){
			switch(type){
				case GGUFType.UINT8:
				case GGUFType.INT8:
				case GGUFType.BOOL:
					SkipBytes(br, count, buffer);
					break;
				case GGUFType.UINT16:
				case GGUFType.INT16:
					SkipBytes(br, checked(count*2), buffer);
					break;
				case GGUFType.UINT32:
				case GGUFType.INT32:
				case GGUFType.FLOAT32:
					SkipBytes(br, checked(count*4), buffer);
					break;
				case GGUFType.UINT64:
				case GGUFType.INT64:
				case GGUFType.FLOAT64:
					SkipBytes(br, checked(count*8), buffer);
					break;
				case GGUFType.STRING:
					for(ulong i = 0; i < count; i++) SkipBytes(br, br.ReadUInt64(), buffer);
					break;
				case GGUFType.ARRAY:
					for(ulong i = 0; i < count; i++) SkipValues(br, (GGUFType)br.ReadUInt32(), br.ReadUInt64(), buffer);
					break;
				default: throw new Exception("Unknown GGUF type code: " + type);
			}
		}
		private static void SkipBytes(BinaryReader br, ulong count, byte[] buffer){
			if(count <= (ulong)buffer.Length){
				for(var remaining = (int)count; remaining > 0;){
					var read = br.Read(buffer, 0, remaining);
					if(read == 0) throw new EndOfStreamException();
					remaining -= read;
				}
				return;
			}
			var stream = br.BaseStream;
			if(stream.Seek(checked((long)count), SeekOrigin.Current) > stream.Length) throw new EndOfStreamException();
		}
		private static string ReadLengthPrefixedString(BinaryReader br){
			var length = br.ReadUInt64();
			if(length == 0) return string.Empty;
			var bytes = br.ReadBytes((int)length);
			return Encoding.UTF8.GetString(bytes);
		}
		public struct GGUFMetaValue{
			public GGUFType Type{get; set;}
			public object Value{get; set;}
			internal ulong ArrayCount{get; set;}
		}
		public struct GGUFMetadataEntry{
			public string Key{get; set;}
			public GGUFMetaValue Val{get; set;}
		}
	}
}