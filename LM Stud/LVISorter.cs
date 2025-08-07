using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Windows.Forms;
namespace LMStud{
	public enum SortDataType{
		String,
		Integer,
		Double,
		DateTime,
		Boolean
	}
	public class LVISorter : IComparer{
		private static readonly NumberStyles NumberStyle = NumberStyles.Any;
		private static readonly CultureInfo Culture = CultureInfo.InvariantCulture;
		private readonly Dictionary<string, object> _parseCache;
		private int _columnIndex;
		private SortDataType _dataType;
		public LVISorter(){
			_columnIndex = 0;
			SortOrder = SortOrder.Ascending;
			_dataType = SortDataType.String;
			_parseCache = new Dictionary<string, object>(StringComparer.Ordinal);
		}
		public LVISorter(int columnIndex, SortDataType dataType, SortOrder sortOrder = SortOrder.Ascending){
			_columnIndex = columnIndex;
			SortOrder = sortOrder;
			_dataType = dataType;
			_parseCache = new Dictionary<string, object>(StringComparer.Ordinal);
		}
		public int ColumnIndex{
			get => _columnIndex;
			set{
				if(_columnIndex != value){
					_columnIndex = value;
					_parseCache.Clear();
				}
			}
		}
		public SortOrder SortOrder{get; set;}
		public SortDataType DataType{
			get => _dataType;
			set{
				if(_dataType != value){
					_dataType = value;
					_parseCache.Clear();
				}
			}
		}
		public int Compare(object x, object y){
			if(!(x is ListViewItem itemX) || !(y is ListViewItem itemY)) return 0;
			if(_columnIndex >= itemX.SubItems.Count || _columnIndex >= itemY.SubItems.Count) return 0;
			var textX = itemX.SubItems[_columnIndex].Text;
			var textY = itemY.SubItems[_columnIndex].Text;
			var result = CompareValues(textX, textY);
			return SortOrder == SortOrder.Ascending ? result : -result;
		}
		private int CompareValues(string textX, string textY){
			if(string.IsNullOrEmpty(textX) && string.IsNullOrEmpty(textY)) return 0;
			if(string.IsNullOrEmpty(textX)) return -1;
			if(string.IsNullOrEmpty(textY)) return 1;
			switch(_dataType){
				case SortDataType.String: return string.Compare(textX, textY, StringComparison.OrdinalIgnoreCase);
				case SortDataType.Integer: return CompareIntegers(textX, textY);
				case SortDataType.Double: return CompareDoubles(textX, textY);
				case SortDataType.DateTime: return CompareDateTimes(textX, textY);
				case SortDataType.Boolean: return CompareBooleans(textX, textY);
				default: return string.Compare(textX, textY, StringComparison.OrdinalIgnoreCase);
			}
		}
		private int CompareIntegers(string textX, string textY){
			var valueX = GetCachedInteger(textX);
			var valueY = GetCachedInteger(textY);
			if(!valueX.HasValue && !valueY.HasValue) return 0;
			if(!valueX.HasValue) return -1;
			if(!valueY.HasValue) return 1;
			return valueX.Value.CompareTo(valueY.Value);
		}
		private int CompareDoubles(string textX, string textY){
			var valueX = GetCachedDouble(textX);
			var valueY = GetCachedDouble(textY);
			if(!valueX.HasValue && !valueY.HasValue) return 0;
			if(!valueX.HasValue) return -1;
			if(!valueY.HasValue) return 1;
			return valueX.Value.CompareTo(valueY.Value);
		}
		private int CompareDateTimes(string textX, string textY){
			var valueX = GetCachedDateTime(textX);
			var valueY = GetCachedDateTime(textY);
			if(!valueX.HasValue && !valueY.HasValue) return 0;
			if(!valueX.HasValue) return -1;
			if(!valueY.HasValue) return 1;
			return valueX.Value.CompareTo(valueY.Value);
		}
		private int CompareBooleans(string textX, string textY){
			var valueX = GetCachedBoolean(textX);
			var valueY = GetCachedBoolean(textY);
			if(!valueX.HasValue && !valueY.HasValue) return 0;
			if(!valueX.HasValue) return -1;
			if(!valueY.HasValue) return 1;
			return valueX.Value.CompareTo(valueY.Value);
		}
		public void ClearCache(){_parseCache.Clear();}
		#region Cached Parsing Methods
		private int? GetCachedInteger(string text){
			if(_parseCache.TryGetValue(text, out var cached)) return cached as int?;
			var result = int.TryParse(text, NumberStyle, Culture, out var value) ? (int?)value : null;
			_parseCache[text] = result;
			return result;
		}
		private double? GetCachedDouble(string text){
			if(_parseCache.TryGetValue(text, out var cached)) return cached as double?;
			var result = double.TryParse(text, NumberStyle, Culture, out var value) ? (double?)value : null;
			_parseCache[text] = result;
			return result;
		}
		private DateTime? GetCachedDateTime(string text){
			if(_parseCache.TryGetValue(text, out var cached)) return cached as DateTime?;
			var result = DateTime.TryParse(text, Culture, DateTimeStyles.None, out var value) ? (DateTime?)value : null;
			_parseCache[text] = result;
			return result;
		}
		private bool? GetCachedBoolean(string text){
			if(_parseCache.TryGetValue(text, out var cached)) return cached as bool?;
			bool? result = null;
			if(bool.TryParse(text, out var value)) result = value;
			else
				switch(text.ToLowerInvariant()){
					case "yes":
					case "y":
					case "1":
					case "on":
						result = true;
						break;
					case "no":
					case "n":
					case "0":
					case "off":
						result = false;
						break;
				}
			_parseCache[text] = result;
			return result;
		}
		#endregion
	}
}