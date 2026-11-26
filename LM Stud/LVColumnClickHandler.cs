using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows.Forms;
namespace LMStud{
	internal class LVColumnClickHandler : IDisposable{
		private readonly Dictionary<ListView, ListViewSortInfo> _listViewSorters = new Dictionary<ListView, ListViewSortInfo>();
		private bool _disposed;
		public void Dispose(){
			if(_disposed) return;
			_disposed = true;

			foreach(var listView in new List<ListView>(_listViewSorters.Keys)) UnregisterListView(listView);
		}
		internal void RegisterListView(ListView listView, SortDataType[] columnDataTypes = null, int initialSortColumn = -1, SortOrder initialSortOrder = SortOrder.Ascending){RegisterListView(listView, columnDataTypes, initialSortColumn, initialSortOrder, CultureInfo.InvariantCulture);}
		internal void RegisterListView(ListView listView, SortDataType[] columnDataTypes, int initialSortColumn, SortOrder initialSortOrder, CultureInfo culture){
			ThrowIfDisposed();
			if(listView == null) throw new ArgumentNullException(nameof(listView));
			if(listView.IsDisposed) throw new ObjectDisposedException(nameof(listView));
			if(listView.VirtualMode) throw new NotSupportedException("Virtual ListViews must sort their backing data.");
			if(initialSortColumn < -1 || initialSortColumn >= listView.Columns.Count) throw new ArgumentOutOfRangeException(nameof(initialSortColumn));
			ValidateSortOrder(initialSortOrder);
			var sortInfo = new ListViewSortInfo(columnDataTypes);
			sortInfo.Sorter.Culture = culture;
			UnregisterListView(listView);

			listView.Sorting = SortOrder.None;
			if(listView.ListViewItemSorter != null) listView.ListViewItemSorter = null;
			_listViewSorters.Add(listView, sortInfo);
			listView.ColumnClick += OnColumnClick;
			listView.Disposed += OnListViewDisposed;
			if(initialSortColumn >= 0 && initialSortOrder != SortOrder.None) ApplySort(listView, sortInfo, initialSortColumn, initialSortOrder);
		}
		public void UnregisterListView(ListView listView){
			if(listView == null || !_listViewSorters.TryGetValue(listView, out var sortInfo)) return;
			_listViewSorters.Remove(listView);
			listView.ColumnClick -= OnColumnClick;
			listView.Disposed -= OnListViewDisposed;
			if(!listView.IsDisposed && !listView.Disposing){
				if(ReferenceEquals(listView.ListViewItemSorter, sortInfo.Sorter)) listView.ListViewItemSorter = null;
				sortInfo.ClearHeader();
			}
			sortInfo.Sorter.ClearCache();
		}
		public void SetColumnDataType(ListView listView, int columnIndex, SortDataType dataType){
			ThrowIfDisposed();
			if(listView == null) throw new ArgumentNullException(nameof(listView));
			if(columnIndex < 0) throw new ArgumentOutOfRangeException(nameof(columnIndex));
			if(!_listViewSorters.TryGetValue(listView, out var sortInfo)) return;
			sortInfo.SetColumnDataType(columnIndex, dataType);
			if(sortInfo.Sorter.SortOrder != SortOrder.None && sortInfo.Sorter.ColumnIndex == columnIndex) RefreshSort(listView);
		}
		public void SortBy(ListView listView, int columnIndex, SortOrder sortOrder = SortOrder.Ascending){
			var sortInfo = GetSortInfo(listView);
			ValidateSortOrder(sortOrder);
			if(sortOrder == SortOrder.None){
				ClearSort(listView);
				return;
			}
			if(columnIndex < 0 || columnIndex >= listView.Columns.Count) throw new ArgumentOutOfRangeException(nameof(columnIndex));
			ApplySort(listView, sortInfo, columnIndex, sortOrder);
		}

		public void RefreshSort(ListView listView){
			var sortInfo = GetSortInfo(listView);
			var sorter = sortInfo.Sorter;
			if(sorter.SortOrder == SortOrder.None) return;
			if(sorter.ColumnIndex >= listView.Columns.Count){
				ClearSort(listView);
				return;
			}
			sorter.ClearCache();
			ApplySort(listView, sortInfo, sorter.ColumnIndex, sorter.SortOrder);
		}

		public void ClearSort(ListView listView){
			var sortInfo = GetSortInfo(listView);
			sortInfo.Sorter.SortOrder = SortOrder.None;
			if(ReferenceEquals(listView.ListViewItemSorter, sortInfo.Sorter)) listView.ListViewItemSorter = null;
			sortInfo.ClearHeader();
			sortInfo.Sorter.ClearCache();
		}
		private void OnColumnClick(object sender, ColumnClickEventArgs e){
			if(!(sender is ListView listView) || !_listViewSorters.TryGetValue(listView, out var sortInfo) || e.Column < 0 || e.Column >= listView.Columns.Count) return;
			var sorter = sortInfo.Sorter;
			var order = sorter.ColumnIndex == e.Column && sorter.SortOrder == SortOrder.Ascending ? SortOrder.Descending : SortOrder.Ascending;
			ApplySort(listView, sortInfo, e.Column, order);
		}
		private static void ApplySort(ListView listView, ListViewSortInfo sortInfo, int columnIndex, SortOrder sortOrder){
			var sorter = sortInfo.Sorter;
			listView.BeginUpdate();
			try{
				sorter.ColumnIndex = columnIndex;
				sorter.DataType = sortInfo.GetColumnDataType(columnIndex);
				sorter.SortOrder = sortOrder;

				if(ReferenceEquals(listView.ListViewItemSorter, sorter)) listView.Sort();
				else listView.ListViewItemSorter = sorter;
				sortInfo.MarkHeader(listView.Columns[columnIndex], sortOrder);
			} finally{ listView.EndUpdate(); }
		}
		private void OnListViewDisposed(object sender, EventArgs e){
			if(sender is ListView listView) UnregisterListView(listView);
		}
		private ListViewSortInfo GetSortInfo(ListView listView){
			ThrowIfDisposed();
			if(listView == null) throw new ArgumentNullException(nameof(listView));
			if(!_listViewSorters.TryGetValue(listView, out var sortInfo)) throw new InvalidOperationException("The ListView is not registered.");
			return sortInfo;
		}
		private static void ValidateSortOrder(SortOrder sortOrder){
			if(sortOrder != SortOrder.None && sortOrder != SortOrder.Ascending && sortOrder != SortOrder.Descending) throw new ArgumentOutOfRangeException(nameof(sortOrder));
		}
		private void ThrowIfDisposed(){
			if(_disposed) throw new ObjectDisposedException(nameof(LVColumnClickHandler));
		}
	}
	internal class ListViewSortInfo{
		private readonly Dictionary<int, SortDataType> _columnDataTypes = new Dictionary<int, SortDataType>();
		private ColumnHeader _markedHeader;
		private string _markedHeaderText;
		private string _originalHeaderText;
		public ListViewSortInfo(SortDataType[] columnDataTypes = null){
			Sorter = new LVISorter(0, SortDataType.String, SortOrder.None);
			if(columnDataTypes == null) return;
			for(var i = 0; i < columnDataTypes.Length; i++) SetColumnDataType(i, columnDataTypes[i]);
		}
		public LVISorter Sorter{get;}
		public SortDataType GetColumnDataType(int columnIndex){return _columnDataTypes.TryGetValue(columnIndex, out var dataType) ? dataType : SortDataType.String;}
		public void SetColumnDataType(int columnIndex, SortDataType dataType){
			if(columnIndex < 0) throw new ArgumentOutOfRangeException(nameof(columnIndex));
			if(!Enum.IsDefined(typeof(SortDataType), dataType)) throw new ArgumentOutOfRangeException(nameof(dataType));
			_columnDataTypes[columnIndex] = dataType;
		}
		internal void MarkHeader(ColumnHeader header, SortOrder order){
			ClearHeader();
			_markedHeader = header;
			_originalHeaderText = header.Text;
			_markedHeaderText = _originalHeaderText + (order == SortOrder.Ascending ? " ↑" : " ↓");
			header.Text = _markedHeaderText;
		}
		internal void ClearHeader(){
			if(_markedHeader != null && _markedHeader.Text == _markedHeaderText) _markedHeader.Text = _originalHeaderText;
			_markedHeader = null;
			_originalHeaderText = null;
			_markedHeaderText = null;
		}
	}
}