using System;
using System.Text;
using System.Windows.Forms;
namespace LMStud{
	internal static class MarkdownToRtfConverter{
		internal static string ConvertMarkdownToRtf(string markdown){
			var rtf = new StringBuilder();
			rtf.Append(@"{\rtf1\ansi\ansicpg1252\deff0\nouicompat{\fonttbl{\f0\fnil\fcharset0 Segoe UI;}}\viewkind4\uc1\pard\sa0\sl0\slmult1\f0\fs20 ");
			var inCodeBlock = false;
			var firstLine = true;
			var lines = markdown.Replace("\r\n", "\n").Split('\n');
			var i = 0;
			for(; i < lines.Length; )
				if(string.IsNullOrWhiteSpace(lines[i])) ++i;
				else break;
			for(; i < lines.Length; ++i){
				var line = lines[i];
				if(string.IsNullOrWhiteSpace(line)){
					rtf.Append("\\line ");
					continue;
				}
				if(!firstLine){
					rtf.Append("\\line ");
				} else firstLine = false;
				var trimmedLine = line.TrimStart();
				// Handle code blocks
				if(trimmedLine.StartsWith("```")){
					if(inCodeBlock){
						// End of code block
						//rtf.Append(@"\fs20 ");
						inCodeBlock = false;
					} else{
						// Start of code block
						//rtf.Append(@"\fs20 ");
						inCodeBlock = true;
					}
					continue;
				}
				if(inCodeBlock){
					// Inside code block - preserve formatting
					rtf.Append(EscapeRtf(line));
					continue;
				}
				var leadingWhitespace = line.Substring(0, line.Length - trimmedLine.Length);
				// Headers - maintain line breaks, but add formatting
				if(trimmedLine.StartsWith("# ")){
					rtf.Append("{\\b\\ul\\fs32 " + ProcessInlineMarkdown(line.Substring(line.IndexOf('#') + 2)) + "\\b0\\ulnone\\fs20} ");
				} else if(trimmedLine.StartsWith("## ")){
					rtf.Append("{\\b\\ul\\fs28 " + ProcessInlineMarkdown(line.Substring(line.IndexOf('#') + 3)) + "\\b0\\ulnone\\fs20} ");
				} else if(trimmedLine.StartsWith("### ")){
					rtf.Append("{\\b\\fs24 " + ProcessInlineMarkdown(line.Substring(line.IndexOf('#') + 4)) + "\\b0\\fs20} ");
				} else if(trimmedLine.StartsWith("#### ")){
					rtf.Append("{\\b\\fs20 " + ProcessInlineMarkdown(line.Substring(line.IndexOf('#') + 5)) + "\\b0\\fs20} ");
				} else if(trimmedLine.StartsWith("##### ")){
					rtf.Append("{\\b\\fs18 " + ProcessInlineMarkdown(line.Substring(line.IndexOf('#') + 6)) + "\\b0\\fs20} ");
				} else if(trimmedLine.StartsWith("###### ")){
					rtf.Append("{\\b\\fs16 " + ProcessInlineMarkdown(line.Substring(line.IndexOf('#') + 7)) + "\\b0\\fs20} ");
				}
				// Lists - using bullet character directly
				else if(trimmedLine.StartsWith("- ")){
					rtf.Append(leadingWhitespace + @"\bullet  " + ProcessInlineMarkdown(line.Substring(line.IndexOf('-') + 2)));
				} else if(trimmedLine.StartsWith("* ")){
					rtf.Append(leadingWhitespace + @"\bullet  " + ProcessInlineMarkdown(line.Substring(line.IndexOf('*') + 2)));
				}
				// Numbered lists
				else if(trimmedLine.Length > 1 && char.IsDigit(trimmedLine[0]) && trimmedLine.Contains(". ")){
					var dotIndex = line.IndexOf(". ", StringComparison.Ordinal);
					if(dotIndex > 0){
						var numberPart = line.Substring(0, dotIndex + 1);
						var textPart = line.Substring(dotIndex + 2);
						rtf.Append(EscapeRtf(numberPart) + " " + ProcessInlineMarkdown(textPart));
					} else{ rtf.Append(ProcessInlineMarkdown(line)); }
				}
				// Normal text
				else{ rtf.Append(ProcessInlineMarkdown(line)); }
			}

			// Close RTF document
			rtf.Append("}");
			return rtf.ToString();
		}

		// Process inline markdown elements
		private static string ProcessInlineMarkdown(string text){
			var result = new StringBuilder();
			var pos = 0;
			while(pos < text.Length){
				// Bold Italic - *** or ___
				if(pos + 2 < text.Length && (text[pos] == '*' && text[pos + 1] == '*' && text[pos + 2] == '*' || text[pos] == '_' && text[pos + 1] == '_' && text[pos + 2] == '_')){
					var marker = text[pos];
					pos += 3;// Skip markers
					var endPos = text.IndexOf(marker + marker.ToString() + marker, pos, StringComparison.Ordinal);
					if(endPos != -1){
						result.Append(@"\b\i " + EscapeRtf(text.Substring(pos, endPos - pos)) + @"\b0\i0 ");
						pos = endPos + 3;// Skip closing markers
					} else{
						// No closing marker
						result.Append(EscapeRtf(marker + marker.ToString() + marker));
						pos += 3;
					}
				}
				// Bold - ** or __
				else if(pos + 1 < text.Length && (text[pos] == '*' && text[pos + 1] == '*' || text[pos] == '_' && text[pos + 1] == '_')){
					var marker = text[pos];
					pos += 2;// Skip markers
					var endPos = text.IndexOf(marker + marker.ToString(), pos, StringComparison.Ordinal);
					if(endPos != -1){
						result.Append(@"\b " + EscapeRtf(text.Substring(pos, endPos - pos)) + @"\b0 ");
						pos = endPos + 2;// Skip closing markers
					} else{
						// No closing marker
						result.Append(EscapeRtf(marker + marker.ToString()));
						pos += 2;
					}
				}
				// Italic - * or _
				else if(pos < text.Length && (text[pos] == '*' || text[pos] == '_')){
					var marker = text[pos];
					pos++;// Skip marker
					var endPos = text.IndexOf(marker, pos);
					if(endPos != -1){
						result.Append(@"\i " + EscapeRtf(text.Substring(pos, endPos - pos)) + @"\i0 ");
						pos = endPos + 1;// Skip closing marker
					} else{
						// No closing marker
						result.Append(EscapeRtf(marker.ToString()));
						pos++;
					}
				}
				// Strikeout - ~~
				else if(pos + 1 < text.Length && text[pos] == '~' && text[pos + 1] == '~'){
					pos += 2;// Skip strikeout markers
					var endPos = text.IndexOf("~~", pos, StringComparison.Ordinal);
					if(endPos != -1){
						result.Append(@"\strike " + EscapeRtf(text.Substring(pos, endPos - pos)) + @"\strike0 ");
						pos = endPos + 2;// Skip closing markers
					} else{
						// No closing marker
						result.Append(EscapeRtf("~~"));
						pos += 2;
					}
				}
				// Inline code
				else if(pos < text.Length && text[pos] == '`'){
					pos++;// Skip backtick
					var endPos = text.IndexOf('`', pos);
					if(endPos != -1){
						result.Append(EscapeRtf(text.Substring(pos, endPos - pos)));
						pos = endPos + 1;
					} else{
						// No closing backtick
						result.Append(EscapeRtf("`"));
						pos++;
					}
				}
				// Links [text](url)
				else if(pos < text.Length && text[pos] == '[' && (pos == 0 || text[pos - 1] == ' ')){
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
					// Regular character
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
						//if(c > 127) sb.Append(@"\u" + (int)c + "?");
						//else
						sb.Append(c);
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
	}
}