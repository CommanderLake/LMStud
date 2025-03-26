#include "GGUFMeta.h"
#include <variant>
#include <string>
#include <vector>
#include <iostream>
#include <fstream>
#include <cstdint>
// Helper to read a little-endian 64-bit length-prefixed string:
std::string readLengthPrefixedString(std::ifstream& fin){
	uint64_t length = 0;
	fin.read(reinterpret_cast<char*>(&length), sizeof(length));
	std::string s(length, '\0');
	if(length>0){ fin.read(s.data(), length); }
	return s;
}
// Parse one metadata entry (key and value)
GGUFMetadataEntry readMetadataEntry(std::ifstream& fin){
	// Read key (GGUF string: 64-bit length + bytes)
	std::string key = readLengthPrefixedString(fin);
	// Read value type code (32-bit)
	uint32_t type_code = 0;
	fin.read(reinterpret_cast<char*>(&type_code), sizeof(type_code));
	auto type = static_cast<GGUFType>(type_code);
	// Read the value based on type
	GGUFMetaValue val;
	val.type = type;
	switch(type){
		case GGUFType::UINT8: {
			uint8_t x = 0;
			fin.read(reinterpret_cast<char*>(&x), sizeof(x));
			val.value = x;
			break;
		}
		case GGUFType::INT8: {
			int8_t x = 0;
			fin.read(reinterpret_cast<char*>(&x), sizeof(x));
			val.value = x;
			break;
		}
		case GGUFType::UINT16: {
			uint16_t x = 0;
			fin.read(reinterpret_cast<char*>(&x), sizeof(x));
			val.value = x;
			break;
		}
		case GGUFType::INT16: {
			int16_t x = 0;
			fin.read(reinterpret_cast<char*>(&x), sizeof(x));
			val.value = x;
			break;
		}
		case GGUFType::UINT32: {
			uint32_t x = 0;
			fin.read(reinterpret_cast<char*>(&x), sizeof(x));
			val.value = x;
			break;
		}
		case GGUFType::INT32: {
			int32_t x = 0;
			fin.read(reinterpret_cast<char*>(&x), sizeof(x));
			val.value = x;
			break;
		}
		case GGUFType::FLOAT32: {
			float f = 0.0f;
			fin.read(reinterpret_cast<char*>(&f), sizeof(f));
			val.value = f;
			break;
		}
		case GGUFType::UINT64: {
			uint64_t x = 0;
			fin.read(reinterpret_cast<char*>(&x), sizeof(x));
			val.value = x;
			break;
		}
		case GGUFType::INT64: {
			int64_t x = 0;
			fin.read(reinterpret_cast<char*>(&x), sizeof(x));
			val.value = x;
			break;
		}
		case GGUFType::FLOAT64: {
			double d = 0.0;
			fin.read(reinterpret_cast<char*>(&d), sizeof(d));
			val.value = d;
			break;
		}
		case GGUFType::BOOL: {
			uint8_t b = 0;
			fin.read(reinterpret_cast<char*>(&b), sizeof(b));
			val.value = static_cast<bool>(b!=0);
			break;
		}
		case GGUFType::STRING: {
			std::string strVal = readLengthPrefixedString(fin);
			val.value = strVal;
			break;
		}
		case GGUFType::ARRAY: {
			// For an array, read the element type and count, then read each element
			uint32_t subtype_code = 0;
			fin.read(reinterpret_cast<char*>(&subtype_code), sizeof(subtype_code));
			auto subtype = static_cast<GGUFType>(subtype_code);
			uint64_t count = 0;
			fin.read(reinterpret_cast<char*>(&count), sizeof(count));
			std::vector<GGUFMetaValue> arr;
			arr.reserve(count);
			for(uint64_t i = 0; i<count; ++i){
				// Recursively read a value of the given subtype
				GGUFMetaValue elemVal = readMetaValue(fin, subtype);
				arr.push_back(elemVal);
			}
			val.value = arr;
			break;
		}
		default: throw std::runtime_error("Unknown metadata value type code");
	}
	return {key, val};
}
// Helper to read a metadata value when the type is already known (used for array elements):
GGUFMetaValue readMetaValue(std::ifstream& fin, GGUFType type){
	// This function is similar to the switch above but without reading a new type code from file.
	GGUFMetaValue val;
	val.type = type;
	switch(type){
		case GGUFType::UINT8: {
			uint8_t x;
			fin.read(reinterpret_cast<char*>(&x), 1);
			val.value = x;
			break;
		}
		case GGUFType::INT8: {
			int8_t x;
			fin.read(reinterpret_cast<char*>(&x), 1);
			val.value = x;
			break;
		}
		case GGUFType::UINT16: {
			uint16_t x;
			fin.read(reinterpret_cast<char*>(&x), 2);
			val.value = x;
			break;
		}
		case GGUFType::INT16: {
			int16_t x;
			fin.read(reinterpret_cast<char*>(&x), 2);
			val.value = x;
			break;
		}
		case GGUFType::UINT32: {
			uint32_t x;
			fin.read(reinterpret_cast<char*>(&x), 4);
			val.value = x;
			break;
		}
		case GGUFType::INT32: {
			int32_t x;
			fin.read(reinterpret_cast<char*>(&x), 4);
			val.value = x;
			break;
		}
		case GGUFType::FLOAT32: {
			float f;
			fin.read(reinterpret_cast<char*>(&f), 4);
			val.value = f;
			break;
		}
		case GGUFType::UINT64: {
			uint64_t x;
			fin.read(reinterpret_cast<char*>(&x), 8);
			val.value = x;
			break;
		}
		case GGUFType::INT64: {
			int64_t x;
			fin.read(reinterpret_cast<char*>(&x), 8);
			val.value = x;
			break;
		}
		case GGUFType::FLOAT64: {
			double d;
			fin.read(reinterpret_cast<char*>(&d), 8);
			val.value = d;
			break;
		}
		case GGUFType::BOOL: {
			uint8_t b;
			fin.read(reinterpret_cast<char*>(&b), 1);
			val.value = static_cast<bool>(b!=0);
			break;
		}
		case GGUFType::STRING: {
			std::string s = readLengthPrefixedString(fin);
			val.value = s;
			break;
		}
		case GGUFType::ARRAY: {
			// Nested array: read just like in readMetadataEntry (recursively)
			uint32_t subtype_code;
			fin.read(reinterpret_cast<char*>(&subtype_code), 4);
			auto subtype = static_cast<GGUFType>(subtype_code);
			uint64_t count;
			fin.read(reinterpret_cast<char*>(&count), 8);
			std::vector<GGUFMetaValue> arr;
			arr.reserve(count);
			for(uint64_t i = 0; i<count; ++i){ arr.push_back(readMetaValue(fin, subtype)); }
			val.value = arr;
			break;
		}
		default: throw std::runtime_error("Unknown subtype in array");
	}
	return val;
}