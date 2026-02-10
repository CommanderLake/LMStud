using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;
namespace LMStud{
	internal sealed class MarkdownRenderControl : ScrollableControl{
		private readonly GdiMarkdownRenderer _renderer = new GdiMarkdownRenderer();
		private string _markdownText = string.Empty;
		internal MarkdownRenderControl(){
			DoubleBuffered = true;
			SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
			BackColor = Color.White;
			AutoScroll = true;
			TabStop = false;
		}
		internal string MarkdownText{
			get => _markdownText;
			set{
				value = value ?? string.Empty;
				if(_markdownText == value) return;
				_markdownText = value;
				Relayout();
			}
		}
		internal event EventHandler ContentHeightChanged;
		protected override void OnSizeChanged(EventArgs e){
			base.OnSizeChanged(e);
			Relayout();
		}
		protected override void OnFontChanged(EventArgs e){
			base.OnFontChanged(e);
			Relayout();
		}
		private void Relayout(){
			var width = Math.Max(10, ClientSize.Width - 8);
			_renderer.Layout(_markdownText, Font, width, ForeColor, BackColor);
			AutoScrollMinSize = new Size(0, _renderer.TotalHeight);
			ContentHeightChanged?.Invoke(this, EventArgs.Empty);
			Invalidate();
		}
		protected override void OnPaint(PaintEventArgs e){
			base.OnPaint(e);
			e.Graphics.Clear(BackColor);
			e.Graphics.TranslateTransform(AutoScrollPosition.X + 4, AutoScrollPosition.Y + 4);
			_renderer.Draw(e.Graphics);
		}
		protected override void OnMouseMove(MouseEventArgs e){
			base.OnMouseMove(e);
			var p = new Point(e.X - AutoScrollPosition.X - 4, e.Y - AutoScrollPosition.Y - 4);
			Cursor = _renderer.HitTestLink(p) != null ? Cursors.Hand : Cursors.IBeam;
		}
		protected override void OnMouseLeave(EventArgs e){
			base.OnMouseLeave(e);
			Cursor = Cursors.IBeam;
		}
		protected override void OnMouseClick(MouseEventArgs e){
			base.OnMouseClick(e);
			if(e.Button != MouseButtons.Left) return;
			var p = new Point(e.X - AutoScrollPosition.X - 4, e.Y - AutoScrollPosition.Y - 4);
			var link = _renderer.HitTestLink(p);
			if(string.IsNullOrEmpty(link)) return;
			try{ Process.Start(link); } catch{}
		}
		protected override void Dispose(bool disposing){
			if(disposing) _renderer.Dispose();
			base.Dispose(disposing);
		}
	}
	internal sealed class GdiMarkdownRenderer : IDisposable{
		private static readonly Regex OrderedListRegex = new Regex("^\\s{0,3}(\\d+)\\.\\s+", RegexOptions.Compiled);
		private static readonly Regex UnorderedListRegex = new Regex("^\\s{0,3}([*+-])\\s+", RegexOptions.Compiled);
		private static readonly Regex LinkRegex = new Regex("^\\[([^\\]]+)\\]\\(([^\\)]+)\\)", RegexOptions.Compiled);
		private static readonly Regex ImageRegex = new Regex("^!\\[([^\\]]*)\\]\\(([^\\)]+)\\)", RegexOptions.Compiled);
		private static readonly Regex UrlRegex = new Regex("^(https?://\\S+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
		private readonly Dictionary<string, Font> _fontCache = new Dictionary<string, Font>();
		private readonly List<LinkRegion> _linkRegions = new List<LinkRegion>();
		private readonly List<DrawOp> _ops = new List<DrawOp>();
		private readonly StringFormat _stringFormat = new StringFormat(StringFormat.GenericTypographic){ FormatFlags = StringFormatFlags.MeasureTrailingSpaces };
		private Color _backColor;
		private Font _baseFont;
		private Color _textColor;
		internal int TotalHeight{get; private set;}
		public void Dispose(){
			foreach(var kv in _fontCache) kv.Value.Dispose();
			_fontCache.Clear();
			_stringFormat.Dispose();
		}
		internal void Layout(string markdown, Font baseFont, int width, Color textColor, Color backColor){
			_ops.Clear();
			_linkRegions.Clear();
			_baseFont = baseFont ?? Control.DefaultFont;
			_textColor = textColor;
			_backColor = backColor;
			var y = 0f;
			var lines = (markdown ?? string.Empty).Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
			for(var i = 0; i < lines.Length; i++){
				var line = lines[i];
				if(line.StartsWith("```") || line.StartsWith("~~~")){
					var fence = line.Substring(0, 3);
					var code = new StringBuilder();
					i++;
					while(i < lines.Length && !lines[i].StartsWith(fence)){
						code.AppendLine(lines[i]);
						i++;
					}
					y = AddCodeBlock(code.ToString().TrimEnd('\n', '\r'), y, width);
					continue;
				}
				if(string.IsNullOrWhiteSpace(line)){
					y += _baseFont.GetHeight()*0.6f;
					continue;
				}
				if(IsHorizontalRule(line)){
					_ops.Add(DrawOp.Line(0, y + 4, width, Color.Silver));
					y += 10;
					continue;
				}
				if(TryParseHeading(line, out var headingLevel)){
					var txt = line.Substring(headingLevel).Trim();
					y = AddInlineParagraph(txt, y, width, headingLevel, false, 0, null);
					y += 2;
					continue;
				}
				if(line.TrimStart().StartsWith(">")){
					var quote = line.TrimStart().Substring(1).TrimStart();
					_ops.Add(DrawOp.Rect(0, y, 4, _baseFont.GetHeight() + 4, Color.Gainsboro));
					y = AddInlineParagraph(quote, y, width - 10, 0, true, 10, null);
					continue;
				}
				var match = OrderedListRegex.Match(line);
				if(match.Success){
					var content = line.Substring(match.Length);
					var bullet = match.Groups[1].Value + ".";
					y = AddInlineParagraph(content, y, width - 24, 0, false, 24, bullet);
					continue;
				}
				match = UnorderedListRegex.Match(line);
				if(match.Success){
					var content = line.Substring(match.Length);
					y = AddInlineParagraph(content, y, width - 24, 0, false, 24, "•");
					continue;
				}
				if(IsTableHeader(lines, i)){
					y = AddTable(lines, ref i, y, width);
					continue;
				}
				y = AddInlineParagraph(line, y, width, 0, false, 0, null);
			}
			TotalHeight = (int)Math.Ceiling(y);
		}
		private static bool TryParseHeading(string line, out int level){
			level = 0;
			if(string.IsNullOrEmpty(line) || line[0] != '#') return false;
			while(level < line.Length && level < 6 && line[level] == '#') level++;
			if(level == 0 || level >= line.Length || line[level] != ' ') return false;
			return true;
		}
		private static bool IsHorizontalRule(string line){
			var t = line.Trim();
			if(t.Length < 3) return false;
			return t == "---" || t == "***" || t == "___";
		}
		private bool IsTableHeader(string[] lines, int i){
			if(i + 1 >= lines.Length) return false;
			return lines[i].Contains("|") && Regex.IsMatch(lines[i + 1].Trim(), "^\\|?\\s*:?-+:?\\s*(\\|\\s*:?-+:?\\s*)+\\|?$");
		}
		private float AddTable(string[] lines, ref int i, float y, int width){
			var rows = new List<string[]>{ SplitCells(lines[i]) };
			i += 2;
			while(i < lines.Length && lines[i].Contains("|")){
				rows.Add(SplitCells(lines[i]));
				i++;
			}
			i--;
			var cols = 0;
			foreach(var row in rows)
				if(row.Length > cols)
					cols = row.Length;
			if(cols == 0) return y;
			var cellWidth = Math.Max(60f, (width - 2)/(float)cols);
			var rowHeight = _baseFont.GetHeight() + 8;
			for(var r = 0; r < rows.Count; r++)
			for(var c = 0; c < cols; c++){
				var rect = new RectangleF(c*cellWidth, y + r*rowHeight, cellWidth, rowHeight);
				_ops.Add(DrawOp.Rect(rect.X, rect.Y, rect.Width, rect.Height, r == 0 ? Color.FromArgb(240, 240, 240) : Color.FromArgb(248, 248, 248)));
				_ops.Add(DrawOp.StrokeRect(rect, Color.LightGray));
				if(c < rows[r].Length) _ops.Add(DrawOp.Text(rows[r][c].Trim(), new RectangleF(rect.X + 4, rect.Y + 4, rect.Width - 8, rect.Height - 6), GetFont(FontStyle.Regular), _textColor));
			}
			return y + rows.Count*rowHeight + 6;
		}
		private static string[] SplitCells(string line){
			var t = line.Trim();
			if(t.StartsWith("|")) t = t.Substring(1);
			if(t.EndsWith("|")) t = t.Substring(0, t.Length - 1);
			return t.Split('|');
		}
		private float AddCodeBlock(string code, float y, int width){
			var mono = GetFont(FontStyle.Regular, _baseFont.Size*0.95f, "Consolas");
			var h = TextRenderer.MeasureText("Ay", mono).Height;
			var lines = code.Split(new[]{ "\n" }, StringSplitOptions.None);
			var blockHeight = Math.Max(h + 10, lines.Length*h + 8);
			_ops.Add(DrawOp.Rect(0, y, width, blockHeight, Color.FromArgb(245, 245, 245)));
			_ops.Add(DrawOp.StrokeRect(new RectangleF(0, y, width, blockHeight), Color.FromArgb(220, 220, 220)));
			for(var j = 0; j < lines.Length; j++) _ops.Add(DrawOp.Text(lines[j], new RectangleF(8, y + 4 + j*h, width - 16, h + 2), mono, Color.FromArgb(45, 45, 48)));
			return y + blockHeight + 6;
		}
		private float AddInlineParagraph(string text, float y, int width, int headingLevel, bool quote, int indent, string bullet){
			var style = FontStyle.Regular;
			var size = _baseFont.Size;
			if(headingLevel > 0){
				style = FontStyle.Bold;
				size += (4 - Math.Min(headingLevel, 3))*2;
			}
			var paraFont = GetFont(style, size);
			if(!string.IsNullOrEmpty(bullet)) _ops.Add(DrawOp.Text(bullet, new RectangleF(0, y, indent - 6, paraFont.GetHeight() + 2), paraFont, _textColor));
			var runs = ParseInline(text);
			var x = (float)indent;
			var lineHeight = Math.Max(paraFont.GetHeight() + 2, 16f);
			var curY = y;
			foreach(var run in runs){
				if(run.Kind == InlineKind.Break){
					x = indent;
					curY += lineHeight;
					continue;
				}
				var font = ResolveRunFont(paraFont, run);
				var color = run.IsLink ? Color.RoyalBlue : run.Kind == InlineKind.Code ? Color.FromArgb(45, 45, 48) : _textColor;
				foreach(var drawText in EnumerateDrawTokens(run.Text)){
					if(drawText.Length == 0) continue;
					var tokenWidth = MeasureTextWidth(drawText, font);
					if(x + tokenWidth > width + indent && x > indent && !string.IsNullOrWhiteSpace(drawText)){
						x = indent;
						curY += lineHeight;
					}
					var rect = new RectangleF(x, curY, tokenWidth + 1, lineHeight);
					if(run.Kind == InlineKind.Code){
						_ops.Add(DrawOp.Rect(rect.X - 1, rect.Y + 1, rect.Width + 2, rect.Height - 2, Color.FromArgb(245, 245, 245)));
						_ops.Add(DrawOp.StrokeRect(new RectangleF(rect.X - 1, rect.Y + 1, rect.Width + 2, rect.Height - 2), Color.FromArgb(225, 225, 225)));
						_ops.Add(DrawOp.Text(drawText, rect, font, color, run.Strike, TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine));
					}
					else{ _ops.Add(DrawOp.Text(drawText, rect, font, color, run.Strike)); }
					if(run.IsLink) _linkRegions.Add(new LinkRegion(rect, run.LinkUrl));
					x += tokenWidth;
				}
			}
			return curY + lineHeight + (quote ? 2 : 4);
		}
		private float MeasureTextWidth(string text, Font font){
			if(string.IsNullOrEmpty(text)) return 0f;
			using(var bmp = new Bitmap(1, 1))
			using(var g = Graphics.FromImage(bmp))
				return g.MeasureString(text, font, PointF.Empty, _stringFormat).Width;
		}
		private static IEnumerable<string> EnumerateDrawTokens(string text){
			if(string.IsNullOrEmpty(text)) yield break;
			var i = 0;
			while(i < text.Length){
				if(char.IsWhiteSpace(text[i])){
					var wsStart = i;
					while(i < text.Length && char.IsWhiteSpace(text[i])) i++;
					yield return text.Substring(wsStart, i - wsStart);
					continue;
				}
				var tokenStart = i;
				while(i < text.Length && !char.IsWhiteSpace(text[i])) i++;
				yield return text.Substring(tokenStart, i - tokenStart);
			}
		}
		private Font ResolveRunFont(Font paraFont, InlineRun run){
			var style = paraFont.Style;
			if(run.Bold) style |= FontStyle.Bold;
			if(run.Italic) style |= FontStyle.Italic;
			if(run.Kind == InlineKind.Code) return GetFont(FontStyle.Regular, paraFont.Size*0.95f, "Consolas");
			return GetFont(style, paraFont.Size, paraFont.FontFamily.Name);
		}
		private List<InlineRun> ParseInline(string text){
			var runs = new List<InlineRun>();
			var i = 0;
			while(i < text.Length){
				if(i + 1 < text.Length && text[i] == ' ' && text[i + 1] == ' '){
					runs.Add(InlineRun.Break());
					i += 2;
					continue;
				}
				var remain = text.Substring(i);
				var img = ImageRegex.Match(remain);
				if(img.Success){
					runs.Add(new InlineRun("[Image: " + img.Groups[1].Value + "]", false, false, false, false, InlineKind.Text, img.Groups[2].Value));
					i += img.Length;
					continue;
				}
				var link = LinkRegex.Match(remain);
				if(link.Success){
					runs.Add(new InlineRun(link.Groups[1].Value, false, false, false, true, InlineKind.Text, link.Groups[2].Value));
					i += link.Length;
					continue;
				}
				var url = UrlRegex.Match(remain);
				if(url.Success){
					runs.Add(new InlineRun(url.Groups[1].Value, false, false, false, true, InlineKind.Text, url.Groups[1].Value));
					i += url.Length;
					continue;
				}
				if(i + 1 < text.Length && text[i] == '*' && text[i + 1] == '*'){
					var end = text.IndexOf("**", i + 2, StringComparison.Ordinal);
					if(end > i){
						runs.Add(new InlineRun(text.Substring(i + 2, end - i - 2), true, false, false, false, InlineKind.Text, null));
						i = end + 2;
						continue;
					}
				}
				if(text[i] == '*' || text[i] == '_'){
					var marker = text[i];
					var end = text.IndexOf(marker, i + 1);
					if(end > i){
						runs.Add(new InlineRun(text.Substring(i + 1, end - i - 1), false, true, false, false, InlineKind.Text, null));
						i = end + 1;
						continue;
					}
				}
				if(i + 1 < text.Length && text[i] == '~' && text[i + 1] == '~'){
					var end = text.IndexOf("~~", i + 2, StringComparison.Ordinal);
					if(end > i){
						runs.Add(new InlineRun(text.Substring(i + 2, end - i - 2), false, false, true, false, InlineKind.Text, null));
						i = end + 2;
						continue;
					}
				}
				if(text[i] == '`'){
					var end = text.IndexOf('`', i + 1);
					if(end > i){
						runs.Add(new InlineRun(text.Substring(i + 1, end - i - 1), false, false, false, false, InlineKind.Code, null));
						i = end + 1;
						continue;
					}
				}
				var next = FindNextSpecial(text, i + 1);
				if(next <= i){
					runs.Add(new InlineRun(text.Substring(i, 1), false, false, false, false, InlineKind.Text, null));
					i++;
					continue;
				}
				runs.Add(new InlineRun(text.Substring(i, next - i), false, false, false, false, InlineKind.Text, null));
				i = next;
			}
			return runs;
		}
		private static int FindNextSpecial(string text, int start){
			for(var i = start; i < text.Length; i++){
				var ch = text[i];
				if(ch == '*' || ch == '_' || ch == '~' || ch == '`' || ch == '[' || ch == '!') return i;
				if((ch == 'h' || ch == 'H') && i + 7 < text.Length)
					if(text.IndexOf("http://", i, StringComparison.OrdinalIgnoreCase) == i || text.IndexOf("https://", i, StringComparison.OrdinalIgnoreCase) == i)
						return i;
			}
			return text.Length;
		}
		internal void Draw(Graphics g){
			foreach(var op in _ops) op.Draw(g);
		}
		internal string HitTestLink(Point point){
			foreach(var t in _linkRegions)
				if(t.Rect.Contains(point))
					return t.Url;
			return null;
		}
		private Font GetFont(FontStyle style, float? size = null, string family = null){
			if(_baseFont == null) _baseFont = Control.DefaultFont;
			var sz = size ?? _baseFont.Size;
			var ff = family ?? _baseFont.FontFamily.Name;
			var key = ff + "|" + sz.ToString("0.##") + "|" + (int)style;
			if(_fontCache.TryGetValue(key, out var font)) return font;
			font = new Font(ff, sz, style, GraphicsUnit.Point);
			_fontCache[key] = font;
			return font;
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
			private readonly Action<Graphics> _draw;
			private DrawOp(Action<Graphics> draw){_draw = draw;}
			internal static DrawOp Text(string text, RectangleF rect, Font font, Color color, bool strike = false, TextFormatFlags extraFlags = TextFormatFlags.Default){
				return new DrawOp(g => {
					TextRenderer.DrawText(g, text, font, Rectangle.Round(rect), color, TextFormatFlags.NoPadding | TextFormatFlags.NoClipping | extraFlags);
					if(strike)
						using(var pen = new Pen(color, 1f)){
							var y = rect.Y + rect.Height/2f;
							g.DrawLine(pen, rect.X, y, rect.Right, y);
						}
				});
			}
			internal static DrawOp Rect(float x, float y, float w, float h, Color color, bool fill = true){
				return new DrawOp(g => {
					if(fill)
						using(var b = new SolidBrush(color)){ g.FillRectangle(b, x, y, w, h); }
					else
						using(var pen = new Pen(color, 1f)){ g.DrawRectangle(pen, x, y, w, h); }
				});
			}
			internal static DrawOp StrokeRect(RectangleF rect, Color color){return Rect(rect.X, rect.Y, rect.Width, rect.Height, color, false);}
			internal static DrawOp Line(float x, float y, float width, Color color){
				return new DrawOp(g => {
					using(var pen = new Pen(color, 1f)){ g.DrawLine(pen, x, y, x + width, y); }
				});
			}
			internal void Draw(Graphics g){_draw(g);}
		}
	}
}