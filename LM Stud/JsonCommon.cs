using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace LMStud{
	internal enum JsonKind{
		Missing = -1,
		Invalid = 0,
		Null = 1,
		Boolean = 2,
		Number = 3,
		String = 4,
		Object = 5,
		Array = 6
	}
	internal enum JsonFormat{
		Compact,
		Indented
	}
	internal readonly struct JsonMember{
		internal readonly string Name;
		internal readonly object Value;
		internal JsonMember(string name, object value){ Name = name; Value = value; }
	}
	internal readonly struct JsonProperty{
		internal readonly string Name;
		internal readonly JsonNode Value;
		internal JsonProperty(string name, JsonNode value){ Name = name; Value = value; }
	}
	internal readonly struct JsonNode : IEnumerable<JsonNode>{
		private readonly string _raw;
		private readonly bool _missing;
		internal JsonNode(string raw){ _raw = raw ?? "null"; _missing = false; }
		private JsonNode(bool missing){ _raw = null; _missing = missing; }
		internal static JsonNode Missing => new JsonNode(true);
		internal static JsonNode Null => new JsonNode("null");
		internal bool Exists => !_missing;
		internal string Raw => _missing ? null : _raw ?? "null";
		internal JsonKind Kind => _missing ? JsonKind.Missing : (JsonKind)NativeMethods.JsonGetKind(Raw);
		internal bool IsNull => !Exists || Kind == JsonKind.Null;
		internal bool IsObject => Kind == JsonKind.Object;
		internal bool IsArray => Kind == JsonKind.Array;
		internal bool IsString => Kind == JsonKind.String;
		internal int Count => IsArray ? Math.Max(0, NativeMethods.JsonArrayCount(Raw)) : 0;
		internal JsonNode this[string key]{
			get{
				if(!IsObject || key == null) return Missing;
				var raw = NativeMethods.ReadUtf8AndFreeOptional(NativeMethods.JsonGetProperty(Raw, key));
				if(raw != null) return new JsonNode(raw);
				foreach(var property in Properties())
					if(string.Equals(property.Name, key, StringComparison.OrdinalIgnoreCase)) return property.Value;
				return Missing;
			}
		}
		internal JsonNode this[int index]{
			get{
				if(!IsArray) return Missing;
				var raw = NativeMethods.ReadUtf8AndFreeOptional(NativeMethods.JsonArrayItem(Raw, index));
				return raw == null ? Missing : new JsonNode(raw);
			}
		}
		internal string GetString(string key){ return this[key].AsString(); }
		internal int? GetInt(string key){ return this[key].AsInt(); }
		internal long? GetLong(string key){ return this[key].AsLong(); }
		internal double? GetDouble(string key){ return this[key].AsDouble(); }
		internal bool? GetBool(string key){ return this[key].AsBool(); }
		internal string AsString(){
			if(IsNull) return null;
			if(IsString) return NativeMethods.ReadUtf8AndFreeOptional(NativeMethods.JsonReadStringValue(Raw));
			return Raw;
		}
		internal int? AsInt(){
			var value = AsLong();
			if(!value.HasValue) return null;
			return value.Value < int.MinValue || value.Value > int.MaxValue ? (int?)null : (int)value.Value;
		}
		internal double? AsDouble(){
			if(IsNull) return null;
			double value;
			return double.TryParse(AsString(), NumberStyles.Float, CultureInfo.InvariantCulture, out value) ? (double?)value : null;
		}
		internal long? AsLong(){
			if(IsNull) return null;
			long value;
			return long.TryParse(AsString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out value) ? (long?)value : null;
		}
		internal bool? AsBool(){
			if(IsNull) return null;
			bool value;
			return bool.TryParse(AsString(), out value) ? (bool?)value : null;
		}
		internal IEnumerable<JsonProperty> Properties(){
			if(!IsObject) yield break;
			var keysRaw = NativeMethods.ReadUtf8AndFreeOptional(NativeMethods.JsonObjectKeys(Raw));
			if(string.IsNullOrWhiteSpace(keysRaw)) yield break;
			foreach(var keyNode in new JsonNode(keysRaw)){
				var key = keyNode.AsString();
				if(key != null) yield return new JsonProperty(key, this[key]);
			}
		}
		internal string ToJson(JsonFormat format = JsonFormat.Compact){
			if(_missing) return "null";
			if(format == JsonFormat.Compact) return Raw;
			var pretty = NativeMethods.JsonPrettyNative(Raw);
			if(pretty.Length == 0) return Raw;
			try{ if(new JsonNode(pretty).Kind != JsonKind.Invalid) return pretty; }
			catch{}
			return Raw;
		}
		public override string ToString(){ return ToJson(JsonFormat.Compact); }
		public IEnumerator<JsonNode> GetEnumerator(){
			for(var i = 0; i < Count; ++i) yield return this[i];
		}
		IEnumerator IEnumerable.GetEnumerator(){ return GetEnumerator(); }
	}
	internal sealed class JsonArrayBuilder : IEnumerable<JsonNode>{
		private readonly List<JsonNode> _items = new List<JsonNode>();
		internal int Count => _items.Count;
		internal JsonNode this[int index] => _items[index];
		internal void Add(object value){ _items.Add(Json.Value(value)); }
		internal void Insert(int index, object value){ _items.Insert(index, Json.Value(value)); }
		internal JsonNode ToNode(){ return new JsonNode(Json.BuildArrayRaw(_items)); }
		internal string ToJson(JsonFormat format = JsonFormat.Compact){ return ToNode().ToJson(format); }
		public override string ToString(){ return ToJson(); }
		public IEnumerator<JsonNode> GetEnumerator(){ return _items.GetEnumerator(); }
		IEnumerator IEnumerable.GetEnumerator(){ return GetEnumerator(); }
	}
	internal sealed class JsonObjectBuilder{
		private readonly List<string> _order = new List<string>();
		private readonly Dictionary<string, JsonNode> _values = new Dictionary<string, JsonNode>();
		internal JsonNode this[string key]{
			get{ return _values.TryGetValue(key, out var value) ? value : JsonNode.Missing; }
			set{ Set(key, value); }
		}
		internal void Set(string key, object value){
			if(key == null) throw new ArgumentNullException(nameof(key));
			if(!_values.ContainsKey(key)) _order.Add(key);
			_values[key] = Json.Value(value);
		}
		internal JsonNode ToNode(){
			var json = new StringBuilder();
			json.Append('{');
			for(var i = 0; i < _order.Count; ++i){
				if(i > 0) json.Append(',');
				var key = _order[i];
				json.Append(Json.String(key).Raw);
				json.Append(':');
				json.Append(_values[key].Raw ?? "null");
			}
			json.Append('}');
			return new JsonNode(json.ToString());
		}
		internal string ToJson(JsonFormat format = JsonFormat.Compact){ return ToNode().ToJson(format); }
		public override string ToString(){ return ToJson(); }
	}
	internal static class Json{
		private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;
		internal static JsonMember P(string name, object value){ return new JsonMember(name, value); }
		internal static JsonNode Parse(string raw){
			if(string.IsNullOrWhiteSpace(raw)) throw new InvalidOperationException("Invalid JSON.");
			var node = new JsonNode(raw);
			if(node.Kind == JsonKind.Invalid) throw new InvalidOperationException("Invalid JSON.");
			return node;
		}
		internal static JsonNode String(string value){ return new JsonNode(NativeMethods.ReadUtf8AndFree(NativeMethods.JsonMakeString(value ?? ""))); }
		internal static JsonNode Object(params JsonMember[] members){
			var obj = new JsonObjectBuilder();
			foreach(var member in members ?? System.Array.Empty<JsonMember>()) obj.Set(member.Name, member.Value);
			return obj.ToNode();
		}
		internal static JsonNode Array(params object[] values){
			var array = new JsonArrayBuilder();
			foreach(var value in values ?? System.Array.Empty<object>()) array.Add(value);
			return array.ToNode();
		}
		internal static JsonArrayBuilder ArrayBuilder(){ return new JsonArrayBuilder(); }
		internal static JsonObjectBuilder ObjectBuilder(params JsonMember[] members){
			var obj = new JsonObjectBuilder();
			foreach(var member in members ?? System.Array.Empty<JsonMember>()) obj.Set(member.Name, member.Value);
			return obj;
		}
		internal static JsonNode Value(object value){
			if(value == null) return JsonNode.Null;
			if(value is JsonNode node) return node.Exists ? node : JsonNode.Null;
			if(value is JsonArrayBuilder array) return array.ToNode();
			if(value is JsonObjectBuilder obj) return obj.ToNode();
			if(value is string text) return String(text);
			if(value is bool flag) return new JsonNode(flag ? "true" : "false");
			if(value is Enum) return new JsonNode(Convert.ToInt64(value, Invariant).ToString(Invariant));
			if(value is float single){
				if(float.IsNaN(single) || float.IsInfinity(single)) throw new InvalidOperationException("Unsupported JSON number: " + single.ToString(Invariant));
				return new JsonNode(single.ToString(Invariant));
			}
			if(value is double dbl){
				if(double.IsNaN(dbl) || double.IsInfinity(dbl)) throw new InvalidOperationException("Unsupported JSON number: " + dbl.ToString(Invariant));
				return new JsonNode(dbl.ToString(Invariant));
			}
			if(value is byte || value is sbyte || value is short || value is ushort || value is int || value is uint || value is long || value is ulong || value is decimal)
				return new JsonNode(Convert.ToString(value, Invariant));
			if(value is IEnumerable<JsonNode> nodes) return new JsonNode(BuildArrayRaw(nodes));
			throw new InvalidOperationException("Unsupported JSON value type: " + value.GetType().Name);
		}
		internal static string BuildArrayRaw(IEnumerable<JsonNode> nodes){
			var json = new StringBuilder();
			json.Append('[');
			var first = true;
			foreach(var item in nodes ?? System.Array.Empty<JsonNode>()){
				if(first) first = false;
				else json.Append(',');
				json.Append(item.Raw ?? "null");
			}
			json.Append(']');
			return json.ToString();
		}
	}
}
