using ClangSharp;
using System;
using System.Linq;

namespace Hebron.Rust
{
	partial class RustCodeConverter
	{
		private void FillTypeDeclaration(Cursor cursor, string name)
		{
			var declaration = new IndentedStringWriter();
			declaration.IndentedWriteLine("#[derive(Debug, Copy, Clone)]");
			declaration.IndentedWriteLine("pub struct " + name + " {");
			++declaration.IndentLevel;

			var def = new IndentedStringWriter();
			def.IndentedWriteLine("impl std::default::Default for " + name + " {");
			++def.IndentLevel;
			def.IndentedWriteLine("fn default() -> Self {");
			++def.IndentLevel;
			def.IndentedWriteLine(name + " {");
			++def.IndentLevel;

			foreach (NamedDecl child in cursor.CursorChildren)
			{
				if (child is RecordDecl)
				{
					continue;
				}

				var asField = (FieldDecl)child;
				var childName = asField.Name.FixSpecialWords();
				var typeInfo = asField.Type.ToTypeInfo();

				if (typeInfo.TypeString.Contains("unnamed "))
				{
					// unnamed struct
					var subName = name + "_unnamed1";
					FillTypeDeclaration(child.CursorChildren[0], subName);

					typeInfo = new TypeInfo(new StructTypeInfo(subName), typeInfo.PointerCount, typeInfo.ConstantArraySizes);
				}

				var expr = "pub " + childName + ":" + ToRustString(typeInfo) + ",";
				declaration.IndentedWriteLine(expr);

				expr = childName + ": " + typeInfo.GetDefaltValue() + ",";
				def.IndentedWriteLine(expr);
			}

			--declaration.IndentLevel;
			declaration.IndentedWriteLine("}");

			--def.IndentLevel;
			def.IndentedWriteLine("}");
			--def.IndentLevel;
			def.IndentedWriteLine("}");
			--def.IndentLevel;
			def.IndentedWriteLine("}");

			Result.Structs[name] = declaration.ToString();
			Result.StructDefaults[name] = def.ToString();
		}

		public void ConvertStructs()
		{
			if (!Parameters.ConversionEntities.HasFlag(ConversionEntities.Structs))
			{
				return;
			}

			Logger.Info("Processing structs...");

			_state = State.Structs;

			foreach (var cursor in TranslationUnit.EnumerateCursors())
			{
				if (cursor.CursorKind != ClangSharp.Interop.CXCursorKind.CXCursor_StructDecl)
				{
					continue;
				}

				var recordDecl = (RecordDecl)cursor;
				var name = recordDecl.GetName().FixSpecialWords();
				if (Parameters.SkipStructs.Contains(name))
				{
					Logger.Info("Skipping.");
					continue;
				}

				Logger.Info("Generating code for struct {0}", name);

				FillTypeDeclaration(cursor, name);
			}
		}
	}
}