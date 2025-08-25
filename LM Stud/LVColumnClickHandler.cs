using System;
using System.Collections.Generic;
using System.Windows.Forms;
namespace LMStud{
	public class LVColumnClickHandler{
		private readonly Dictionary<ListView, ListViewSortInfo> _listViewSorters;
		public LVColumnClickHandler(){_listViewSorters = new Dictionary<ListView, ListViewSortInfo>();}
		public void RegisterListView(ListView listView, SortDataType[] columnDataTypes = null){
			if(listView == null) throw new ArgumentNullException(nameof(listView));
			UnregisterListView(listView);
			var sortInfo = new ListViewSortInfo(columnDataTypes);
			_listViewSorters[listView] = sortInfo;
			listView.ListViewItemSorter = sortInfo.Sorter;
			listView.ColumnClick += OnColumnClick;
		}
		public void UnregisterListView(ListView listView){
			if(listView != null && _listViewSorters.ContainsKey(listView)){
				listView.ColumnClick -= OnColumnClick;
				_listViewSorters.Remove(listView);
			}
		}
		public void SetColumnDataType(ListView listView, int columnIndex, SortDataType dataType){
			if(_listViewSorters.TryGetValue(listView, out var sortInfo)) sortInfo.SetColumnDataType(columnIndex, dataType);
		}
		private void OnColumnClick(object sender, ColumnClickEventArgs e){
			if(!(sender is ListView listView) || !_listViewSorters.TryGetValue(listView, out var sortInfo)) return;
			var sorter = sortInfo.Sorter;
			if(sorter.ColumnIndex == e.Column){ sorter.SortOrder = sorter.SortOrder == SortOrder.Ascending ? SortOrder.Descending : SortOrder.Ascending; } else{
				sorter.ColumnIndex = e.Column;
				sorter.SortOrder = SortOrder.Ascending;
			}
			sorter.DataType = sortInfo.GetColumnDataType(e.Column);
			listView.Sort();
			UpdateColumnHeaders(listView, e.Column, sorter.SortOrder);
		}
		private void UpdateColumnHeaders(ListView listView, int sortColumnIndex, SortOrder sortOrder){
			for(var i = 0; i < listView.Columns.Count; i++){
				var column = listView.Columns[i];
				var headerText = column.Tag?.ToString() ?? column.Text;
				if(headerText.EndsWith(" ↑") || headerText.EndsWith(" ↓")) headerText = headerText.Substring(0, headerText.Length - 2);
				if(column.Tag == null) column.Tag = headerText;
				if(i == sortColumnIndex){
					listView.Sorting = sortOrder;
					column.Text = headerText + (sortOrder == SortOrder.Ascending ? " ↑" : " ↓");
				} else{ column.Text = headerText; }
			}
		}
	}
	internal class ListViewSortInfo{
		private readonly SortDataType[] _columnDataTypes;
		public ListViewSortInfo(SortDataType[] columnDataTypes = null){
			Sorter = new LVISorter();
			_columnDataTypes = columnDataTypes ?? new SortDataType[0];
		}
		public LVISorter Sorter{get;}
		public SortDataType GetColumnDataType(int columnIndex){return columnIndex < _columnDataTypes.Length ? _columnDataTypes[columnIndex] : SortDataType.String;}
		public void SetColumnDataType(int columnIndex, SortDataType dataType){
			if(columnIndex < _columnDataTypes.Length) _columnDataTypes[columnIndex] = dataType;
		}
	}
}