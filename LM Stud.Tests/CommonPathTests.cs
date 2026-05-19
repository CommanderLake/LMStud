using System.IO;
using LMStud;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LM_Stud.Tests{
	[TestClass]
	public class CommonPathTests{
		[TestMethod]
		public void GetPathRelativeToModelsDir_WithChildPath_ReturnsRelativePath(){
			var oldModelsDir = Common.ModelsDir;
			try{
				Common.ModelsDir = Path.Combine(Path.GetTempPath(), "LMStudModels") + Path.DirectorySeparatorChar;
				var modelPath = Path.Combine(Common.ModelsDir, "vendor", "model.gguf");
				Assert.AreEqual(Path.Combine("vendor", "model.gguf"), Common.GetPathRelativeToModelsDir(modelPath));
			} finally{
				Common.ModelsDir = oldModelsDir;
			}
		}

		[TestMethod]
		public void GetPathRelativeToModelsDir_WithSiblingPath_DoesNotStripPrefix(){
			var oldModelsDir = Common.ModelsDir;
			try{
				var tempRoot = Path.GetTempPath().TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
				Common.ModelsDir = Path.Combine(tempRoot, "LMStudModels") + Path.DirectorySeparatorChar;
				var siblingPath = Path.Combine(tempRoot, "LMStudModels2", "model.gguf");
				Assert.AreEqual(siblingPath, Common.GetPathRelativeToModelsDir(siblingPath));
			} finally{
				Common.ModelsDir = oldModelsDir;
			}
		}
	}
}
