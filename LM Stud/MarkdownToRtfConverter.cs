using System;
using System.Text;
using System.Windows.Forms;
namespace LMStud{
	internal static class MarkdownToRtfConverter{
		internal static string ConvertMarkdownToRtf(string markdown){
			const string RtfHeader = @"{\rtf1\ansi\ansicpg1252\deff0\nouicompat{\fonttbl{\f0\fnil\fcharset0 Segoe UI;}}\viewkind4\uc1\pard\sa0\sl0\slmult1\f0\fs20 ";
			var rtf = new StringBuilder(RtfHeader);
			var lines = markdown.Replace("\r\n", "\n").Split('\n');
			var state = new MarkdownState();
			var startIndex = Array.FindIndex(lines, line => !string.IsNullOrWhiteSpace(line));
			if(startIndex == -1) return rtf.Append("}").ToString();
			for(var i = startIndex; i < lines.Length; i++) ProcessLine(lines[i], rtf, state);
			return rtf.Append("}").ToString();
		}
		private static void ProcessLine(string line, StringBuilder rtf, MarkdownState state){
			if(string.IsNullOrWhiteSpace(line)){
				rtf.Append(@"\line ");
				return;
			}
			if(line.TrimStart().StartsWith("```")){
				state.InCodeBlock = !state.InCodeBlock;
				return;
			}
			if(!state.FirstLine) rtf.Append(@"\line ");
			state.FirstLine = false;
			if(state.InCodeBlock){
				rtf.Append(EscapeRtf(line));
				return;
			}
			ProcessMarkdownLine(line, rtf);
		}
		private static void ProcessMarkdownLine(string line, StringBuilder rtf){
			var trimmedLine = line.TrimStart();
			var leadingWhitespace = line.Substring(0, line.Length - trimmedLine.Length);
			if(TryProcessHeading(trimmedLine, rtf) || TryProcessListItem(trimmedLine, leadingWhitespace, rtf) || TryProcessNumberedList(trimmedLine, rtf)) return;
			rtf.Append(ProcessInlineMarkdown(line));
		}
		private static bool TryProcessHeading(string trimmedLine, StringBuilder rtf){
			var headingStyles = new[]{
				new{ Prefix = "# ", Style = @"\b\ul\fs32 ", EndStyle = @"\b0\ulnone\fs20" },
				new{ Prefix = "## ", Style = @"\b\ul\fs28 ", EndStyle = @"\b0\ulnone\fs20" },
				new{ Prefix = "### ", Style = @"\b\fs24 ", EndStyle = @"\b0\fs20" },
				new{ Prefix = "#### ", Style = @"\b\fs20 ", EndStyle = @"\b0\fs20" },
				new{ Prefix = "##### ", Style = @"\b\fs18 ", EndStyle = @"\b0\fs20" },
				new{ Prefix = "###### ", Style = @"\b\fs16 ", EndStyle = @"\b0\fs20" }
			};
			foreach(var style in headingStyles){
				if(trimmedLine.StartsWith(style.Prefix)){
					var content = ProcessInlineMarkdown(trimmedLine.Substring(style.Prefix.Length));
					rtf.Append("{").Append(style.Style).Append(content).Append(style.EndStyle).Append("} ");
					return true;
				}
			}
			return false;
		}
		private static bool TryProcessListItem(string trimmedLine, string leadingWhitespace, StringBuilder rtf){
			if(trimmedLine.StartsWith("- ") || trimmedLine.StartsWith("* ")){
				var marker = trimmedLine[0];
				var content = ProcessInlineMarkdown(trimmedLine.Substring(2));
				rtf.Append(leadingWhitespace).Append(@"\bullet  ").Append(content);
				return true;
			}
			return false;
		}
		private static bool TryProcessNumberedList(string trimmedLine, StringBuilder rtf){
			if(trimmedLine.Length > 1 && char.IsDigit(trimmedLine[0])){
				var dotIndex = trimmedLine.IndexOf(". ", StringComparison.Ordinal);
				if(dotIndex > 0){
					var numberPart = trimmedLine.Substring(0, dotIndex + 1);
					var textPart = trimmedLine.Substring(dotIndex + 2);
					rtf.Append(EscapeRtf(numberPart)).Append(" ").Append(ProcessInlineMarkdown(textPart));
					return true;
				}
			}
			return false;
		}
		private static string ProcessInlineMarkdown(string text){
			var result = new StringBuilder();
			var pos = 0;
			while(pos < text.Length){
				bool IsValidEmphasisDelimiter(int position, char marker){
					var validLeft = position == 0 || char.IsWhiteSpace(text[position - 1]) || char.IsPunctuation(text[position - 1]);
					var validRight = position + 1 < text.Length && !char.IsWhiteSpace(text[position + 1]);
					return validLeft && validRight;
				}
				if(pos + 2 < text.Length && ((text[pos] == '*' && text[pos + 1] == '*' && text[pos + 2] == '*') || (text[pos] == '_' && text[pos + 1] == '_' && text[pos + 2] == '_')) &&
					IsValidEmphasisDelimiter(pos, text[pos])){
					var marker = text[pos];
					pos += 3;
					var endPos = text.IndexOf(marker + marker.ToString() + marker, pos, StringComparison.Ordinal);
					if(endPos != -1 && (endPos + 3 >= text.Length || char.IsWhiteSpace(text[endPos + 3]) || char.IsPunctuation(text[endPos + 3]))){
						result.Append(@"\b\i " + EscapeRtf(text.Substring(pos, endPos - pos)) + @"\b0\i0 ");
						pos = endPos + 3;
					} else{ result.Append(EscapeRtf(marker + marker.ToString() + marker)); }
				} else if(pos + 1 < text.Length && ((text[pos] == '*' && text[pos + 1] == '*') || (text[pos] == '_' && text[pos + 1] == '_')) && IsValidEmphasisDelimiter(pos, text[pos])){
					var marker = text[pos];
					pos += 2;
					var endPos = text.IndexOf(marker + marker.ToString(), pos, StringComparison.Ordinal);
					if(endPos != -1 && (endPos + 2 >= text.Length || char.IsWhiteSpace(text[endPos + 2]) || char.IsPunctuation(text[endPos + 2]))){
						result.Append(@"\b " + EscapeRtf(text.Substring(pos, endPos - pos)) + @"\b0 ");
						pos = endPos + 2;
					} else{ result.Append(EscapeRtf(marker + marker.ToString())); }
				} else if(pos < text.Length && (text[pos] == '*' || text[pos] == '_') && IsValidEmphasisDelimiter(pos, text[pos])){
					var marker = text[pos];
					pos++;
					var endPos = text.IndexOf(marker, pos);
					if(endPos != -1 && (endPos + 1 >= text.Length || char.IsWhiteSpace(text[endPos + 1]) || char.IsPunctuation(text[endPos + 1]))){
						result.Append(@"\i " + EscapeRtf(text.Substring(pos, endPos - pos)) + @"\i0 ");
						pos = endPos + 1;
					} else{ result.Append(EscapeRtf(marker.ToString())); }
				} else if(pos + 1 < text.Length && text[pos] == '~' && text[pos + 1] == '~' && IsValidEmphasisDelimiter(pos, '~')){
					pos += 2;
					var endPos = text.IndexOf("~~", pos, StringComparison.Ordinal);
					if(endPos != -1 && (endPos + 2 >= text.Length || char.IsWhiteSpace(text[endPos + 2]) || char.IsPunctuation(text[endPos + 2]))){
						result.Append(@"\strike " + EscapeRtf(text.Substring(pos, endPos - pos)) + @"\strike0 ");
						pos = endPos + 2;
					} else{ result.Append(EscapeRtf("~~")); }
				} else if(pos < text.Length && text[pos] == '`'){
					pos++;
					var endPos = text.IndexOf('`', pos);
					if(endPos != -1){
						result.Append(EscapeRtf(text.Substring(pos, endPos - pos)));
						pos = endPos + 1;
					} else{ result.Append(EscapeRtf("`")); }
				} else if(pos < text.Length && text[pos] == '[' && (pos == 0 || char.IsWhiteSpace(text[pos - 1]))){
					var closeBracket = text.IndexOf(']', pos);
					if(closeBracket > pos && closeBracket + 1 < text.Length && text[closeBracket + 1] == '('){
						var closeParenthesis = text.IndexOf(')', closeBracket + 2);
						if(closeParenthesis != -1){
							var linkText = text.Substring(pos + 1, closeBracket - pos - 1);
							result.Append(@"\ul " + EscapeRtf(linkText) + @"\ulnone ");
							pos = closeParenthesis + 1;
						} else{
							result.Append(EscapeRtf("["));
							pos++;
						}
					} else{
						result.Append(EscapeRtf("["));
						pos++;
					}
				} else{
					result.Append(EscapeRtf(text[pos].ToString()));
					pos++;
				}
			}
			return result.ToString();
		}
		private static string EscapeRtf(string text){
			if(string.IsNullOrEmpty(text)) return string.Empty;
			var sb = new StringBuilder();
			foreach(var c in text)
				switch(c){
					case '\\':
						sb.Append(@"\\");
						break;
					case '{':
						sb.Append(@"\{");
						break;
					case '}':
						sb.Append(@"\}");
						break;
					case '\t':
						sb.Append(@"\tab ");
						break;
					default:
						if(c > 127) sb.Append(@"\u" + (int)c + "?");
						else sb.Append(c);
						break;
				}
			return sb.ToString();
		}
		internal static void UpdateRichTextWithMarkdown(RichTextBox rtb, string markdownText){
			var originalSelectionStart = rtb.SelectionStart;
			var scrollToEnd = rtb.SelectionStart == rtb.TextLength;
			var rtfContent = ConvertMarkdownToRtf(markdownText);
			rtb.SuspendLayout();
			rtb.Rtf = rtfContent;
			if(scrollToEnd){
				NativeMethods.SendMessage(rtb.Handle, NativeMethods.EM_SETSEL, (IntPtr)rtb.Text.Length, (IntPtr)rtb.Text.Length);
				NativeMethods.SendMessage(rtb.Handle, NativeMethods.WM_VSCROLL, (IntPtr)NativeMethods.SB_BOTTOM, IntPtr.Zero);
			} else{ rtb.SelectionStart = Math.Min(originalSelectionStart, rtb.TextLength); }
			rtb.ResumeLayout();
		}
		private class MarkdownState{
			public bool InCodeBlock{get; set;}
			public bool FirstLine{get; set;} = true;
		}
	}
}