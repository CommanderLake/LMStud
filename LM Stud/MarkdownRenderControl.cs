using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;
namespace LMStud{
	[DesignerCategory("Code")]
	[ToolboxItem(true)]
	public sealed class MarkdownRenderControl : Control{
		private const int ContentPadding = 4;
		private readonly ContextMenuStrip _contextMenu;
		private readonly MarkdownImageLoader _imageLoader;
		private readonly GdiMarkdownRenderer _renderer = new GdiMarkdownRenderer();
		private int _layoutWidth = -1;
		private string _markdownText = string.Empty;
		private Point _mouseDownLocation;
		private float _preferredCaretX = float.NaN;
		private bool _renderMarkdown = true;
		private bool _selecting;
		private int _selectionAnchor;
		private bool _selectionMoved;
		public MarkdownRenderControl(){
			_imageLoader = new MarkdownImageLoader(ImageLoaded);
			DoubleBuffered = true;
			SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.Selectable | ControlStyles.Opaque, true);
			BackColor = SystemColors.Window;
			ForeColor = SystemColors.WindowText;
			TabStop = true;
			var copyItem = new ToolStripMenuItem("Copy", null, (sender, args) => CopySelection());
			var selectAllItem = new ToolStripMenuItem("Select All", null, (sender, args) => SelectAllText());
			_contextMenu = new ContextMenuStrip();
			_contextMenu.Items.Add(copyItem);
			_contextMenu.Items.Add(selectAllItem);
			_contextMenu.Opening += (sender, args) => copyItem.Enabled = SelectionLength > 0;
			ContextMenuStrip = _contextMenu;
		}
		[Browsable(false)]
		[DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
		public string MarkdownText{
			get => _markdownText;
			set{ SetContent(value, _renderMarkdown); }
		}
		internal string PlainText => _renderer.PlainText;
		[Category("Behavior")]
		[DefaultValue(true)]
		public bool RenderMarkdown{
			get => _renderMarkdown;
			set{ SetContent(_markdownText, value); }
		}
		[Browsable(false)]
		[DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
		public int ContentHeight{get; private set;}
		internal int SelectionStart => Math.Min(_selectionAnchor, CaretPosition);
		internal int SelectionLength => Math.Abs(CaretPosition - _selectionAnchor);
		internal int CaretPosition{get; private set;}
		internal int LayoutCount{get; private set;}
		internal void SetContent(string markdownText, bool renderMarkdown, bool notifyHeightChanged = true){
			markdownText = markdownText ?? string.Empty;
			if(_markdownText == markdownText && _renderMarkdown == renderMarkdown) return;
			_markdownText = markdownText;
			_renderMarkdown = renderMarkdown;
			Relayout(notifyHeightChanged);
		}
		private void ImageLoaded(){
			if(IsDisposed || Disposing) return;
			try{
				if(IsHandleCreated) BeginInvoke(new MethodInvoker(Relayout));
			} catch(ObjectDisposedException){} catch(InvalidOperationException){}
		}
		public event EventHandler ContentHeightChanged;
		protected override void OnSizeChanged(EventArgs e){
			base.OnSizeChanged(e);
			var width = Math.Max(10, ClientSize.Width - ContentPadding*2);
			if(width != _layoutWidth) Relayout();
		}
		protected override void OnFontChanged(EventArgs e){
			base.OnFontChanged(e);
			Relayout();
		}
		protected override void OnForeColorChanged(EventArgs e){
			base.OnForeColorChanged(e);
			Relayout();
		}
		protected override void OnBackColorChanged(EventArgs e){
			base.OnBackColorChanged(e);
			Relayout();
		}
		private void Relayout(){Relayout(true);}
		private void Relayout(bool notifyHeightChanged){
			if(IsDisposed) return;
			LayoutCount++;
			var width = Math.Max(10, ClientSize.Width - ContentPadding*2);
			_layoutWidth = width;
			_renderer.Layout(_markdownText, Font, width, ForeColor, BackColor, _imageLoader.Get, _renderMarkdown);
			var textLength = _renderer.PlainText.Length;
			_selectionAnchor = UnicodeText.NormalizeCaretPosition(_renderer.PlainText, Math.Min(_selectionAnchor, textLength));
			CaretPosition = UnicodeText.NormalizeCaretPosition(_renderer.PlainText, Math.Min(CaretPosition, textLength));
			var contentHeight = Math.Max(18, _renderer.TotalHeight + 8);
			if(ContentHeight != contentHeight){
				ContentHeight = contentHeight;
				if(notifyHeightChanged) ContentHeightChanged?.Invoke(this, EventArgs.Empty);
			}
			Invalidate();
		}
		protected override void OnPaint(PaintEventArgs e){
			e.Graphics.Clear(BackColor);
			_renderer.Draw(e.Graphics, SelectionStart, SelectionLength, SystemColors.Highlight, SystemColors.HighlightText, ContentPadding, ContentPadding);
			if(Focused && SelectionLength == 0){
				var caret = _renderer.GetCaretRectangle(CaretPosition);
				using(var pen = new Pen(ForeColor)){ e.Graphics.DrawLine(pen, caret.Left + ContentPadding, caret.Top + ContentPadding, caret.Left + ContentPadding, caret.Bottom + ContentPadding); }
			}
		}
		protected override void OnMouseDown(MouseEventArgs e){
			base.OnMouseDown(e);
			if(e.Button != MouseButtons.Left) return;
			Focus();
			_preferredCaretX = float.NaN;
			_mouseDownLocation = e.Location;
			_selectionMoved = false;
			_selecting = true;
			Capture = true;
			var index = HitTestText(e.Location);
			if((ModifierKeys & Keys.Shift) == 0) _selectionAnchor = index;
			CaretPosition = index;
			Invalidate();
		}
		protected override void OnMouseMove(MouseEventArgs e){
			base.OnMouseMove(e);
			if(_selecting){
				if(Math.Abs(e.X - _mouseDownLocation.X) >= SystemInformation.DragSize.Width/2 || Math.Abs(e.Y - _mouseDownLocation.Y) >= SystemInformation.DragSize.Height/2) _selectionMoved = true;
				var index = HitTestText(e.Location);
				if(CaretPosition != index){
					CaretPosition = index;
					Invalidate();
				}
				Cursor = Cursors.IBeam;
				return;
			}
			var point = ToRendererPoint(e.Location);
			Cursor = _renderer.HitTestLink(point) == null ? Cursors.IBeam : Cursors.Hand;
		}
		protected override void OnMouseUp(MouseEventArgs e){
			base.OnMouseUp(e);
			if(e.Button != MouseButtons.Left || !_selecting) return;
			_selecting = false;
			Capture = false;
			CaretPosition = HitTestText(e.Location);
			Invalidate();
			if(_selectionMoved || SelectionLength != 0) return;
			OpenLink(_renderer.HitTestLink(ToRendererPoint(e.Location)));
		}
		protected override void OnMouseDoubleClick(MouseEventArgs e){
			base.OnMouseDoubleClick(e);
			if(e.Button != MouseButtons.Left || _renderer.PlainText.Length == 0) return;
			_preferredCaretX = float.NaN;
			var index = Math.Min(HitTestText(e.Location), _renderer.PlainText.Length - 1);
			var start = index;
			var end = index;
			while(start > 0 && IsWordCharacter(_renderer.PlainText[start - 1])) start--;
			while(end < _renderer.PlainText.Length && IsWordCharacter(_renderer.PlainText[end])) end++;
			_selectionAnchor = start;
			CaretPosition = end;
			_selectionMoved = true;
			Invalidate();
		}
		private static bool IsWordCharacter(char value){return char.IsLetterOrDigit(value) || value == '_' || value == '-';}
		protected override void OnMouseLeave(EventArgs e){
			base.OnMouseLeave(e);
			if(!_selecting) Cursor = Cursors.IBeam;
		}
		protected override void OnKeyDown(KeyEventArgs e){
			if(e.Control && e.KeyCode == Keys.A){
				SelectAllText();
				e.SuppressKeyPress = true;
				return;
			}
			if(e.Control && e.KeyCode == Keys.C){
				CopySelection();
				e.SuppressKeyPress = true;
				return;
			}
			if(e.KeyCode == Keys.Escape){
				_selectionAnchor = CaretPosition;
				_preferredCaretX = float.NaN;
				Invalidate();
				e.SuppressKeyPress = true;
				return;
			}
			if(e.KeyCode == Keys.Left || e.KeyCode == Keys.Right || e.KeyCode == Keys.Up || e.KeyCode == Keys.Down || e.KeyCode == Keys.Home || e.KeyCode == Keys.End || e.KeyCode == Keys.PageUp || e.KeyCode == Keys.PageDown){
				MoveSelection(e.KeyCode, e.Shift, e.Control);
				e.SuppressKeyPress = true;
				e.Handled = true;
				return;
			}
			base.OnKeyDown(e);
		}
		protected override bool IsInputKey(Keys keyData){
			switch(keyData & Keys.KeyCode){
				case Keys.Left:
				case Keys.Right:
				case Keys.Up:
				case Keys.Down:
				case Keys.Home:
				case Keys.End:
				case Keys.PageUp:
				case Keys.PageDown: return true;
				default: return base.IsInputKey(keyData);
			}
		}
		private void MoveSelection(Keys key, bool extend, bool control){
			var position = CaretPosition;
			if(!extend && SelectionLength > 0 && (key == Keys.Left || key == Keys.Right)) position = key == Keys.Left ? SelectionStart : SelectionStart + SelectionLength;
			else
				switch(key){
					case Keys.Left:
						position = control ? PreviousWord(position) : UnicodeText.PreviousCaretPosition(_renderer.PlainText, position);
						break;
					case Keys.Right:
						position = control ? NextWord(position) : UnicodeText.NextCaretPosition(_renderer.PlainText, position);
						break;
					case Keys.Home:
						position = control ? 0 : _renderer.GetLineBoundary(position, false);
						break;
					case Keys.End:
						position = control ? _renderer.PlainText.Length : _renderer.GetLineBoundary(position, true);
						break;
					case Keys.Up:
					case Keys.Down:
						if(control) position = MoveParagraph(position, key == Keys.Up ? -1 : 1);
						else position = MoveVertical(position, key == Keys.Up ? -1 : 1, false);
						break;
					case Keys.PageUp:
					case Keys.PageDown:
						position = MoveVertical(position, key == Keys.PageUp ? -1 : 1, true);
						break;
				}
			position = Math.Max(0, Math.Min(_renderer.PlainText.Length, position));
			if(!extend) _selectionAnchor = position;
			CaretPosition = position;
			if(key != Keys.Up && key != Keys.Down && key != Keys.PageUp && key != Keys.PageDown) _preferredCaretX = float.NaN;
			Invalidate();
			EnsureCaretVisible();
		}
		private int MoveVertical(int position, int direction, bool page){
			if(float.IsNaN(_preferredCaretX)) _preferredCaretX = _renderer.GetCaretRectangle(position).Left;
			return page ? _renderer.MoveCaretByPixels(position, direction*GetPageHeight(), _preferredCaretX) : _renderer.MoveCaretVertically(position, direction, _preferredCaretX);
		}
		private int GetPageHeight(){
			var parent = Parent;
			while(parent != null && !(parent is MyFlowLayoutPanel)) parent = parent.Parent;
			return Math.Max(Font.Height, (parent?.ClientSize.Height ?? ClientSize.Height) - Font.Height);
		}
		private int PreviousWord(int position){
			var text = _renderer.PlainText;
			while(position > 0 && char.IsWhiteSpace(text[position - 1])) position--;
			if(position == 0) return 0;
			var word = IsWordCharacter(text[position - 1]);
			while(position > 0 && !char.IsWhiteSpace(text[position - 1]) && IsWordCharacter(text[position - 1]) == word) position--;
			return position;
		}
		private int NextWord(int position){
			var text = _renderer.PlainText;
			if(position >= text.Length) return text.Length;
			var word = IsWordCharacter(text[position]);
			while(position < text.Length && !char.IsWhiteSpace(text[position]) && IsWordCharacter(text[position]) == word) position++;
			while(position < text.Length && char.IsWhiteSpace(text[position])) position++;
			return position;
		}
		private int MoveParagraph(int position, int direction){
			var text = _renderer.PlainText;
			if(direction < 0){
				position = Math.Max(0, position - 1);
				var boundary = text.LastIndexOf('\n', Math.Max(0, position - 1));
				return boundary < 0 ? 0 : boundary + 1;
			}
			var next = text.IndexOf('\n', position);
			return next < 0 ? text.Length : Math.Min(text.Length, next + 1);
		}
		private void EnsureCaretVisible(){
			var parent = Parent;
			while(parent != null && !(parent is MyFlowLayoutPanel)) parent = parent.Parent;
			var scrollPanel = parent as MyFlowLayoutPanel;
			if(scrollPanel == null || !IsHandleCreated || !scrollPanel.IsHandleCreated) return;
			var caret = _renderer.GetCaretRectangle(CaretPosition);
			var top = scrollPanel.PointToClient(PointToScreen(new Point((int)caret.Left + ContentPadding, (int)caret.Top + ContentPadding))).Y;
			var bottom = top + (int)Math.Ceiling(caret.Height);
			var scrollOffset = -scrollPanel.AutoScrollPosition.Y;
			if(top < 4) scrollPanel.AutoScrollPosition = new Point(0, Math.Max(0, scrollOffset + top - 4));
			else if(bottom > scrollPanel.ClientSize.Height - 4) scrollPanel.AutoScrollPosition = new Point(0, scrollOffset + bottom - scrollPanel.ClientSize.Height + 4);
		}
		private void SelectAllText(){
			_selectionAnchor = 0;
			CaretPosition = _renderer.PlainText.Length;
			_preferredCaretX = float.NaN;
			Invalidate();
		}
		private void CopySelection(){
			if(SelectionLength <= 0) return;
			try{ Clipboard.SetText(_renderer.PlainText.Substring(SelectionStart, SelectionLength)); } catch(ExternalException){}
		}
		private int HitTestText(Point point){return _renderer.HitTestText(ToRendererPoint(point));}
		private static Point ToRendererPoint(Point point){return new Point(point.X - ContentPadding, point.Y - ContentPadding);}
		private static void OpenLink(string link){
			if(string.IsNullOrEmpty(link)) return;
			try{
				if(!Uri.TryCreate(link, UriKind.Absolute, out var uri)) return;
				if(uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeMailto) return;
				Process.Start(new ProcessStartInfo(uri.ToString()){ UseShellExecute = true });
			} catch{}
		}
		protected override void OnMouseWheel(MouseEventArgs e){
			var parent = Parent;
			while(parent != null && !(parent is MyFlowLayoutPanel)) parent = parent.Parent;
			if(parent != null){
				NativeMethods.SendMessage(parent.Handle, 0x020A, (IntPtr)(e.Delta << 16), IntPtr.Zero);
				return;
			}
			base.OnMouseWheel(e);
		}
		protected override void OnGotFocus(EventArgs e){
			base.OnGotFocus(e);
			Invalidate();
		}
		protected override void OnLostFocus(EventArgs e){
			base.OnLostFocus(e);
			Invalidate();
		}
		protected override void Dispose(bool disposing){
			if(disposing){
				_contextMenu.Dispose();
				_imageLoader.Dispose();
				_renderer.Dispose();
			}
			base.Dispose(disposing);
		}
	}
	internal sealed class MarkdownImageLoader : IDisposable{
		private const int MaximumDownloadBytes = 20*1024*1024;
		private static readonly HttpClient HttpClient = new HttpClient{ Timeout = TimeSpan.FromSeconds(20) };
		private readonly Action _imageLoaded;
		private readonly Dictionary<string, ImageEntry> _images = new Dictionary<string, ImageEntry>(StringComparer.OrdinalIgnoreCase);
		private bool _disposed;
		internal MarkdownImageLoader(Action imageLoaded){_imageLoaded = imageLoaded;}
		public void Dispose(){
			lock(_images){
				_disposed = true;
				foreach(var entry in _images.Values) entry.Image?.Dispose();
				_images.Clear();
			}
		}
		internal Image Get(string source){
			if(string.IsNullOrWhiteSpace(source) || _disposed) return null;
			lock(_images){
				if(_images.TryGetValue(source, out var existing)) return existing.Image;
				_images[source] = new ImageEntry();
			}
			ThreadPool.QueueUserWorkItem(_ => Load(source));
			return null;
		}
		private void Load(string source){
			Image image = null;
			try{
				byte[] bytes;
				if(!MarkdownImages.TryReadLocalOrDataBytes(source, out bytes)){
					if(!Uri.TryCreate(source, UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)) return;
					bytes = HttpClient.GetByteArrayAsync(uri).GetAwaiter().GetResult();
					if(bytes.Length > MaximumDownloadBytes) return;
				}
				using(var stream = new MemoryStream(bytes))
				using(var sourceImage = Image.FromStream(stream)){ image = new Bitmap(sourceImage); }
			} catch{}
			if(image == null) return;
			lock(_images){
				if(_disposed){
					image.Dispose();
					return;
				}
				if(_images.TryGetValue(source, out var entry)) entry.Image = image;
			}
			_imageLoaded?.Invoke();
		}
		private sealed class ImageEntry{
			internal Image Image;
		}
	}
	internal sealed class GdiMarkdownRenderer : IDisposable{
		private static readonly Regex OrderedListRegex = new Regex("^(\\s*)(\\d+[.)])\\s+(.+)$", RegexOptions.Compiled);
		private static readonly Regex UnorderedListRegex = new Regex("^(\\s*)([-+*])\\s+(.+)$", RegexOptions.Compiled);
		private static readonly Regex TaskListRegex = new Regex("^\\[([ xX])\\]\\s+(.+)$", RegexOptions.Compiled);
		private static readonly Regex LinkRegex = new Regex("^\\[([^\\]]+)\\]\\(([^\\)]+)\\)", RegexOptions.Compiled);
		private static readonly Regex ImageRegex = new Regex("^!\\[([^\\]]*)\\]\\(([^\\)]+)\\)", RegexOptions.Compiled);
		private static readonly Regex UrlRegex = new Regex("^(https?://\\S+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
		private static readonly Regex TableDelimiterRegex = new Regex("^\\|?\\s*:?-{3,}:?\\s*(\\|\\s*:?-{3,}:?\\s*)*\\|?$", RegexOptions.Compiled);
		private readonly Dictionary<string, Font> _fontCache = new Dictionary<string, Font>();
		private readonly Dictionary<Font, IntPtr> _fontHandles = new Dictionary<Font, IntPtr>();
		private readonly List<LinkRegion> _linkRegions = new List<LinkRegion>();
		private readonly IntPtr _measureHdc;
		private readonly Dictionary<Font, Dictionary<string, float[]>> _measurementCache = new Dictionary<Font, Dictionary<string, float[]>>();
		private int _measurementCacheEntryCount;
		private readonly Dictionary<Font, int> _measurementPrefixWidths = new Dictionary<Font, int>();
		private readonly List<DrawOp> _ops = new List<DrawOp>();
		private readonly StringBuilder _plainText = new StringBuilder();
		private const string MeasurementPrefix = "\u200B";
		private const int ExactTextRendererMeasurementLimit = 256;
		private const int MaximumMeasurementCacheEntries = 4096;
		private const TextFormatFlags TextRendererFlags = TextFormatFlags.NoPadding | TextFormatFlags.NoClipping | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine;
		private Color _backColor;
		private Font _baseFont;
		private Color _codeBackColor;
		private Color _codeBorderColor;
		private Func<string, Image> _imageResolver;
		private int _layoutWidth;
		private Color _linkColor;
		private Color _mutedColor;
		private IntPtr _originalMeasureFont;
		private Color _quoteColor;
		private IntPtr _selectedMeasureFont;
		private Color _tableCellColor;
		private Color _tableHeaderColor;
		private int _textRendererMeasurementCount;
		private Color _textColor;
		internal GdiMarkdownRenderer(){
			_measureHdc = GdiText.CreateCompatibleDC(IntPtr.Zero);
			if(_measureHdc == IntPtr.Zero) throw new InvalidOperationException("Unable to create a text measurement context.");
		}
		internal int TotalHeight{get; private set;}
		internal string PlainText{get; private set;} = string.Empty;
		internal int TextRendererMeasurementCount => _textRendererMeasurementCount;
		public void Dispose(){
			if(_originalMeasureFont != IntPtr.Zero) GdiText.SelectObject(_measureHdc, _originalMeasureFont);
			foreach(var handle in _fontHandles.Values) GdiText.DeleteObject(handle);
			_fontHandles.Clear();
			foreach(var font in _fontCache.Values) font.Dispose();
			_fontCache.Clear();
			GdiText.DeleteDC(_measureHdc);
		}
		internal void Layout(string markdown, Font baseFont, int width, Color textColor, Color backColor, Func<string, Image> imageResolver = null, bool renderMarkdown = true){
			_ops.Clear();
			_linkRegions.Clear();
			_plainText.Clear();
			if(_measurementCacheEntryCount > MaximumMeasurementCacheEntries){
				_measurementCache.Clear();
				_measurementCacheEntryCount = 0;
			}
			_baseFont = baseFont ?? Control.DefaultFont;
			_textColor = textColor;
			_backColor = backColor;
			_imageResolver = imageResolver;
			_layoutWidth = Math.Max(10, width);
			_codeBackColor = Blend(textColor, backColor, 0.08f);
			_codeBorderColor = Blend(textColor, backColor, 0.20f);
			_mutedColor = Blend(textColor, backColor, 0.55f);
			_quoteColor = Blend(textColor, backColor, 0.28f);
			_tableHeaderColor = Blend(textColor, backColor, 0.09f);
			_tableCellColor = Blend(textColor, backColor, 0.035f);
			_linkColor = backColor.GetBrightness() < 0.45f ? Color.FromArgb(86, 156, 214) : SystemColors.HotTrack;
			var y = 0f;
			var lines = Normalize(markdown).Split('\n');
			if(!renderMarkdown){
				foreach(var line in lines)
					if(line.Length == 0){
						AppendLineBreak();
						y += _baseFont.GetHeight()*0.6f;
					} else{ y = AddPlainParagraph(line, y, _layoutWidth); }
				PlainText = _plainText.ToString().TrimEnd('\n');
				TotalHeight = (int)Math.Ceiling(Math.Max(y, _baseFont.GetHeight() + 2));
				return;
			}
			for(var i = 0; i < lines.Length; i++){
				var line = lines[i];
				if(TryGetFence(line, out var fence)){
					var code = new List<string>();
					i++;
					while(i < lines.Length && !IsClosingFence(lines[i], fence)){
						code.Add(lines[i]);
						i++;
					}
					y = AddCodeBlock(code, y, _layoutWidth);
					continue;
				}
				if(string.IsNullOrWhiteSpace(line)){
					AppendLineBreak();
					y += _baseFont.GetHeight()*0.6f;
					continue;
				}
				if(MarkdownImages.TryParseStandalone(line, out var image)){
					y = AddImageBlock(image.AltText, image.Source, y, _layoutWidth);
					continue;
				}
				if(i + 1 < lines.Length && TryParseSetextHeading(lines[i + 1], out var setextLevel)){
					y = AddInlineParagraph(line.Trim(), y, _layoutWidth, setextLevel, false, 0, null);
					i++;
					continue;
				}
				if(IsHorizontalRule(line)){
					_ops.Add(DrawOp.Line(0, y + 4, _layoutWidth, _mutedColor));
					_plainText.Append("---");
					AppendLineBreak();
					y += 10;
					continue;
				}
				if(TryParseHeading(line, out var headingLevel, out var headingText)){
					y = AddInlineParagraph(headingText, y, _layoutWidth, headingLevel, false, 0, null);
					y += 2;
					continue;
				}
				if(IsTableHeader(lines, i)){
					y = AddTable(lines, ref i, y, _layoutWidth);
					continue;
				}
				if(line.TrimStart().StartsWith(">")){
					var quote = line.TrimStart().Substring(1).TrimStart();
					var startY = y;
					y = AddInlineParagraph(quote, y, _layoutWidth - 12, 0, true, 12, null);
					_ops.Add(DrawOp.FilledRect(0, startY, 4, Math.Max(4, y - startY - 2), _quoteColor));
					continue;
				}
				var match = OrderedListRegex.Match(line);
				if(match.Success){
					var indent = 24 + Math.Min(64, CountIndent(match.Groups[1].Value)*12);
					y = AddInlineParagraph(match.Groups[3].Value, y, _layoutWidth - indent, 0, false, indent, match.Groups[2].Value);
					continue;
				}
				match = UnorderedListRegex.Match(line);
				if(match.Success){
					var content = match.Groups[3].Value;
					var bullet = "\u2022";
					var taskMatch = TaskListRegex.Match(content);
					if(taskMatch.Success){
						bullet = taskMatch.Groups[1].Value == " " ? "[ ]" : "[x]";
						content = taskMatch.Groups[2].Value;
					}
					var indent = 24 + Math.Min(64, CountIndent(match.Groups[1].Value)*12);
					y = AddInlineParagraph(content, y, _layoutWidth - indent, 0, false, indent, bullet);
					continue;
				}
				y = AddInlineParagraph(line, y, _layoutWidth, 0, false, 0, null);
			}
			PlainText = _plainText.ToString().TrimEnd('\n');
			TotalHeight = (int)Math.Ceiling(Math.Max(y, _baseFont.GetHeight() + 2));
		}
		private float AddPlainParagraph(string text, float y, int width){
			var font = GetFont(FontStyle.Regular);
			var lineHeight = Math.Max(font.GetHeight() + 2, 16f);
			var x = 0f;
			var currentY = y;
			foreach(var token in EnumerateDrawTokens(text)) AddWrappedToken(token, font, _textColor, false, false, null, ref x, ref currentY, lineHeight, 0, width);
			AppendLineBreak();
			return currentY + lineHeight + 4;
		}
		private float AddImageBlock(string altText, string source, float y, int width){
			var image = _imageResolver?.Invoke(source);
			var caption = string.IsNullOrWhiteSpace(altText) ? "Image" : altText;
			if(image == null){
				var placeholderHeight = Math.Max(48f, _baseFont.GetHeight() + 24);
				_ops.Add(DrawOp.FilledRect(0, y, width, placeholderHeight, _codeBackColor));
				_ops.Add(DrawOp.StrokeRect(new RectangleF(0, y, width, placeholderHeight), _codeBorderColor));
				AppendVisibleText("[Image: " + caption + "]", 10, y + 10, GetFont(FontStyle.Italic), _mutedColor, false, false, null, width - 20);
				AppendLineBreak();
				return y + placeholderHeight + 6;
			}
			var scale = Math.Min(1f, Math.Min(width/(float)image.Width, 600f/image.Height));
			var imageWidth = Math.Max(1, (int)Math.Round(image.Width*scale));
			var imageHeight = Math.Max(1, (int)Math.Round(image.Height*scale));
			var x = Math.Max(0, (width - imageWidth)/2f);
			var imageRect = new RectangleF(x, y, imageWidth, imageHeight);
			_ops.Add(DrawOp.Image(imageRect, image));
			var captionY = y + imageHeight + 3;
			AppendVisibleText(caption, 0, captionY, GetFont(FontStyle.Italic, _baseFont.Size*0.9f), _mutedColor, false, false, null, width);
			AppendLineBreak();
			return captionY + Math.Max(_baseFont.GetHeight() + 2, 16f) + 6;
		}
		private static Color Blend(Color foreground, Color background, float foregroundAmount){
			var backgroundAmount = 1f - foregroundAmount;
			return Color.FromArgb((int)(foreground.R*foregroundAmount + background.R*backgroundAmount), (int)(foreground.G*foregroundAmount + background.G*backgroundAmount), (int)(foreground.B*foregroundAmount + background.B*backgroundAmount));
		}
		private static string Normalize(string text){return (text ?? string.Empty).Replace("\r\n", "\n").Replace('\r', '\n');}
		private static bool TryGetFence(string line, out string fence){
			var trimmed = line.TrimStart();
			fence = null;
			if(!trimmed.StartsWith("```") && !trimmed.StartsWith("~~~")) return false;
			var marker = trimmed[0];
			var length = 0;
			while(length < trimmed.Length && trimmed[length] == marker) length++;
			fence = new string(marker, length);
			return true;
		}
		private static bool IsClosingFence(string line, string fence){
			var trimmed = line.TrimStart();
			if(!trimmed.StartsWith(fence)) return false;
			return string.IsNullOrWhiteSpace(trimmed.Substring(fence.Length));
		}
		private static bool TryParseHeading(string line, out int level, out string text){
			var trimmed = line.TrimStart();
			level = 0;
			text = null;
			while(level < trimmed.Length && level < 6 && trimmed[level] == '#') level++;
			if(level == 0 || level >= trimmed.Length || trimmed[level] != ' ') return false;
			text = trimmed.Substring(level).Trim().TrimEnd('#').TrimEnd();
			return true;
		}
		private static bool TryParseSetextHeading(string line, out int level){
			var trimmed = line.Trim();
			level = 0;
			if(trimmed.Length < 3) return false;
			if(trimmed.Trim('=').Length == 0){
				level = 1;
				return true;
			}
			if(trimmed.Trim('-').Length == 0){
				level = 2;
				return true;
			}
			return false;
		}
		private static bool IsHorizontalRule(string line){
			var compact = line.Replace(" ", "").Replace("\t", "");
			if(compact.Length < 3) return false;
			return compact.Trim('-').Length == 0 || compact.Trim('*').Length == 0 || compact.Trim('_').Length == 0;
		}
		private bool IsTableHeader(string[] lines, int index){return index + 1 < lines.Length && lines[index].Contains("|") && TableDelimiterRegex.IsMatch(lines[index + 1].Trim());}
		private float AddTable(string[] lines, ref int index, float y, int width){
			var rows = new List<string[]>{ SplitCells(lines[index]) };
			index += 2;
			while(index < lines.Length && lines[index].Contains("|") && !string.IsNullOrWhiteSpace(lines[index])){
				rows.Add(SplitCells(lines[index]));
				index++;
			}
			index--;
			var columns = 1;
			foreach(var row in rows) columns = Math.Max(columns, row.Length);
			var cellWidth = Math.Max(24f, (width - 2)/(float)columns);
			var normal = GetFont(FontStyle.Regular);
			var bold = GetFont(FontStyle.Bold);
			var lineHeight = Math.Max(normal.GetHeight() + 2, 16f);
			for(var rowIndex = 0; rowIndex < rows.Count; rowIndex++){
				var cellRuns = new List<InlineRun>[columns];
				var rowLines = 1;
				var paragraphFont = rowIndex == 0 ? bold : normal;
				for(var column = 0; column < columns; column++){
					var value = column < rows[rowIndex].Length ? rows[rowIndex][column].Trim() : string.Empty;
					cellRuns[column] = ParseInline(value);
					rowLines = Math.Max(rowLines, MeasureInlineLineCount(cellRuns[column], paragraphFont, Math.Max(8, cellWidth - 8)));
				}
				var rowHeight = rowLines*lineHeight + 8;
				for(var column = 0; column < columns; column++){
					var rect = new RectangleF(column*cellWidth, y, cellWidth, rowHeight);
					_ops.Add(DrawOp.FilledRect(rect.X, rect.Y, rect.Width, rect.Height, rowIndex == 0 ? _tableHeaderColor : _tableCellColor));
					_ops.Add(DrawOp.StrokeRect(rect, _codeBorderColor));
				}
				for(var column = 0; column < columns; column++){
					var rect = new RectangleF(column*cellWidth, y, cellWidth, rowHeight);
					if(column > 0) _plainText.Append('\t');
					AddInlineCellContent(cellRuns[column], paragraphFont, rect.X + 4, rect.Y + 4, Math.Max(8, cellWidth - 8), lineHeight);
				}
				AppendLineBreak();
				y += rowHeight;
			}
			return y + 6;
		}
		private int MeasureInlineLineCount(List<InlineRun> runs, Font paragraphFont, float width){
			var x = 0f;
			var line = 0;
			foreach(var run in runs){
				if(run.Kind == InlineKind.Break){
					x = 0;
					line++;
					continue;
				}
				var font = ResolveRunFont(paragraphFont, run);
				foreach(var token in EnumerateDrawTokens(run.Text)){
					var tokenStart = 0;
					while(tokenStart < token.Length){
						var available = Math.Max(1f, width - x);
						var remaining = token.Length - tokenStart;
						var fit = FittedCharacterCount(token, tokenStart, remaining, font, available);
						if(fit == 0 && x > 0){
							x = 0;
							line++;
							available = Math.Max(1f, width);
							fit = FittedCharacterCount(token, tokenStart, remaining, font, available);
						}
						if(fit == 0) fit = Math.Max(1, UnicodeText.NextCaretPosition(token, tokenStart) - tokenStart);
						var piece = token.Substring(tokenStart, fit);
						x += MeasureTextWidth(piece, font);
						tokenStart += fit;
						if(tokenStart < token.Length){
							x = 0;
							line++;
						}
					}
				}
			}
			return line + 1;
		}
		private void AddInlineCellContent(List<InlineRun> runs, Font paragraphFont, float left, float top, float width, float lineHeight){
			var x = left;
			var currentY = top;
			foreach(var run in runs){
				if(run.Kind == InlineKind.Break){
					x = left;
					currentY += lineHeight;
					continue;
				}
				var font = ResolveRunFont(paragraphFont, run);
				var color = run.IsLink ? _linkColor : _textColor;
				foreach(var token in EnumerateDrawTokens(run.Text))
					AddWrappedToken(token, font, color, run.Strike, run.Kind == InlineKind.Code,
						run.IsLink ? run.LinkUrl : null, ref x, ref currentY, lineHeight, (int)left, (int)(left + width));
			}
		}
		private List<string> WrapText(string text, Font font, float width){
			var result = new List<string>();
			if(string.IsNullOrEmpty(text)){
				result.Add(string.Empty);
				return result;
			}
			var start = 0;
			while(start < text.Length){
				var count = FittedCharacterCount(text, start, text.Length - start, font, width);
				if(count <= 0) count = Math.Max(1, UnicodeText.NextCaretPosition(text, start) - start);
				var breakAt = count;
				if(start + count < text.Length){
					var whitespace = text.LastIndexOf(' ', start + count - 1, count);
					if(whitespace >= start) breakAt = Math.Max(1, whitespace - start);
				}
				result.Add(text.Substring(start, breakAt));
				start += breakAt;
				while(start < text.Length && text[start] == ' ') start++;
			}
			return result;
		}
		private static string[] SplitCells(string line){
			var trimmed = line.Trim();
			if(trimmed.StartsWith("|")) trimmed = trimmed.Substring(1);
			if(trimmed.EndsWith("|")) trimmed = trimmed.Substring(0, trimmed.Length - 1);
			return Regex.Split(trimmed, "(?<!\\\\)\\|");
		}
		private float AddCodeBlock(List<string> codeLines, float y, int width){
			var mono = GetFont(FontStyle.Regular, _baseFont.Size*0.95f, "Consolas");
			var lineHeight = Math.Max(mono.GetHeight() + 2, 16f);
			var wrappedLines = new List<string>();
			foreach(var line in codeLines){
				var wrapped = WrapText(line, mono, Math.Max(8, width - 16));
				wrappedLines.AddRange(wrapped);
			}
			if(wrappedLines.Count == 0) wrappedLines.Add(string.Empty);
			var blockHeight = wrappedLines.Count*lineHeight + 8;
			_ops.Add(DrawOp.FilledRect(0, y, width, blockHeight, _codeBackColor));
			_ops.Add(DrawOp.StrokeRect(new RectangleF(0, y, width, blockHeight), _codeBorderColor));
			var visualLine = 0;
			foreach(var sourceLine in codeLines){
				var wrapped = WrapText(sourceLine, mono, Math.Max(8, width - 16));
				foreach(var line in wrapped){
					AppendVisibleText(line, 8, y + 4 + visualLine*lineHeight, mono, _textColor, false, false, null, width - 16);
					visualLine++;
				}
				AppendLineBreak();
			}
			if(codeLines.Count == 0) AppendLineBreak();
			return y + blockHeight + 6;
		}
		private float AddInlineParagraph(string text, float y, int width, int headingLevel, bool quote, int indent, string bullet){
			var style = headingLevel > 0 ? FontStyle.Bold : FontStyle.Regular;
			var size = _baseFont.Size;
			if(headingLevel > 0) size += (4 - Math.Min(headingLevel, 3))*2f;
			var paragraphFont = GetFont(style, size);
			var lineHeight = Math.Max(paragraphFont.GetHeight() + 2, 16f);
			var x = (float)indent;
			var currentY = y;
			if(!string.IsNullOrEmpty(bullet)){
				AppendVisibleText(bullet, 0, y, paragraphFont, _textColor, false, false, null, Math.Max(8, indent - 6));
				_plainText.Append(' ');
			}
			var runs = ParseInline(text);
			foreach(var run in runs){
				if(run.Kind == InlineKind.Break){
					AppendLineBreak();
					x = indent;
					currentY += lineHeight;
					continue;
				}
				var font = ResolveRunFont(paragraphFont, run);
				var color = run.IsLink ? _linkColor : run.Kind == InlineKind.Code ? _textColor : _textColor;
				foreach(var token in EnumerateDrawTokens(run.Text)) AddWrappedToken(token, font, color, run.Strike, run.Kind == InlineKind.Code, run.IsLink ? run.LinkUrl : null, ref x, ref currentY, lineHeight, indent, width + indent);
			}
			AppendLineBreak();
			return currentY + lineHeight + (quote ? 2 : 4);
		}
		private void AddWrappedToken(string token, Font font, Color color, bool strike, bool code, string linkUrl, ref float x, ref float y, float lineHeight, int indent, int rightEdge){
			if(token.Length == 0) return;
			var tokenStart = 0;
			while(tokenStart < token.Length){
				var available = Math.Max(1f, rightEdge - x);
				var remaining = token.Length - tokenStart;
				var fit = FittedCharacterCount(token, tokenStart, remaining, font, available);
				if(fit == 0 && x > indent){
					x = indent;
					y += lineHeight;
					available = Math.Max(1f, rightEdge - x);
					fit = FittedCharacterCount(token, tokenStart, remaining, font, available);
				}
				if(fit == 0) fit = Math.Max(1, UnicodeText.NextCaretPosition(token, tokenStart) - tokenStart);
				var piece = token.Substring(tokenStart, fit);
				var metrics = MeasureCharacterOffsets(piece, font);
				var rect = new RectangleF(x, y, metrics[metrics.Length - 1] + 1, lineHeight);
				if(code){
					_ops.Add(DrawOp.FilledRect(rect.X - 1, rect.Y + 1, rect.Width + 2, rect.Height - 2, _codeBackColor));
					_ops.Add(DrawOp.StrokeRect(new RectangleF(rect.X - 1, rect.Y + 1, rect.Width + 2, rect.Height - 2), _codeBorderColor));
				}
				var textStart = _plainText.Length;
				_plainText.Append(piece);
				var op = DrawOp.Text(piece, rect, font, color, textStart, metrics, strike, code ? TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine : TextFormatFlags.Default);
				_ops.Add(op);
				if(linkUrl != null) _linkRegions.Add(new LinkRegion(rect, linkUrl));
				x += metrics[metrics.Length - 1];
				tokenStart += fit;
				if(tokenStart < token.Length){
					x = indent;
					y += lineHeight;
				}
			}
		}
		private void AppendVisibleText(string text, float x, float y, Font font, Color color, bool strike, bool code, string linkUrl, float maximumWidth){
			if(string.IsNullOrEmpty(text)) return;
			var metrics = MeasureCharacterOffsets(text, font);
			var rect = new RectangleF(x, y, Math.Min(maximumWidth, metrics[metrics.Length - 1] + 1), Math.Max(font.GetHeight() + 2, 16f));
			var start = _plainText.Length;
			_plainText.Append(text);
			_ops.Add(DrawOp.Text(text, rect, font, color, start, metrics, strike, code ? TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine : TextFormatFlags.Default));
			if(linkUrl != null) _linkRegions.Add(new LinkRegion(rect, linkUrl));
		}
		private int FittedCharacterCount(string text, int start, int count, Font font, float width){
			if(count <= 0) return 0;
			var measuredText = start == 0 && count == text.Length ? text : text.Substring(start, count);
			var offsets = MeasureCharacterOffsets(measuredText, font);
			var fitted = 0;
			var low = 1;
			var high = count;
			while(low <= high){
				var candidate = low + (high - low)/2;
				if(offsets[candidate] <= width){
					fitted = candidate;
					low = candidate + 1;
				} else{ high = candidate - 1; }
			}
			return UnicodeText.CaretPositionAtOrBefore(measuredText, fitted);
		}
		private float[] MeasureCharacterOffsets(string text, Font font){
			if(!_measurementCache.TryGetValue(font, out var fontMeasurements)){
				fontMeasurements = new Dictionary<string, float[]>(StringComparer.Ordinal);
				_measurementCache[font] = fontMeasurements;
			}
			if(fontMeasurements.TryGetValue(text, out var cached)) return cached;
			var offsets = new float[text.Length + 1];
			if(text.Length > 0){
				if(text.Length <= ExactTextRendererMeasurementLimit || UnicodeText.RequiresShapingAwareMeasurement(text)){
					var previousBoundary = 0;
					var previousWidth = 0f;
					foreach(var boundary in UnicodeText.GetCaretBoundaries(text)){
						if(boundary == 0) continue;
						var width = Math.Max(previousWidth, MeasureLogicalTextWidth(text.Substring(0, boundary), font));
						for(var index = previousBoundary + 1; index < boundary; index++) offsets[index] = previousWidth;
						offsets[boundary] = width;
						previousBoundary = boundary;
						previousWidth = width;
					}
				} else{
					SelectMeasureFont(font);
					var cumulativeWidths = new int[text.Length];
					if(!GdiText.GetTextExtentExPoint(_measureHdc, text, text.Length, int.MaxValue, out _, cumulativeWidths, out _)) throw new InvalidOperationException("Unable to measure rendered text.");
					var measuredWidth = cumulativeWidths[cumulativeWidths.Length - 1];
					var targetWidth = MeasureLogicalTextWidth(text, font);
					var scale = measuredWidth <= 0 ? 1f : targetWidth/measuredWidth;
					for(var i = 0; i < text.Length; i++) offsets[i + 1] = cumulativeWidths[i]*scale;
				}
			}
			fontMeasurements[text] = offsets;
			_measurementCacheEntryCount++;
			return offsets;
		}
		private float MeasureLogicalTextWidth(string text, Font font){
			if(!_measurementPrefixWidths.TryGetValue(font, out var prefixWidth)){
				_textRendererMeasurementCount++;
				prefixWidth = TextRenderer.MeasureText(MeasurementPrefix, font, new Size(int.MaxValue, int.MaxValue), TextRendererFlags).Width;
				_measurementPrefixWidths[font] = prefixWidth;
			}
			_textRendererMeasurementCount++;
			var measured = TextRenderer.MeasureText(MeasurementPrefix + text, font, new Size(int.MaxValue, int.MaxValue), TextRendererFlags).Width;
			return Math.Max(0, measured - prefixWidth);
		}
		private float MeasureTextWidth(string text, Font font){
			if(string.IsNullOrEmpty(text)) return 0f;
			var offsets = MeasureCharacterOffsets(text, font);
			return offsets[offsets.Length - 1];
		}
		private void SelectMeasureFont(Font font){
			if(!_fontHandles.TryGetValue(font, out var handle)){
				handle = font.ToHfont();
				_fontHandles[font] = handle;
			}
			if(handle == _selectedMeasureFont) return;
			var previous = GdiText.SelectObject(_measureHdc, handle);
			if(_originalMeasureFont == IntPtr.Zero) _originalMeasureFont = previous;
			_selectedMeasureFont = handle;
		}
		private static IEnumerable<string> EnumerateDrawTokens(string text){
			if(string.IsNullOrEmpty(text)) yield break;
			var index = 0;
			while(index < text.Length){
				var start = index;
				var whitespace = char.IsWhiteSpace(text[index]);
				while(index < text.Length && char.IsWhiteSpace(text[index]) == whitespace) index++;
				yield return text.Substring(start, index - start);
			}
		}
		private Font ResolveRunFont(Font paragraphFont, InlineRun run){
			var style = paragraphFont.Style;
			if(run.Bold) style |= FontStyle.Bold;
			if(run.Italic) style |= FontStyle.Italic;
			if(run.Kind == InlineKind.Code) return GetFont(FontStyle.Regular, paragraphFont.Size*0.95f, "Consolas");
			return GetFont(style, paragraphFont.Size, paragraphFont.FontFamily.Name);
		}
		private List<InlineRun> ParseInline(string text){
			var runs = new List<InlineRun>();
			ParseInlineInto(text, false, false, false, false, null, runs);
			return runs;
		}
		private void ParseInlineInto(string text, bool bold, bool italic, bool strike, bool isLink, string linkUrl, List<InlineRun> runs){
			var index = 0;
			while(index < text.Length){
				if(text[index] == '\\' && index + 1 < text.Length){
					runs.Add(new InlineRun(text.Substring(index + 1, 1), bold, italic, strike, isLink, InlineKind.Text, linkUrl));
					index += 2;
					continue;
				}
				if(index + 1 < text.Length && text[index] == ' ' && text[index + 1] == ' '){
					runs.Add(InlineRun.Break());
					index += 2;
					continue;
				}
				var remaining = text.Substring(index);
				var image = ImageRegex.Match(remaining);
				if(image.Success){
					var label = string.IsNullOrEmpty(image.Groups[1].Value) ? "[Image]" : "[Image: " + image.Groups[1].Value + "]";
					runs.Add(new InlineRun(label, bold, italic, strike, true, InlineKind.Text, image.Groups[2].Value));
					index += image.Length;
					continue;
				}
				var link = LinkRegex.Match(remaining);
				if(link.Success){
					ParseInlineInto(link.Groups[1].Value, bold, italic, strike, true, link.Groups[2].Value, runs);
					index += link.Length;
					continue;
				}
				var url = UrlRegex.Match(remaining);
				if(url.Success){
					var value = url.Groups[1].Value.TrimEnd('.', ',', ';', ':', '!', '?');
					runs.Add(new InlineRun(value, bold, italic, strike, true, InlineKind.Text, value));
					index += value.Length;
					continue;
				}
				if(TryParseDelimited(text, ref index, "***", true, true, strike, isLink, linkUrl, runs)) continue;
				if(TryParseDelimited(text, ref index, "___", true, true, strike, isLink, linkUrl, runs)) continue;
				if(TryParseDelimited(text, ref index, "**", true, italic, strike, isLink, linkUrl, runs)) continue;
				if(TryParseDelimited(text, ref index, "__", true, italic, strike, isLink, linkUrl, runs)) continue;
				if(TryParseDelimited(text, ref index, "~~", bold, italic, true, isLink, linkUrl, runs)) continue;
				if(TryParseDelimited(text, ref index, "*", bold, true, strike, isLink, linkUrl, runs)) continue;
				if(TryParseDelimited(text, ref index, "_", bold, true, strike, isLink, linkUrl, runs)) continue;
				if(text[index] == '`'){
					var markerLength = 1;
					while(index + markerLength < text.Length && text[index + markerLength] == '`') markerLength++;
					var marker = new string('`', markerLength);
					var end = text.IndexOf(marker, index + markerLength, StringComparison.Ordinal);
					if(end >= 0){
						var code = text.Substring(index + markerLength, end - index - markerLength);
						runs.Add(new InlineRun(code, bold, italic, strike, isLink, InlineKind.Code, linkUrl));
						index = end + markerLength;
						continue;
					}
				}
				var next = FindNextSpecial(text, index + 1);
				runs.Add(new InlineRun(text.Substring(index, next - index), bold, italic, strike, isLink, InlineKind.Text, linkUrl));
				index = next;
			}
		}
		private bool TryParseDelimited(string text, ref int index, string marker, bool bold, bool italic, bool strike, bool isLink, string linkUrl, List<InlineRun> runs){
			if(index + marker.Length > text.Length || string.CompareOrdinal(text, index, marker, 0, marker.Length) != 0) return false;
			var end = text.IndexOf(marker, index + marker.Length, StringComparison.Ordinal);
			if(end <= index + marker.Length) return false;
			ParseInlineInto(text.Substring(index + marker.Length, end - index - marker.Length), bold, italic, strike, isLink, linkUrl, runs);
			index = end + marker.Length;
			return true;
		}
		private static int FindNextSpecial(string text, int start){
			for(var i = start; i < text.Length; i++){
				var ch = text[i];
				if(ch == '\\' || ch == '*' || ch == '_' || ch == '~' || ch == '`' || ch == '[' || ch == '!') return i;
				if((ch == 'h' || ch == 'H') && i + 7 < text.Length && (text.IndexOf("http://", i, StringComparison.OrdinalIgnoreCase) == i || text.IndexOf("https://", i, StringComparison.OrdinalIgnoreCase) == i)) return i;
			}
			return text.Length;
		}
		private static int CountIndent(string whitespace){
			var count = 0;
			foreach(var character in whitespace) count += character == '\t' ? 4 : 1;
			return count/2;
		}
		private void AppendLineBreak(){
			if(_plainText.Length == 0 || _plainText[_plainText.Length - 1] != '\n') _plainText.Append('\n');
		}
		internal void Draw(Graphics graphics, int selectionStart, int selectionLength, Color selectionBackColor, Color selectionTextColor, float offsetX = 0, float offsetY = 0){
			foreach(var op in _ops) op.DrawNonText(graphics, offsetX, offsetY);
			if(selectionLength > 0)
				foreach(var op in _ops)
					op.DrawSelectionBackground(graphics, selectionStart, selectionLength, selectionBackColor, offsetX, offsetY);
			foreach(var op in _ops) op.DrawBaseText(graphics, offsetX, offsetY);
			if(selectionLength > 0)
				foreach(var op in _ops)
					op.DrawSelectionText(graphics, selectionStart, selectionLength, selectionTextColor, offsetX, offsetY);
		}
		internal string HitTestLink(Point point){
			foreach(var link in _linkRegions)
				if(link.Rect.Contains(point))
					return link.Url;
			return null;
		}
		internal int HitTestText(Point point){
			DrawOp closest = null;
			var closestDistance = float.MaxValue;
			foreach(var op in _ops){
				if(!op.IsText) continue;
				if(op.Rect.Contains(point)) return op.HitTestCharacter(point.X);
				var dx = point.X < op.Rect.Left ? op.Rect.Left - point.X : point.X > op.Rect.Right ? point.X - op.Rect.Right : 0;
				var dy = point.Y < op.Rect.Top ? op.Rect.Top - point.Y : point.Y > op.Rect.Bottom ? point.Y - op.Rect.Bottom : 0;
				var distance = dx*dx + dy*dy;
				if(distance < closestDistance){
					closestDistance = distance;
					closest = op;
				}
			}
			if(closest == null) return 0;
			if(point.Y > closest.Rect.Bottom || point.X > closest.Rect.Right) return closest.TextStart + closest.TextLength;
			return closest.TextStart;
		}
		internal int GetLineBoundary(int characterIndex, bool end){
			var lines = GetVisualLines();
			if(lines.Count == 0) return 0;
			var line = lines[FindLineIndex(lines, characterIndex)];
			return end ? line.End : line.Start;
		}
		internal int MoveCaretVertically(int characterIndex, int lineDelta, float preferredX){
			var lines = GetVisualLines();
			if(lines.Count == 0) return 0;
			var current = FindLineIndex(lines, characterIndex);
			var target = Math.Max(0, Math.Min(lines.Count - 1, current + lineDelta));
			return HitTestLine(lines[target], preferredX);
		}
		internal int MoveCaretByPixels(int characterIndex, float yDelta, float preferredX){
			var lines = GetVisualLines();
			if(lines.Count == 0) return 0;
			var current = FindLineIndex(lines, characterIndex);
			var targetY = lines[current].Top + yDelta;
			var target = 0;
			var distance = float.MaxValue;
			for(var i = 0; i < lines.Count; i++){
				var candidate = Math.Abs(lines[i].Top - targetY);
				if(candidate < distance){
					distance = candidate;
					target = i;
				}
			}
			return HitTestLine(lines[target], preferredX);
		}
		private List<VisualLine> GetVisualLines(){
			var lines = new List<VisualLine>();
			foreach(var op in _ops){
				if(!op.IsText) continue;
				VisualLine line = null;
				foreach(var existing in lines)
					if(Math.Abs(existing.Top - op.Rect.Top) < 0.5f){
						line = existing;
						break;
					}
				if(line == null){
					line = new VisualLine(op.Rect.Top, op.Rect.Bottom);
					lines.Add(line);
				}
				line.Top = Math.Min(line.Top, op.Rect.Top);
				line.Bottom = Math.Max(line.Bottom, op.Rect.Bottom);
				line.Start = Math.Min(line.Start, op.TextStart);
				line.End = Math.Max(line.End, op.TextStart + op.TextLength);
				line.Ops.Add(op);
			}
			lines.Sort((left, right) => left.Top.CompareTo(right.Top));
			return lines;
		}
		private static int FindLineIndex(List<VisualLine> lines, int characterIndex){
			for(var i = 0; i < lines.Count; i++)
				if(characterIndex >= lines[i].Start && characterIndex < lines[i].End)
					return i;
			for(var i = 0; i < lines.Count; i++)
				if(characterIndex == lines[i].Start)
					return i;
			var closest = 0;
			var distance = int.MaxValue;
			for(var i = 0; i < lines.Count; i++){
				var candidate = characterIndex < lines[i].Start ? lines[i].Start - characterIndex : characterIndex - lines[i].End;
				if(candidate < distance){
					distance = candidate;
					closest = i;
				}
			}
			return closest;
		}
		private static int HitTestLine(VisualLine line, float x){
			DrawOp closest = null;
			var distance = float.MaxValue;
			foreach(var op in line.Ops){
				if(x >= op.Rect.Left && x <= op.Rect.Right) return op.HitTestCharacter(x);
				var candidate = x < op.Rect.Left ? op.Rect.Left - x : x - op.Rect.Right;
				if(candidate < distance){
					distance = candidate;
					closest = op;
				}
			}
			return closest == null ? line.Start : closest.HitTestCharacter(x);
		}
		internal RectangleF GetCaretRectangle(int characterIndex){
			DrawOp closest = null;
			foreach(var op in _ops){
				if(!op.IsText) continue;
				closest = op;
				if(characterIndex >= op.TextStart && characterIndex <= op.TextStart + op.TextLength) return op.GetCaretRectangle(characterIndex);
			}
			return closest?.GetCaretRectangle(closest.TextStart + closest.TextLength) ?? new RectangleF(0, 0, 1, _baseFont?.GetHeight() ?? 16);
		}
		private Font GetFont(FontStyle style, float? size = null, string family = null){
			if(_baseFont == null) _baseFont = Control.DefaultFont;
			var fontSize = size ?? _baseFont.Size;
			var fontFamily = family ?? _baseFont.FontFamily.Name;
			var key = fontFamily + "|" + fontSize.ToString("0.##") + "|" + (int)style;
			if(_fontCache.TryGetValue(key, out var font)) return font;
			try{ font = new Font(fontFamily, fontSize, style, GraphicsUnit.Point); } catch(ArgumentException){ font = new Font(_baseFont.FontFamily, fontSize, style, GraphicsUnit.Point); }
			_fontCache[key] = font;
			return font;
		}
		private sealed class VisualLine{
			internal readonly List<DrawOp> Ops = new List<DrawOp>();
			internal float Bottom;
			internal int End;
			internal int Start = int.MaxValue;
			internal float Top;
			internal VisualLine(float top, float bottom){
				Top = top;
				Bottom = bottom;
			}
		}
		private sealed class LinkRegion{
			internal readonly RectangleF Rect;
			internal readonly string Url;
			internal LinkRegion(RectangleF rect, string url){
				Rect = rect;
				Url = url;
			}
		}
		private enum InlineKind{
			Text,
			Code,
			Break
		}
		private struct InlineRun{
			internal readonly string Text;
			internal readonly bool Bold;
			internal readonly bool Italic;
			internal readonly bool Strike;
			internal readonly bool IsLink;
			internal readonly InlineKind Kind;
			internal readonly string LinkUrl;
			internal InlineRun(string text, bool bold, bool italic, bool strike, bool isLink, InlineKind kind, string linkUrl){
				Text = text;
				Bold = bold;
				Italic = italic;
				Strike = strike;
				IsLink = isLink;
				Kind = kind;
				LinkUrl = linkUrl;
			}
			internal static InlineRun Break(){return new InlineRun(string.Empty, false, false, false, false, InlineKind.Break, null);}
		}
		private sealed class DrawOp{
			private readonly float[] _characterOffsets;
			private readonly Color _color;
			private readonly TextFormatFlags _flags;
			private readonly Font _font;
			private readonly Image _image;
			private readonly OpKind _kind;
			private readonly bool _strike;
			private readonly string _text;
			private readonly int[] _caretStops;
			internal readonly RectangleF Rect;
			internal readonly int TextStart;
			private DrawOp(OpKind kind, RectangleF rect, Color color, string text = null, Font font = null, int textStart = 0, float[] characterOffsets = null, bool strike = false, TextFormatFlags flags = TextFormatFlags.Default, Image image = null){
				_kind = kind;
				Rect = rect;
				_color = color;
				_text = text;
				_caretStops = text == null ? null : UnicodeText.GetCaretBoundaries(text).ToArray();
				_font = font;
				TextStart = textStart;
				_characterOffsets = characterOffsets;
				_strike = strike;
				_flags = flags;
				_image = image;
			}
			internal bool IsText => _kind == OpKind.Text;
			internal int TextLength => _text?.Length ?? 0;
			internal static DrawOp Text(string text, RectangleF rect, Font font, Color color, int textStart, float[] characterOffsets, bool strike = false, TextFormatFlags flags = TextFormatFlags.Default){return new DrawOp(OpKind.Text, rect, color, text, font, textStart, characterOffsets, strike, flags);}
			internal static DrawOp Image(RectangleF rect, Image image){return new DrawOp(OpKind.Image, rect, Color.Empty, image: image);}
			internal static DrawOp FilledRect(float x, float y, float width, float height, Color color){return new DrawOp(OpKind.Rectangle, new RectangleF(x, y, width, height), color);}
			internal static DrawOp StrokeRect(RectangleF rect, Color color){return new DrawOp(OpKind.StrokeRectangle, rect, color);}
			internal static DrawOp Line(float x, float y, float width, Color color){return new DrawOp(OpKind.Line, new RectangleF(x, y, width, 1), color);}
			internal void DrawNonText(Graphics graphics, float offsetX, float offsetY){
				if(IsText) return;
				var renderedRect = new RectangleF(Rect.X + offsetX, Rect.Y + offsetY, Rect.Width, Rect.Height);
				if(!renderedRect.IntersectsWith(graphics.VisibleClipBounds)) return;
				switch(_kind){
					case OpKind.Image:
						graphics.DrawImage(_image, renderedRect);
						return;
					case OpKind.Rectangle:
						using(var brush = new SolidBrush(_color)){ graphics.FillRectangle(brush, renderedRect); }
						return;
					case OpKind.StrokeRectangle:
						using(var pen = new Pen(_color)){ graphics.DrawRectangle(pen, renderedRect.X, renderedRect.Y, renderedRect.Width, renderedRect.Height); }
						return;
					case OpKind.Line:
						using(var pen = new Pen(_color)){ graphics.DrawLine(pen, renderedRect.Left, renderedRect.Top, renderedRect.Right, renderedRect.Top); }
						return;
				}
			}
			internal void DrawSelectionBackground(Graphics graphics, int selectionStart, int selectionLength, Color selectionBackColor, float offsetX, float offsetY){
				if(!TryGetSelectionRectangle(selectionStart, selectionLength, offsetX, offsetY, out _, out var selectionRect)) return;
				using(var brush = new SolidBrush(selectionBackColor)){ graphics.FillRectangle(brush, selectionRect); }
			}
			internal void DrawBaseText(Graphics graphics, float offsetX, float offsetY){
				if(!IsText) return;
				var renderedRect = new RectangleF(Rect.X + offsetX, Rect.Y + offsetY, Rect.Width, Rect.Height);
				if(!renderedRect.IntersectsWith(graphics.VisibleClipBounds)) return;
				DrawText(graphics, _text, renderedRect, _color);
				if(_strike) DrawStrike(graphics, renderedRect, _color);
			}
			internal void DrawSelectionText(Graphics graphics, int selectionStart, int selectionLength, Color selectionTextColor, float offsetX, float offsetY){
				if(!TryGetSelectionRectangle(selectionStart, selectionLength, offsetX, offsetY, out var renderedRect, out var selectionRect)) return;
				var state = graphics.Save();
				graphics.SetClip(selectionRect, System.Drawing.Drawing2D.CombineMode.Intersect);
				DrawText(graphics, _text, renderedRect, selectionTextColor);
				if(_strike) DrawStrike(graphics, renderedRect, selectionTextColor);
				graphics.Restore(state);
			}
			private bool TryGetSelectionRectangle(int selectionStart, int selectionLength, float offsetX, float offsetY, out RectangleF renderedRect, out RectangleF selectionRect){
				renderedRect = new RectangleF(Rect.X + offsetX, Rect.Y + offsetY, Rect.Width, Rect.Height);
				selectionRect = RectangleF.Empty;
				if(!IsText || selectionLength <= 0) return false;
				var selectionEnd = selectionStart + selectionLength;
				var overlapStart = Math.Max(selectionStart, TextStart);
				var overlapEnd = Math.Min(selectionEnd, TextStart + TextLength);
				if(overlapStart >= overlapEnd) return false;
				var localStart = overlapStart - TextStart;
				var localEnd = overlapEnd - TextStart;
				var pixelRect = Rectangle.Round(renderedRect);
				var left = pixelRect.X + _characterOffsets[localStart];
				var right = pixelRect.X + _characterOffsets[localEnd];
				selectionRect = new RectangleF(left, pixelRect.Y, Math.Max(1, right - left), pixelRect.Height);
				return true;
			}
			private void DrawText(Graphics graphics, string text, RectangleF rect, Color color){TextRenderer.DrawText(graphics, text, _font, Rectangle.Round(rect), color, TextRendererFlags | _flags);}
			private static void DrawStrike(Graphics graphics, RectangleF rect, Color color){
				using(var pen = new Pen(color)){
					var y = rect.Y + rect.Height/2f;
					graphics.DrawLine(pen, rect.X, y, rect.Right, y);
				}
			}
			internal int HitTestCharacter(float x){
				var localX = x - (float)Math.Round(Rect.X);
				if(localX <= 0) return TextStart;
				for(var i = 0; i < _caretStops.Length - 1; i++){
					var start = _caretStops[i];
					var end = _caretStops[i + 1];
					var midpoint = (_characterOffsets[start] + _characterOffsets[end])/2f;
					if(localX < midpoint) return TextStart + start;
				}
				return TextStart + TextLength;
			}
			internal RectangleF GetCaretRectangle(int characterIndex){
				var localIndex = Math.Max(0, Math.Min(TextLength, characterIndex - TextStart));
				localIndex = UnicodeText.NormalizeCaretPosition(_text, localIndex);
				return new RectangleF((float)Math.Round(Rect.X) + _characterOffsets[localIndex], (float)Math.Round(Rect.Y) + 1, 1, (float)Math.Max(1, Math.Round(Rect.Height) - 2));
			}
			private enum OpKind{
				Text,
				Image,
				Rectangle,
				StrokeRectangle,
				Line
			}
		}
		private static class GdiText{
			[DllImport("gdi32.dll")]
			internal static extern IntPtr CreateCompatibleDC(IntPtr hdc);
			[DllImport("gdi32.dll")]
			[return: MarshalAs(UnmanagedType.Bool)]
			internal static extern bool DeleteDC(IntPtr hdc);
			[DllImport("gdi32.dll")]
			internal static extern IntPtr SelectObject(IntPtr hdc, IntPtr value);
			[DllImport("gdi32.dll")]
			[return: MarshalAs(UnmanagedType.Bool)]
			internal static extern bool DeleteObject(IntPtr value);
			[DllImport("gdi32.dll", CharSet = CharSet.Unicode)]
			[return: MarshalAs(UnmanagedType.Bool)]
			internal static extern bool GetTextExtentExPoint(IntPtr hdc, string text, int count, int maximumExtent, out int fitted, [Out] int[] cumulativeWidths, out Size size);
		}
	}
	internal static class UnicodeText{
		internal static List<int> GetCaretBoundaries(string text){
			var boundaries = new List<int>{0};
			if(string.IsNullOrEmpty(text)) return boundaries;
			var index = 0;
			while(index < text.Length){
				var codePoint = char.ConvertToUtf32(text, index);
				index += CodePointLength(codePoint);
				if(codePoint == '\r' && index < text.Length && text[index] == '\n') index++;
				else if(IsRegionalIndicator(codePoint) && index < text.Length){
					var next = char.ConvertToUtf32(text, index);
					if(IsRegionalIndicator(next)) index += CodePointLength(next);
				}
				ConsumeExtenders(text, ref index);
				while(index < text.Length && char.ConvertToUtf32(text, index) == 0x200D){
					index++;
					if(index >= text.Length) break;
					var joined = char.ConvertToUtf32(text, index);
					index += CodePointLength(joined);
					ConsumeExtenders(text, ref index);
				}
				boundaries.Add(index);
			}
			return boundaries;
		}
		internal static bool RequiresShapingAwareMeasurement(string text){
			for(var index = 0; index < text.Length; index++){
				var value = text[index];
				if(char.IsSurrogate(value) || value == '\u200D' || value >= '\uFE00' && value <= '\uFE0F') return true;
				var category = CharUnicodeInfo.GetUnicodeCategory(text, index);
				if(category == UnicodeCategory.NonSpacingMark || category == UnicodeCategory.SpacingCombiningMark || category == UnicodeCategory.EnclosingMark) return true;
			}
			return false;
		}
		internal static int PreviousCaretPosition(string text, int position){
			position = Math.Max(0, Math.Min(text?.Length ?? 0, position));
			var previous = 0;
			foreach(var boundary in GetCaretBoundaries(text)){
				if(boundary >= position) return previous;
				previous = boundary;
			}
			return previous;
		}
		internal static int NextCaretPosition(string text, int position){
			position = Math.Max(0, Math.Min(text?.Length ?? 0, position));
			foreach(var boundary in GetCaretBoundaries(text))
				if(boundary > position)
					return boundary;
			return text?.Length ?? 0;
		}
		internal static int CaretPositionAtOrBefore(string text, int position){
			position = Math.Max(0, Math.Min(text?.Length ?? 0, position));
			var previous = 0;
			foreach(var boundary in GetCaretBoundaries(text)){
				if(boundary > position) return previous;
				previous = boundary;
			}
			return previous;
		}
		internal static int NormalizeCaretPosition(string text, int position){
			position = Math.Max(0, Math.Min(text?.Length ?? 0, position));
			var previous = 0;
			foreach(var boundary in GetCaretBoundaries(text)){
				if(boundary == position) return boundary;
				if(boundary > position) return position - previous <= boundary - position ? previous : boundary;
				previous = boundary;
			}
			return previous;
		}
		private static void ConsumeExtenders(string text, ref int index){
			while(index < text.Length){
				var codePoint = char.ConvertToUtf32(text, index);
				if(!IsExtender(text, index, codePoint)) return;
				index += CodePointLength(codePoint);
			}
		}
		private static bool IsExtender(string text, int index, int codePoint){
			if(codePoint >= 0xFE00 && codePoint <= 0xFE0F || codePoint >= 0xE0100 && codePoint <= 0xE01EF) return true;
			if(codePoint >= 0x1F3FB && codePoint <= 0x1F3FF || codePoint >= 0xE0020 && codePoint <= 0xE007F) return true;
			var category = CharUnicodeInfo.GetUnicodeCategory(text, index);
			return category == UnicodeCategory.NonSpacingMark || category == UnicodeCategory.SpacingCombiningMark || category == UnicodeCategory.EnclosingMark;
		}
		private static bool IsRegionalIndicator(int codePoint){return codePoint >= 0x1F1E6 && codePoint <= 0x1F1FF;}
		private static int CodePointLength(int codePoint){return codePoint > 0xFFFF ? 2 : 1;}
	}
}
