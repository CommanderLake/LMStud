using System;
using System.Collections;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Windows.Forms;
using LMStud;
using Microsoft.VisualStudio.TestTools.UnitTesting;
namespace LM_Stud.Tests{
	[TestClass]
	public class MarkdownRenderTests{
		[TestMethod]
		public void RenderControl_IsDesignerCreatableWithoutSerializingRuntimeContent(){
			var controlType = typeof(MarkdownRenderControl);
			Assert.IsTrue(controlType.IsPublic);
			Assert.IsNotNull(controlType.GetConstructor(Type.EmptyTypes));
			var markdownText = TypeDescriptor.GetProperties(controlType)["MarkdownText"];
			Assert.IsFalse(markdownText.IsBrowsable);
			Assert.AreEqual(DesignerSerializationVisibility.Hidden,
				markdownText.Attributes[typeof(DesignerSerializationVisibilityAttribute)] is DesignerSerializationVisibilityAttribute attribute
					? attribute.Visibility
					: DesignerSerializationVisibility.Visible);
			Assert.IsNotNull(TypeDescriptor.GetEvents(controlType)["ContentHeightChanged"]);
		}
		[TestMethod]
		public void Renderer_HandlesCommonGfmBlocksAndInlineStyles(){
			const string markdown = "# Heading\n\n- [x] task\n- item with **bold**, *italic*, ~~strike~~, and `code`\n\n" +
				"| Name | Value |\n| --- | ---: |\n| A | 1 |\n\n> quote\n\n```csharp\nvar value = 1;\n```";
			using(var renderer = new GdiMarkdownRenderer()){
				renderer.Layout(markdown, Control.DefaultFont, 500, Color.Black, Color.White);
				Assert.IsTrue(renderer.TotalHeight > 0);
				StringAssert.Contains(renderer.PlainText, "Heading");
				StringAssert.Contains(renderer.PlainText, "[x] task");
				StringAssert.Contains(renderer.PlainText, "bold");
				StringAssert.Contains(renderer.PlainText, "Name");
				StringAssert.Contains(renderer.PlainText, "var value = 1;");
			}
		}
		[TestMethod]
		public void Renderer_HandlesInlineStylesInsideTables(){
			const string markdown = "| Style | Link |\n| --- | --- |\n| **bold** *italic* ~~strike~~ `code` | [site](https://example.com) |";
			using(var renderer = new GdiMarkdownRenderer()){
				renderer.Layout(markdown, Control.DefaultFont, 500, Color.Black, Color.White);
				Assert.AreEqual("Style\tLink\nbold italic strike code\tsite", renderer.PlainText);
				var opsField = typeof(GdiMarkdownRenderer).GetField("_ops", BindingFlags.Instance | BindingFlags.NonPublic);
				var drawOps = ((IEnumerable)opsField.GetValue(renderer)).Cast<object>().ToList();
				object FindTextOp(string value){
					return drawOps.Single(op => (string)op.GetType().GetField("_text", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(op) == value);
				}
				Font GetFont(object op){return (Font)op.GetType().GetField("_font", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(op);}
				var bold = FindTextOp("bold");
				var italic = FindTextOp("italic");
				var strike = FindTextOp("strike");
				var code = FindTextOp("code");
				var site = FindTextOp("site");
				Assert.IsTrue(GetFont(bold).Bold);
				Assert.IsTrue(GetFont(italic).Italic);
				Assert.IsTrue((bool)strike.GetType().GetField("_strike", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(strike));
				Assert.AreEqual("Consolas", GetFont(code).FontFamily.Name);
				var siteRect = (RectangleF)site.GetType().GetField("Rect", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public).GetValue(site);
				Assert.AreEqual("https://example.com", renderer.HitTestLink(Point.Round(new PointF(siteRect.Left + 1, siteRect.Top + 1))));
			}
		}
		[TestMethod]
		public void Renderer_WrappedInlineTableCellsIncreaseRowHeight(){
			const string markdown = "| Styled |\n| --- |\n| **bold words that must wrap across several table lines** |\n| next row |";
			using(var narrow = new GdiMarkdownRenderer())
			using(var wide = new GdiMarkdownRenderer()){
				narrow.Layout(markdown, Control.DefaultFont, 120, Color.Black, Color.White);
				wide.Layout(markdown, Control.DefaultFont, 600, Color.Black, Color.White);
				Assert.IsTrue(narrow.TotalHeight > wide.TotalHeight, "Styled table wrapping should increase the containing row height.");
				Assert.AreEqual("Styled\nbold words that must wrap across several table lines\nnext row", narrow.PlainText);
			}
		}
		[TestMethod]
		public void Renderer_TreatsSeparatedDashesAsHorizontalRule(){
			using(var renderer = new GdiMarkdownRenderer()){
				renderer.Layout("Paragraph\n\n---", Control.DefaultFont, 500, Color.Black, Color.White);
				Assert.AreEqual("Paragraph\n---", renderer.PlainText);
			}
		}
		[TestMethod]
		public void Renderer_TreatsAdjacentDashesAsSetextHeading(){
			using(var renderer = new GdiMarkdownRenderer()){
				renderer.Layout("Heading\n---", Control.DefaultFont, 500, Color.Black, Color.White);
				Assert.AreEqual("Heading", renderer.PlainText);
			}
		}
		[TestMethod]
		public void RenderControl_SelectAllSelectsRenderedText(){
			using(var control = new MarkdownRenderControl{ Size = new Size(500, 100) }){
				control.MarkdownText = "**Bold** and selectable";
				var onKeyDown = typeof(MarkdownRenderControl).GetMethod("OnKeyDown", BindingFlags.Instance | BindingFlags.NonPublic);
				onKeyDown.Invoke(control, new object[]{ new KeyEventArgs(Keys.Control | Keys.A) });
				Assert.AreEqual(control.PlainText.Length, control.SelectionLength);
				Assert.AreEqual("Bold and selectable", control.PlainText);
			}
		}
		[TestMethod]
		public void RenderControl_KeyboardNavigationMovesAndExtendsSelection(){
			using(var control = new MarkdownRenderControl{ Size = new Size(500, 100), RenderMarkdown = false }){
				control.MarkdownText = "one two\nthree four";
				SendKey(control, Keys.Right);
				Assert.AreEqual(1, control.CaretPosition);
				SendKey(control, Keys.Control | Keys.Right);
				Assert.AreEqual(4, control.CaretPosition);
				SendKey(control, Keys.Control | Keys.Shift | Keys.Right);
				Assert.AreEqual(8, control.CaretPosition);
				Assert.AreEqual(4, control.SelectionLength);
				SendKey(control, Keys.Home);
				Assert.AreEqual(8, control.CaretPosition, "Home should move to the beginning of the current visual line.");
				Assert.AreEqual(0, control.SelectionLength);
				SendKey(control, Keys.Control | Keys.Home);
				Assert.AreEqual(0, control.CaretPosition);
				SendKey(control, Keys.End);
				Assert.AreEqual(7, control.CaretPosition);
				SendKey(control, Keys.Down);
				Assert.IsTrue(control.CaretPosition > 7);
				SendKey(control, Keys.Control | Keys.End);
				Assert.AreEqual(control.PlainText.Length, control.CaretPosition);
				SendKey(control, Keys.Control | Keys.Up);
				Assert.AreEqual(8, control.CaretPosition);
			}
		}
		[TestMethod]
		public void RenderControl_PageKeysMoveCaretAcrossVisualLines(){
			using(var control = new MarkdownRenderControl{ Size = new Size(180, 80), RenderMarkdown = false }){
				control.MarkdownText = string.Join("\n", Enumerable.Range(0, 30).Select(index => "Line " + index));
				SendKey(control, Keys.PageDown);
				Assert.IsTrue(control.CaretPosition > 0);
				var pageDownPosition = control.CaretPosition;
				SendKey(control, Keys.Shift | Keys.PageUp);
				Assert.IsTrue(control.CaretPosition < pageDownPosition);
				Assert.IsTrue(control.SelectionLength > 0);
				var isInputKey = typeof(MarkdownRenderControl).GetMethod("IsInputKey", BindingFlags.Instance | BindingFlags.NonPublic);
				Assert.IsTrue((bool)isInputKey.Invoke(control, new object[]{ Keys.PageDown }));
			}
		}
		[TestMethod]
		public void Renderer_CaretOffsetsUseSameMetricsAsPaintedText(){
			using(var renderer = new GdiMarkdownRenderer())
			using(var bitmap = new Bitmap(200, 50))
			using(var graphics = Graphics.FromImage(bitmap)){
				const string text = "Wide letters";
				renderer.Layout(text, Control.DefaultFont, 500, Color.Black, Color.White, renderMarkdown: false);
				for(var i = 1; i <= text.Length; i++){
					var expected = TextRenderer.MeasureText(graphics, text.Substring(0, i), Control.DefaultFont,
						new Size(int.MaxValue, int.MaxValue),
						TextFormatFlags.NoPadding | TextFormatFlags.NoClipping | TextFormatFlags.SingleLine).Width;
					Assert.AreEqual(expected, renderer.GetCaretRectangle(i).Left, 0.1, "Caret drifted at character " + i + ".");
				}
			}
		}
		[TestMethod]
		public void Renderer_SelectedHeaderSpaceDoesNotErasePreviousGlyphOverhang(){
			using(var renderer = new GdiMarkdownRenderer())
			using(var font = new Font("Times New Roman", 18f))
			using(var unselected = new Bitmap(240, 80))
			using(var selected = new Bitmap(240, 80))
			using(var unselectedGraphics = Graphics.FromImage(unselected))
			using(var selectedGraphics = Graphics.FromImage(selected)){
				renderer.Layout("# *f* next", font, 220, Color.Black, Color.White);
				unselectedGraphics.Clear(Color.White);
				selectedGraphics.Clear(Color.White);
				renderer.Draw(unselectedGraphics, 0, 0, SystemColors.Highlight, SystemColors.HighlightText);
				renderer.Draw(selectedGraphics, 1, 1, SystemColors.Highlight, SystemColors.HighlightText);
				var spaceLeft = (int)Math.Floor(renderer.GetCaretRectangle(1).Left);
				var spaceRight = (int)Math.Ceiling(renderer.GetCaretRectangle(2).Left);
				var overhangPixels = 0;
				var preservedPixels = 0;
				for(var x = spaceLeft; x < spaceRight; x++)
					for(var y = 0; y < unselected.Height; y++)
						if(IsDark(unselected.GetPixel(x, y))){
							overhangPixels++;
							if(IsDark(selected.GetPixel(x, y))) preservedPixels++;
						}
				Assert.IsTrue(overhangPixels > 0, "The test font should have visible glyph overhang into the following space.");
				Assert.AreEqual(overhangPixels, preservedPixels, "Selecting the space erased part of the preceding header glyph.");
			}
		}
		[TestMethod]
		public void RenderControl_EmojiHeaderUsesGraphemeCaretStopsAndVisibleSpaces(){
			const string markdown = "## 🛠️ Renderer Compatibility Notes";
			using(var control = new MarkdownRenderControl{ Size = new Size(600, 100) }){
				control.MarkdownText = markdown;
				Assert.AreEqual("🛠️ Renderer Compatibility Notes", control.PlainText);
				SendKey(control, Keys.Right);
				Assert.AreEqual(3, control.CaretPosition, "The emoji and variation selector should be one caret step.");
				SendKey(control, Keys.Right);
				Assert.AreEqual(4, control.CaretPosition, "The real space after the emoji should be a separate caret step.");
			}
			using(var renderer = new GdiMarkdownRenderer()){
				renderer.Layout(markdown, Control.DefaultFont, 600, Color.Black, Color.White);
				var emojiEnd = renderer.GetCaretRectangle(3).Left;
				var spaceEnd = renderer.GetCaretRectangle(4).Left;
				Assert.IsTrue(emojiEnd > renderer.GetCaretRectangle(0).Left);
				Assert.IsTrue(spaceEnd > emojiEnd, "The space after the emoji must retain visible width.");
			}
		}
		[TestMethod]
		public void Renderer_PinHeaderMaintainsPositiveWordSpacingAndSelectionExtent(){
			const string markdown = "## 📌 Headings (Hierarchical Structure)";
			using(var renderer = new GdiMarkdownRenderer())
			using(var bitmap = new Bitmap(600, 80))
			using(var graphics = Graphics.FromImage(bitmap)){
				renderer.Layout(markdown, Control.DefaultFont, 580, Color.Black, Color.White);
				Assert.AreEqual("📌 Headings (Hierarchical Structure)", renderer.PlainText);
				var spaceStart = renderer.PlainText.IndexOf(" Structure", StringComparison.Ordinal);
				var beforeSpace = renderer.GetCaretRectangle(spaceStart).Left;
				var afterSpace = renderer.GetCaretRectangle(spaceStart + 1).Left;
				Assert.IsTrue(afterSpace > beforeSpace, "The space before Structure must have positive width.");
				graphics.Clear(Color.White);
				renderer.Draw(graphics, 0, 0, SystemColors.Highlight, SystemColors.HighlightText);
				var rightmostInk = -1;
				for(var x = 0; x < bitmap.Width; x++)
					for(var y = 0; y < bitmap.Height; y++)
						if(IsDark(bitmap.GetPixel(x, y)))
							rightmostInk = Math.Max(rightmostInk, x);
				var selectionEnd = renderer.GetCaretRectangle(renderer.PlainText.Length).Left;
				Assert.IsTrue(rightmostInk <= Math.Ceiling(selectionEnd) + 1,
					"Painted heading text extends beyond its selection and caret metrics.");
			}
		}
		[TestMethod]
		public void RenderControl_HeightOnlyResizeDoesNotRelayout(){
			using(var control = new MarkdownRenderControl{ Size = new Size(500, 100) }){
				control.MarkdownText = "A message that should not be laid out again for a height-only resize.";
				var layoutCount = control.LayoutCount;
				control.Height += 50;
				Assert.AreEqual(layoutCount, control.LayoutCount);
			}
		}
		[TestMethod]
		public void RenderControl_SetContentBatchesTextAndModeIntoOneLayout(){
			using(var control = new MarkdownRenderControl{ Size = new Size(500, 100) }){
				var layoutCount = control.LayoutCount;
				control.SetContent("**batched** content", false);
				Assert.AreEqual(layoutCount + 1, control.LayoutCount);
				Assert.AreEqual("**batched** content", control.PlainText);
			}
		}
		[TestMethod]
		public void RenderControl_SelectionStartsAtTheRenderedContentOrigin(){
			using(var control = new MarkdownRenderControl{ Size = new Size(300, 60), BackColor = Color.White, ForeColor = Color.Black })
			using(var bitmap = new Bitmap(300, 60)){
				control.MarkdownText = "origin alignment";
				SendKey(control, Keys.Shift | Keys.End);
				control.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size));
				Assert.AreEqual(SystemColors.Highlight.ToArgb(), bitmap.GetPixel(4, 4).ToArgb());
				Assert.AreEqual(Color.White.ToArgb(), bitmap.GetPixel(3, 4).ToArgb());
			}
		}
		[TestMethod]
		public void Renderer_LayoutsLongUnbrokenTextWithoutQuadraticDelay(){
			using(var renderer = new GdiMarkdownRenderer()){
				var text = new string('W', 6000);
				var stopwatch = Stopwatch.StartNew();
				renderer.Layout(text, Control.DefaultFont, 500, Color.Black, Color.White);
				stopwatch.Stop();
				Assert.IsTrue(stopwatch.ElapsedMilliseconds < 2000,
					"Long-run layout took " + stopwatch.ElapsedMilliseconds + " ms.");
			}
		}
		[TestMethod]
		public void Renderer_StreamingAppendReusesExistingTextMeasurements(){
			using(var renderer = new GdiMarkdownRenderer()){
				var text = string.Join(" ", Enumerable.Range(0, 80).Select(index => "word" + index));
				renderer.Layout(text, Control.DefaultFont, 600, Color.Black, Color.White);
				var initialMeasurements = renderer.TextRendererMeasurementCount;
				renderer.Layout(text + " appended", Control.DefaultFont, 600, Color.Black, Color.White);
				var appendedMeasurements = renderer.TextRendererMeasurementCount - initialMeasurements;
				Assert.IsTrue(initialMeasurements > 100, "The initial layout should exercise the exact text measurement path.");
				Assert.IsTrue(appendedMeasurements < 20,
					"A streaming append repeated " + appendedMeasurements + " TextRenderer measurements instead of reusing cached token metrics.");
			}
		}
		[TestMethod]
		public void Chunker_SplitsVeryLongFencedContentIntoRenderableSegments(){
			var markdown = new StringBuilder("```text\n");
			for(var i = 0; i < 2500; i++) markdown.Append("line ").Append(i).Append('\n');
			markdown.Append("```");
			var chunks = MarkdownMessageChunker.Split(markdown.ToString());
			Assert.IsTrue(chunks.Count > 1, "Long messages should be split before a WinForms child becomes excessively tall.");
			foreach(var chunk in chunks)
				using(var renderer = new GdiMarkdownRenderer()){
					renderer.Layout(chunk, Control.DefaultFont, 500, Color.Black, Color.White);
					Assert.IsTrue(renderer.TotalHeight > 0);
				}
		}
		[TestMethod]
		public void MarkdownImages_IgnoresImagesInsideCodeFences(){
			const string markdown = "![real](https://example.com/real.png)\n```\n![code](https://example.com/code.png)\n```";
			var images = MarkdownImages.Find(markdown);
			Assert.AreEqual(1, images.Count);
			Assert.AreEqual("real", images[0].AltText);
		}
		[TestMethod]
		public void VisionImageMarkdown_BuildsResponsesAndChatContentParts(){
			var path = Path.Combine(Path.GetTempPath(), "lmstud-vision-" + Guid.NewGuid().ToString("N") + ".png");
			try{
				using(var bitmap = new Bitmap(2, 2)) bitmap.Save(path);
				var markdown = "Describe this image.\n\n![sample](<" + new Uri(path).AbsoluteUri + ">)";
				var payload = APIClient.BuildInputMessagePayload(new APIClient.ChatMessage("user", markdown));
				var content = payload["content"];
				Assert.IsTrue(content.IsArray);
				Assert.AreEqual("input_text", content[0].GetString("type"));
				Assert.AreEqual("input_image", content[1].GetString("type"));
				StringAssert.StartsWith(content[1].GetString("image_url"), "data:image/png;base64,");
				var history = Json.ArrayBuilder();
				history.Add(payload);
				var chatMessages = APIClient.ConvertHistoryToChatCompletionMessages(history);
				var chatContent = chatMessages.Single()["content"];
				Assert.AreEqual("text", chatContent[0].GetString("type"));
				Assert.AreEqual("image_url", chatContent[1].GetString("type"));
				StringAssert.StartsWith(chatContent[1]["image_url"].GetString("url"), "data:image/png;base64,");
				var assistantPayload = APIClient.BuildInputMessagePayload(new APIClient.ChatMessage("assistant", markdown));
				Assert.IsTrue(assistantPayload["content"].IsString, "Only explicit user attachments should become image inputs.");
			} finally{
				if(File.Exists(path)) File.Delete(path);
			}
		}
		[TestMethod]
		public void Chunker_BoundsImageHeavyChunksBelowNativeControlHeightLimit(){
			var markdown = string.Join("\n", Enumerable.Repeat("![image](memory:image)", 100));
			var chunks = MarkdownMessageChunker.Split(markdown);
			Assert.AreEqual(2, chunks.Count, "Image-heavy messages should use nearly the full safe control height.");
			using(var image = new Bitmap(1, 1000))
			foreach(var chunk in chunks)
				using(var renderer = new GdiMarkdownRenderer()){
					renderer.Layout(chunk, Control.DefaultFont, 500, Color.Black, Color.White, source => image);
					Assert.IsTrue(renderer.TotalHeight + 8 <= MarkdownMessageChunker.MaximumRenderedContentHeight,
						"Rendered chunk height was " + renderer.TotalHeight + ", which risks overflowing a native WinForms child window.");
				}
		}
		[TestMethod]
		public void Chunker_KeepsOrdinaryLongTextTogetherUntilRenderedHeightRequiresSplit(){
			var markdown = string.Join("\n", Enumerable.Range(0, 700).Select(index => "line " + index));
			var chunks = MarkdownMessageChunker.Split(markdown);
			Assert.AreEqual(1, chunks.Count, "Source line counts alone should not split a message that fits within the safe rendered height.");
		}
		private static void SendKey(MarkdownRenderControl control, Keys keys){
			var onKeyDown = typeof(MarkdownRenderControl).GetMethod("OnKeyDown", BindingFlags.Instance | BindingFlags.NonPublic);
			onKeyDown.Invoke(control, new object[]{ new KeyEventArgs(keys) });
		}
		private static bool IsDark(Color color){return color.R < 80 && color.G < 80 && color.B < 80;}
	}
}
