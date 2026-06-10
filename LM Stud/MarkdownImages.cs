using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Forms;
namespace LMStud{
	internal sealed class MarkdownImageReference{
		internal readonly string AltText;
		internal readonly int Length;
		internal readonly string Source;
		internal readonly int Start;
		internal MarkdownImageReference(string altText, string source, int start, int length){
			AltText = altText ?? "";
			Source = source ?? "";
			Start = start;
			Length = length;
		}
	}
	internal static class MarkdownImages{
		private const int MaximumImageBytes = 20*1024*1024;
		private const long MaximumResizeSourceBytes = 256L*1024*1024;
		private const int MaximumVisionDimension = 4096;
		private static readonly HashSet<string> ImageExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase){
			".bmp", ".gif", ".jpeg", ".jpg",
			".png", ".tif", ".tiff", ".webp"
		};
		internal static List<MarkdownImageReference> Find(string markdown){
			var images = new List<MarkdownImageReference>();
			if(string.IsNullOrEmpty(markdown)) return images;
			var fenceMarker = '\0';
			var fenceLength = 0;
			var lineStart = 0;
			while(lineStart <= markdown.Length){
				var lineEnd = markdown.IndexOf('\n', lineStart);
				if(lineEnd < 0) lineEnd = markdown.Length;
				var line = markdown.Substring(lineStart, lineEnd - lineStart).TrimEnd('\r');
				var trimmed = line.TrimStart();
				if(TryReadFence(trimmed, out var marker, out var markerLength)){
					if(fenceMarker == '\0'){
						fenceMarker = marker;
						fenceLength = markerLength;
					} else if(marker == fenceMarker && markerLength >= fenceLength){
						fenceMarker = '\0';
						fenceLength = 0;
					}
				} else if(fenceMarker == '\0'){ FindInLine(markdown, lineStart, lineEnd, images); }
				if(lineEnd == markdown.Length) break;
				lineStart = lineEnd + 1;
			}
			return images;
		}
		private static void FindInLine(string markdown, int lineStart, int lineEnd, List<MarkdownImageReference> images){
			var inCode = false;
			var codeTicks = 0;
			for(var index = lineStart; index < lineEnd; index++){
				if(markdown[index] == '`'){
					var ticks = 1;
					while(index + ticks < lineEnd && markdown[index + ticks] == '`') ticks++;
					if(!inCode){
						inCode = true;
						codeTicks = ticks;
					} else if(ticks == codeTicks){
						inCode = false;
						codeTicks = 0;
					}
					index += ticks - 1;
					continue;
				}
				if(inCode || markdown[index] != '!' || index + 1 >= lineEnd || markdown[index + 1] != '[' || IsEscaped(markdown, index)) continue;
				if(!TryParseAt(markdown, index, lineEnd, out var image)) continue;
				images.Add(image);
				index += image.Length - 1;
			}
		}
		internal static bool TryParseStandalone(string line, out MarkdownImageReference image){
			image = null;
			if(string.IsNullOrWhiteSpace(line)) return false;
			var leading = line.Length - line.TrimStart().Length;
			if(!TryParseAt(line, leading, line.Length, out var parsed)) return false;
			if(!string.IsNullOrWhiteSpace(line.Substring(leading + parsed.Length))) return false;
			image = parsed;
			return true;
		}
		private static bool TryParseAt(string markdown, int start, int limit, out MarkdownImageReference image){
			image = null;
			var altStart = start + 2;
			var altEnd = FindUnescaped(markdown, ']', altStart, limit);
			if(altEnd < 0 || altEnd + 1 >= limit || markdown[altEnd + 1] != '(') return false;
			var sourceStart = altEnd + 2;
			var sourceEnd = FindClosingParenthesis(markdown, sourceStart, limit);
			if(sourceEnd < 0) return false;
			var source = markdown.Substring(sourceStart, sourceEnd - sourceStart).Trim();
			var titleSeparator = FindTitleSeparator(source);
			if(titleSeparator >= 0) source = source.Substring(0, titleSeparator).Trim();
			if(source.Length >= 2 && source[0] == '<' && source[source.Length - 1] == '>') source = source.Substring(1, source.Length - 2);
			image = new MarkdownImageReference(Unescape(markdown.Substring(altStart, altEnd - altStart)), Unescape(source), start, sourceEnd - start + 1);
			return true;
		}
		private static int FindTitleSeparator(string source){
			var angle = false;
			for(var i = 0; i < source.Length; i++)
				if(source[i] == '<') angle = true;
				else if(source[i] == '>') angle = false;
				else if(!angle && char.IsWhiteSpace(source[i])) return i;
			return -1;
		}
		private static int FindUnescaped(string text, char value, int start, int limit){
			for(var i = start; i < limit; i++)
				if(text[i] == value && !IsEscaped(text, i))
					return i;
			return -1;
		}
		private static int FindClosingParenthesis(string text, int start, int limit){
			var depth = 0;
			var angle = false;
			for(var i = start; i < limit; i++){
				if(IsEscaped(text, i)) continue;
				if(text[i] == '<'){ angle = true; } else if(text[i] == '>'){ angle = false; } else if(!angle && text[i] == '('){ depth++; } else if(!angle && text[i] == ')'){
					if(depth == 0) return i;
					depth--;
				}
			}
			return -1;
		}
		private static bool IsEscaped(string text, int index){
			var slashes = 0;
			for(var i = index - 1; i >= 0 && text[i] == '\\'; i--) slashes++;
			return slashes%2 != 0;
		}
		private static string Unescape(string value){return (value ?? "").Replace("\\]", "]").Replace("\\)", ")").Replace("\\(", "(").Replace("\\\\", "\\");}
		private static bool TryReadFence(string line, out char marker, out int length){
			marker = '\0';
			length = 0;
			if(string.IsNullOrEmpty(line) || (line[0] != '`' && line[0] != '~')) return false;
			marker = line[0];
			while(length < line.Length && line[length] == marker) length++;
			return length >= 3;
		}
		internal static bool IsImageFile(string path){return !string.IsNullOrWhiteSpace(path) && ImageExtensions.Contains(Path.GetExtension(path));}
		internal static string AddFileReference(TextBoxBase textBox, string path, string altText = null){
			if(textBox == null) throw new ArgumentNullException(nameof(textBox));
			var fullPath = Path.GetFullPath(path);
			var source = new Uri(fullPath).AbsoluteUri;
			var alt = EscapeAlt(string.IsNullOrWhiteSpace(altText) ? Path.GetFileName(fullPath) : altText);
			var markdown = "![" + alt + "](<" + source + ">)";
			var prefix = textBox.TextLength == 0 || textBox.Text.EndsWith("\r\n", StringComparison.Ordinal) ? "" : "\r\n";
			textBox.SelectedText = prefix + markdown + "\r\n";
			return markdown;
		}
		internal static string SaveClipboardImage(Image image){
			if(image == null) throw new ArgumentNullException(nameof(image));
			var directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LM Stud", "Attachments");
			Directory.CreateDirectory(directory);
			var path = Path.Combine(directory, "clipboard-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff") + "-" + Guid.NewGuid().ToString("N").Substring(0, 8) + ".png");
			image.Save(path, ImageFormat.Png);
			return path;
		}
		private static string EscapeAlt(string value){return (value ?? "").Replace("\\", "\\\\").Replace("]", "\\]");}
		internal static bool TryGetVisionUrl(string source, out string visionUrl){return TryGetVisionUrl(source, true, out visionUrl);}
		private static bool TryGetVisionUrl(string source, bool allowRemote, out string visionUrl){
			visionUrl = null;
			if(string.IsNullOrWhiteSpace(source)) return false;
			source = source.Trim();
			if(source.StartsWith("data:image/", StringComparison.OrdinalIgnoreCase)){
				if(source.Length > MaximumImageBytes*2) return false;
				visionUrl = source;
				return true;
			}
			if(Uri.TryCreate(source, UriKind.Absolute, out var uri)){
				if(uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps){
					if(!allowRemote) return false;
					visionUrl = uri.ToString();
					return true;
				}
				if(uri.Scheme == Uri.UriSchemeFile) source = uri.LocalPath;
				else return false;
			}
			try{
				var path = Path.GetFullPath(source);
				if(!File.Exists(path)) return false;
				var file = new FileInfo(path);
				if(file.Length <= 0) return false;
				byte[] bytes;
				var mimeType = GetMimeType(path);
				if(file.Length <= MaximumImageBytes) bytes = File.ReadAllBytes(path);
				else{
					if(!TryDownscaleImage(path, file.Length, out bytes)) return false;
					mimeType = "image/jpeg";
				}
				visionUrl = "data:" + mimeType + ";base64," + Convert.ToBase64String(bytes);
				return true;
			} catch{ return false; }
		}
		internal static bool TryReadLocalOrDataBytes(string source, out byte[] bytes){
			bytes = null;
			if(string.IsNullOrWhiteSpace(source)) return false;
			try{
				if(source.StartsWith("data:image/", StringComparison.OrdinalIgnoreCase)){
					var comma = source.IndexOf(',');
					if(comma < 0 || source.IndexOf(";base64", StringComparison.OrdinalIgnoreCase) < 0) return false;
					bytes = Convert.FromBase64String(source.Substring(comma + 1));
					return bytes.Length <= MaximumImageBytes;
				}
				if(Uri.TryCreate(source, UriKind.Absolute, out var uri)){
					if(uri.Scheme != Uri.UriSchemeFile) return false;
					source = uri.LocalPath;
				}
				var path = Path.GetFullPath(source);
				if(!File.Exists(path)) return false;
				var file = new FileInfo(path);
				if(file.Length <= 0) return false;
				if(file.Length <= MaximumImageBytes) bytes = File.ReadAllBytes(path);
				else if(!TryDownscaleImage(path, file.Length, out bytes)) return false;
				return true;
			} catch{ return false; }
		}
		private static bool TryDownscaleImage(string path, long sourceLength, out byte[] bytes){
			bytes = null;
			if(sourceLength <= MaximumImageBytes || sourceLength > MaximumResizeSourceBytes) return false;
			try{
				using(var source = Image.FromFile(path)){
					var largestDimension = Math.Max(source.Width, source.Height);
					if(largestDimension <= 0) return false;
					var dimensionScale = Math.Min(1.0, MaximumVisionDimension/(double)largestDimension);
					var byteScale = Math.Sqrt((MaximumImageBytes*0.75)/sourceLength);
					var scale = Math.Min(0.95, Math.Min(dimensionScale, byteScale));
					var quality = 90L;
					for(var attempt = 0; attempt < 6; attempt++){
						var width = Math.Max(1, (int)Math.Round(source.Width*scale));
						var height = Math.Max(1, (int)Math.Round(source.Height*scale));
						using(var resized = new Bitmap(width, height, PixelFormat.Format24bppRgb)){
							using(var graphics = Graphics.FromImage(resized)){
								graphics.Clear(Color.White);
								graphics.CompositingQuality = CompositingQuality.HighQuality;
								graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
								graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
								graphics.SmoothingMode = SmoothingMode.HighQuality;
								graphics.DrawImage(source, new Rectangle(0, 0, width, height));
							}
							using(var output = new MemoryStream()){
								SaveJpeg(resized, output, quality);
								if(output.Length <= MaximumImageBytes){
									bytes = output.ToArray();
									return true;
								}
							}
						}
						scale *= 0.75;
						quality = Math.Max(60L, quality - 10L);
					}
				}
			} catch{}
			return false;
		}
		private static void SaveJpeg(Image image, Stream output, long quality){
			var encoder = Array.Find(ImageCodecInfo.GetImageEncoders(), item => item.FormatID == ImageFormat.Jpeg.Guid);
			if(encoder == null){
				image.Save(output, ImageFormat.Jpeg);
				return;
			}
			using(var parameters = new EncoderParameters(1)){
				parameters.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, quality);
				image.Save(output, encoder, parameters);
			}
		}
		private static JsonArrayBuilder BuildVisionContent(string markdown, bool allowRemote, out bool hasImages){
			var parts = Json.ArrayBuilder();
			var images = Find(markdown);
			hasImages = false;
			var position = 0;
			foreach(var image in images){
				if(!TryGetVisionUrl(image.Source, allowRemote, out var imageUrl)) continue;
				AppendTextPart(parts, markdown.Substring(position, image.Start - position));
				parts.Add(Json.Object(Json.P("type", "input_image"), Json.P("image_url", imageUrl)));
				position = image.Start + image.Length;
				hasImages = true;
			}
			if(!hasImages) return parts;
			AppendTextPart(parts, markdown.Substring(position));
			return parts;
		}
		internal static JsonArrayBuilder BuildResponsesContent(string markdown, out bool hasImages){
			return BuildVisionContent(markdown, true, out hasImages);
		}
		internal static string BuildNativeContentJson(string markdown, out bool hasImages){
			var content = BuildVisionContent(markdown ?? "", false, out hasImages);
			if(!hasImages) return Json.String(markdown ?? "").ToJson();
			return ConvertResponsesContentToChat(content.ToNode()).ToJson();
		}
		private static void AppendTextPart(JsonArrayBuilder parts, string text){
			text = (text ?? "").Trim();
			if(text.Length > 0) parts.Add(Json.Object(Json.P("type", "input_text"), Json.P("text", text)));
		}
		internal static JsonNode ConvertResponsesContentToChat(JsonNode content){
			if(!content.IsArray) return content;
			var parts = Json.ArrayBuilder();
			foreach(var part in content){
				if(!part.IsObject) continue;
				var type = part.GetString("type");
				if(string.Equals(type, "input_text", StringComparison.OrdinalIgnoreCase)){ parts.Add(Json.Object(Json.P("type", "text"), Json.P("text", part.GetString("text") ?? ""))); } else if(string.Equals(type, "input_image", StringComparison.OrdinalIgnoreCase)){
					var imageUrl = part.GetString("image_url");
					if(!string.IsNullOrWhiteSpace(imageUrl)) parts.Add(Json.Object(Json.P("type", "image_url"), Json.P("image_url", Json.Object(Json.P("url", imageUrl)))));
				} else{ parts.Add(part); }
			}
			return parts.ToNode();
		}
		private static string GetMimeType(string path){
			switch(Path.GetExtension(path).ToLowerInvariant()){
				case ".bmp": return "image/bmp";
				case ".gif": return "image/gif";
				case ".jpg":
				case ".jpeg": return "image/jpeg";
				case ".tif":
				case ".tiff": return "image/tiff";
				case ".webp": return "image/webp";
				default: return "image/png";
			}
		}
	}
}
