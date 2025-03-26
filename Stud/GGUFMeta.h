#pragma once
#include <string>
#include <variant>
#include <vector>
enum class GGUFType : uint32_t{
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
	// (values 13+ are not used in current spec)
};
struct GGUFMetaValue{
	GGUFType type = GGUFType::UINT32;
	// Use std::variant to hold any possible value type:
	std::variant<uint8_t, int8_t, uint16_t, int16_t, uint32_t, int32_t, float, uint64_t, int64_t, double, bool, std::string, std::vector<GGUFMetaValue>> value;
};
struct GGUFMetadataEntry{
	std::string key;
	GGUFMetaValue val = {};
};
GGUFMetadataEntry readMetadataEntry(std::ifstream& fin);
GGUFMetaValue readMetaValue(std::ifstream& fin, GGUFType type);