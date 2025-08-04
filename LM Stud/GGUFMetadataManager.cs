using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
namespace LMStud{
	internal static class GGUFMetadataManager{
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
		// Public method analogous to C++ LoadGGUFMetadata(const char* filename)
		public static List<GGUFMetadataEntry> LoadGGUFMetadata(string filename){
			var metadata = new List<GGUFMetadataEntry>();
			// Open the file in binary mode
			using(var fs = new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.Read))
			using(var br = new BinaryReader(fs)){
				// Read the 'magic' and check it
				var magic = br.ReadUInt32();
				if(magic != 0x46554747)// "GGUF" in little-endian
					return null;
				// Read version (not used here, but must be consumed)
				br.ReadUInt32();
				// Read counts
				br.ReadUInt64();
				var metadataCount = br.ReadUInt64();
				metadata.Capacity = (int)metadataCount;
				// Read each metadata entry
				for(ulong i = 0; i < metadataCount; i++){
					var entry = ReadMetadataEntry(br);
					metadata.Add(entry);
				}
			}
			return metadata;
		}
		// This function searches the stored metadata for the context length
		public static int GetGGUFCtxMax(List<GGUFMetadataEntry> metadata){
			foreach(var entry in metadata.Where(entry => entry.Key.EndsWith(".context_length")))
				switch(entry.Val.Value){
					// We only handle int32/uint32 in the snippet
					case uint uval: return (int)uval;
					case int ival: return ival;
					default:
						// If it's some other type, return -1
						return -1;
				}
			// If not found, return -1
			return -1;
		}
		// Reads one metadata entry (key + value)
		private static GGUFMetadataEntry ReadMetadataEntry(BinaryReader br){
			// First read the key (a length-prefixed string)
			var key = ReadLengthPrefixedString(br);
			// Next read the type
			var typeCode = br.ReadUInt32();
			var type = (GGUFType)typeCode;
			// Read the value based on the type
			var val = new GGUFMetaValue{ Type = type, Value = ReadMetaValue(br, type) };
			return new GGUFMetadataEntry{ Key = key, Val = val };
		}
		// Recursively reads a metadata value for a given type
		private static object ReadMetaValue(BinaryReader br, GGUFType type){
			switch(type){
				case GGUFType.UINT8: return br.ReadByte();
				case GGUFType.INT8:
					// ReadByte returns byte; cast to sbyte
					return (sbyte)br.ReadByte();
				case GGUFType.UINT16: return br.ReadUInt16();
				case GGUFType.INT16: return br.ReadInt16();
				case GGUFType.UINT32: return br.ReadUInt32();
				case GGUFType.INT32: return br.ReadInt32();
				case GGUFType.FLOAT32: return br.ReadSingle();
				case GGUFType.UINT64: return br.ReadUInt64();
				case GGUFType.INT64: return br.ReadInt64();
				case GGUFType.FLOAT64: return br.ReadDouble();
				case GGUFType.BOOL:
					// Stored as a single byte, 0 or 1
					return br.ReadByte() != 0;
				case GGUFType.STRING: return ReadLengthPrefixedString(br);
				case GGUFType.ARRAY:
					// Arrays have a subtype and a count
					var subtypeCode = br.ReadUInt32();
					var subtype = (GGUFType)subtypeCode;
					var count = br.ReadUInt64();
					var arr = new List<object>();
					for(ulong i = 0; i < count; i++) arr.Add(ReadMetaValue(br, subtype));
					return arr;
				default: throw new Exception("Unknown GGUF type code: " + type);
			}
		}
		// Helper for reading a 64-bit-length-prefixed string
		private static string ReadLengthPrefixedString(BinaryReader br){
			var length = br.ReadUInt64();
			if(length == 0) return string.Empty;

			// Read the bytes and construct a string
			var bytes = br.ReadBytes((int)length);
			return Encoding.UTF8.GetString(bytes);
		}
// A container for the read value plus its type
		public struct GGUFMetaValue{
			public GGUFType Type{get; set;}
			public object Value{get; set;}
		}
// Each metadata entry: a key plus a value
		public struct GGUFMetadataEntry{
			public string Key{get; set;}
			public GGUFMetaValue Val{get; set;}
		}
	}
}