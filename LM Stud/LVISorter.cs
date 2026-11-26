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
		private const int MaxCachedValues = 16384;
		private const NumberStyles NumberStyle = NumberStyles.Any;

		private readonly Dictionary<string, IComparable> _parseCache = new Dictionary<string, IComparable>(StringComparer.Ordinal);
		private int _columnIndex;
		private CultureInfo _culture = CultureInfo.InvariantCulture;
		private SortDataType _dataType;
		private SortOrder _sortOrder;
		public LVISorter() : this(0, SortDataType.String){}
		public LVISorter(int columnIndex, SortDataType dataType, SortOrder sortOrder = SortOrder.Ascending){
			ColumnIndex = columnIndex;
			DataType = dataType;
			SortOrder = sortOrder;
		}
		public int ColumnIndex{
			get => _columnIndex;
			set{
				if(value < 0) throw new ArgumentOutOfRangeException(nameof(value));
				if(_columnIndex == value) return;
				_columnIndex = value;
				ClearCache();
			}
		}
		public SortOrder SortOrder{
			get => _sortOrder;
			set{
				if(value != SortOrder.None && value != SortOrder.Ascending && value != SortOrder.Descending) throw new ArgumentOutOfRangeException(nameof(value));
				_sortOrder = value;
			}
		}
		public SortDataType DataType{
			get => _dataType;
			set{
				if(!Enum.IsDefined(typeof(SortDataType), value)) throw new ArgumentOutOfRangeException(nameof(value));
				if(_dataType == value) return;
				_dataType = value;
				ClearCache();
			}
		}

		public CultureInfo Culture{
			get => _culture;
			set{
				_culture = CultureInfo.ReadOnly(value ?? throw new ArgumentNullException(nameof(value)));
				ClearCache();
			}
		}
		public int Compare(object x, object y){
			if(SortOrder == SortOrder.None || ReferenceEquals(x, y)) return 0;
			if(x != null && !(x is ListViewItem)) throw new ArgumentException("Expected a ListViewItem.", nameof(x));
			if(y != null && !(y is ListViewItem)) throw new ArgumentException("Expected a ListViewItem.", nameof(y));
			var itemX = (ListViewItem)x;
			var itemY = (ListViewItem)y;

			return SortOrder == SortOrder.Descending ? CompareValues(GetText(itemY), GetText(itemX)) : CompareValues(GetText(itemX), GetText(itemY));
		}
		private string GetText(ListViewItem item){return item != null && ColumnIndex < item.SubItems.Count ? item.SubItems[ColumnIndex].Text : string.Empty;}
		private int CompareValues(string left, string right){
			if(string.Equals(left, right, StringComparison.Ordinal)) return 0;

			var leftBlank = string.IsNullOrWhiteSpace(left);
			var rightBlank = string.IsNullOrWhiteSpace(right);
			if(leftBlank || rightBlank) return leftBlank == rightBlank ? 0 : leftBlank ? -1 : 1;
			if(DataType == SortDataType.String) return StringComparer.OrdinalIgnoreCase.Compare(left, right);
			var leftValue = GetParsedValue(left);
			var rightValue = GetParsedValue(right);
			if(leftValue == null || rightValue == null) return leftValue == rightValue ? 0 : leftValue == null ? -1 : 1;
			return leftValue.CompareTo(rightValue);
		}
		private IComparable GetParsedValue(string text){
			if(_parseCache.TryGetValue(text, out var cached)) return cached;
			var parsed = ParseValue(text);

			if(_parseCache.Count < MaxCachedValues) _parseCache.Add(text, parsed);
			return parsed;
		}
		private IComparable ParseValue(string text){
			switch(DataType){
				case SortDataType.Integer:
					if(long.TryParse(text, NumberStyle, Culture, out var integer)) return integer;
					break;
				case SortDataType.Double:
					if(double.TryParse(text, NumberStyle, Culture, out var number)) return number;
					break;
				case SortDataType.DateTime:
					if(DateTime.TryParse(text, Culture, DateTimeStyles.None, out var date)) return date;
					break;
				case SortDataType.Boolean:
					if(bool.TryParse(text, out var boolean)) return boolean;
					switch(text.Trim().ToLowerInvariant()){
						case "yes":
						case "y":
						case "1":
						case "on": return true;
						case "no":
						case "n":
						case "0":
						case "off": return false;
					}
					break;
			}
			return null;
		}
		public void ClearCache(){_parseCache.Clear();}
	}
}