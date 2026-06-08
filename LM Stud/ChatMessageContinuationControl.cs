using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
namespace LMStud{
	internal sealed partial class ChatMessageContinuationControl : UserControl{
		private bool _heightUpdatePending;
		internal ChatMessageContinuationControl(Font font, Color backColor, Color foreColor){
			InitializeComponent();
			BackColor = backColor;
			ForeColor = foreColor;
			Font = font;
			MarkdownView.BackColor = backColor;
			MarkdownView.ForeColor = foreColor;
			MarkdownView.Font = font;
		}
		internal event EventHandler ContentHeightApplied;
		private void MarkdownView_ContentHeightChanged(object sender, EventArgs e){RequestHeight();}
		internal void SetContent(string text, bool markdown){
			MarkdownView.SetContent(text, markdown, false);
			RequestHeight();
		}
		private void RequestHeight(){
			if(IsDisposed || Disposing || _heightUpdatePending) return;
			if(!IsHandleCreated){
				ApplyHeight();
				return;
			}
			_heightUpdatePending = true;
			try{
				BeginInvoke(new MethodInvoker(() => {
					_heightUpdatePending = false;
					ApplyHeight();
				}));
			} catch(ObjectDisposedException){} catch(InvalidOperationException){ _heightUpdatePending = false; }
		}
		private void ApplyHeight(){
			var newHeight = MarkdownView.ContentHeight + Height - ClientSize.Height;
			if(Height == newHeight) return;
			Height = newHeight;
			ContentHeightApplied?.Invoke(this, EventArgs.Empty);
		}
	}
	internal static class MarkdownMessageChunker{
		internal const int MaximumRenderedContentHeight = 32600;
		private const int MaximumLineCharacters = 3000;
		internal static List<string> Split(string text){return Split(text, Control.DefaultFont, 500, Color.Black, Color.White, true);}
		internal static List<string> Split(string text, Font font, int width, Color foreColor, Color backColor, bool renderMarkdown){
			text = (text ?? "").Replace("\r\n", "\n").Replace('\r', '\n');
			using(var maximumImage = new Bitmap(1, 600))
			using(var renderer = new GdiMarkdownRenderer()){
				int MeasureHeight(string chunk){
					renderer.Layout(chunk, font, width, foreColor, backColor, source => maximumImage, renderMarkdown);
					return renderer.TotalHeight + 8;
				}
				return MeasureHeight(text) <= MaximumRenderedContentHeight ? new List<string>{ text } : SplitMeasured(text, MeasureHeight);
			}
		}
		internal static bool RequiresSplit(string text, int renderedContentHeight, bool renderMarkdown){
			if(renderedContentHeight > MaximumRenderedContentHeight) return true;
			if(!renderMarkdown || string.IsNullOrEmpty(text)) return false;
			var potentialImageHeight = 0;
			string openFence = null;
			foreach(var line in text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n')){
				if(TryUpdateFence(line, ref openFence)) continue;
				if(openFence == null && MarkdownImages.TryParseStandalone(line, out _)){
					potentialImageHeight += 600;
					if(renderedContentHeight + potentialImageHeight > MaximumRenderedContentHeight) return true;
				}
			}
			return false;
		}
		internal static List<string> SplitOversized(string text, Font font, int width, Color foreColor, Color backColor, bool renderMarkdown){
			text = (text ?? "").Replace("\r\n", "\n").Replace('\r', '\n');
			using(var maximumImage = new Bitmap(1, 600))
			using(var renderer = new GdiMarkdownRenderer()){
				int MeasureHeight(string chunk){
					renderer.Layout(chunk, font, width, foreColor, backColor, source => maximumImage, renderMarkdown);
					return renderer.TotalHeight + 8;
				}
				return SplitMeasured(text, MeasureHeight);
			}
		}
		private static List<string> SplitMeasured(string text, Func<string, int> measureHeight){
			var lines = new List<string>();
			foreach(var originalLine in text.Split('\n')) lines.AddRange(SplitLongLine(originalLine));
			var openFenceBefore = new string[lines.Count + 1];
			string openFence = null;
			for(var i = 0; i < lines.Count; i++){
				openFenceBefore[i] = openFence;
				UpdateFence(lines[i], ref openFence);
			}
			openFenceBefore[lines.Count] = openFence;
			var chunks = new List<string>();
			var start = 0;
			while(start < lines.Count){
				var low = start + 1;
				var high = lines.Count;
				var best = start;
				string bestChunk = null;
				while(low <= high){
					var end = low + (high - low)/2;
					var candidate = BuildChunk(lines, start, end, openFenceBefore[start], openFenceBefore[end]);
					if(measureHeight(candidate) <= MaximumRenderedContentHeight){
						best = end;
						bestChunk = candidate;
						low = end + 1;
					} else{ high = end - 1; }
				}
				if(best == start){
					best = start + 1;
					bestChunk = BuildChunk(lines, start, best, openFenceBefore[start], openFenceBefore[best]);
				}
				chunks.Add(bestChunk);
				start = best;
			}
			if(chunks.Count == 0) chunks.Add(text);
			return chunks;
		}
		private static string BuildChunk(List<string> lines, int start, int end, string openFence, string endingFence){
			var current = new StringBuilder();
			if(openFence != null) current.Append(openFence).Append('\n');
			for(var i = start; i < end; i++){
				if(i > start) current.Append('\n');
				current.Append(lines[i]);
			}
			if(endingFence != null){
				if(current.Length > 0) current.Append('\n');
				current.Append(GetClosingFence(endingFence));
			}
			return current.ToString();
		}
		private static IEnumerable<string> SplitLongLine(string line){
			if(line.Length <= MaximumLineCharacters || MarkdownImages.TryParseStandalone(line, out _)){
				yield return line;
				yield break;
			}
			var position = 0;
			while(position < line.Length){
				var count = Math.Min(MaximumLineCharacters, line.Length - position);
				if(position + count < line.Length){
					var breakAt = line.LastIndexOf(' ', position + count - 1, count);
					if(breakAt > position) count = breakAt - position;
				}
				yield return line.Substring(position, count);
				position += count;
				while(position < line.Length && line[position] == ' ') position++;
			}
		}
		private static string GetClosingFence(string openFence){
			var marker = openFence.TrimStart();
			var markerLength = 0;
			while(markerLength < marker.Length && marker[markerLength] == marker[0]) markerLength++;
			return marker.Substring(0, markerLength);
		}
		private static void UpdateFence(string line, ref string openFence){
			TryUpdateFence(line, ref openFence);
		}
		private static bool TryUpdateFence(string line, ref string openFence){
			var trimmed = line.TrimStart();
			if(trimmed.Length < 3 || (trimmed[0] != '`' && trimmed[0] != '~')) return false;
			var marker = trimmed[0];
			var length = 0;
			while(length < trimmed.Length && trimmed[length] == marker) length++;
			if(length < 3) return false;
			if(openFence == null){ openFence = trimmed; } else{
				var openMarker = openFence.TrimStart();
				var openLength = 0;
				while(openLength < openMarker.Length && openMarker[openLength] == openMarker[0]) openLength++;
				if(marker == openMarker[0] && length >= openLength) openFence = null;
			}
			return true;
		}
	}
}
