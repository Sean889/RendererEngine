using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml;
using System.IO;

namespace Parser
{
    //Parses a gl.xml specification file into a C# binding
    class Program
    {
        static string[,] MapIds = 
        {
            { "unsigned long long",   "UInt64"    },
            { "unsigned long",        "UInt32"    },
            { "unsigned int",         "UInt32"    },
            { "unsigned short",       "UInt16"    },
            { "unsigned char",        "Byte"      },
            { "char",                 "Char"      },
            { "signed char",          "SByte"     },
            { "short",                "Int16"     },
            { "int",                  "Int32"     },
            { "long",                 "Int32"     },
            { "long long",            "Int64"     },
            { "int8_t",               "SByte"     },
            { "int16_t",              "Int16"     },
            { "int32_t",              "Int32"     },
            { "int64_t",              "Int64"     },
            { "uint8_t" ,             "Byte"      },
            { "uint16_t",             "UInt16"    },
            { "uint32_t",             "UInt32"    },
            { "uint64_t",             "UInt64"    },
            { "ptrdiff_t",            "IntPtr"    },
            { "double",               "Double"    },
            { "float",                "Single"    },
            { "GLint",                "Int32"     },
            { "struct __GLsync *",     "UIntPtr"   },
            { "void *",                "UIntPtr"   },
            { "GLintptr",             "IntPtr"    }
        };

        public static string ReplaceLastOccurrence(string Source, string Find, string Replace)
        {
            int place = Source.LastIndexOf(Find);

            if (place == -1)
                return string.Empty;

            string result = Source.Remove(place, Find.Length).Insert(place, Replace);
            return result;
        }

        static void Main(string[] args)
        {
            Dictionary<string, string> idmap = new Dictionary<string, string>();

            for(int i = 0; i < MapIds.GetLength(0); i++)
            {
                idmap.Add(MapIds[i, 0], MapIds[i, 1]);
            }

            XmlDocument doc = new XmlDocument();
            doc.Load("D:\\gl.xml");

            List<string> Lines = new List<string>();
            List<string> Names = new List<string>();
            List<string> Commands = new List<string>(); 

            Lines.Add("using System;\n");
            Lines.Add("namespace OpenGL\n{");


            //Typedefs
            foreach(XmlNode ParentNode in doc.SelectNodes("//types"))
            {
                foreach (XmlNode Node in ParentNode.ChildNodes)
                {
                    try
                    {
                        XmlNode Name = Node.FirstChild;
                        XmlNode Type = Node.ChildNodes[1].FirstChild;

                        try
                        {
                            if (Node.Attributes != null && ((Node.Attributes["api"].Value == "gles1") || (Node.Attributes["api"].Value == "gles2")))
                            {
                                continue;
                            }
                        }
                        catch (NullReferenceException e) { }


                        if (Name.Value.Length > 15)
                        {
                            string Id = Name.Value.Substring(15).Trim(' ');

                            if (Id == "void") continue;

                            Lines.Add("\tusing " + Type.Value.Trim(' ') + " = " + (!idmap.ContainsKey(Id) ? Id : idmap[Id]) + ";");
                        }
                    }
                    catch (NullReferenceException e)
                    {

                    }
                }
            }
            
            Lines.Add("\n\tpublic class GL");
            Lines.Add("\t{");

            //Enums
            foreach(XmlNode ParentNode in doc.SelectNodes("//enums"))
            {
                foreach(XmlNode Node in ParentNode.ChildNodes)
                {
                    try
                    {
                        string name = Node.Attributes["name"].Value;
                        string value = Node.Attributes["value"].Value;

                        if (!Names.Contains(name))
                        {
                            Lines.Add("\t\tpublic const uint " + name + " = " + value + ";");
                            Names.Add(name);
                        }
                    }
                    catch(NullReferenceException)
                    {

                    }
                }
            }

            Lines.Add("");

            {
                List<string> PublicFunctions = new List<string>();

                //Commands
                foreach(XmlNode Command in doc.SelectNodes("//commands//command"))
                {
                    XmlNode Proto = Command.FirstChild;

                    string RetType = Proto.FirstChild.InnerText.Trim(' ', '\r', '\n');
                    string FuncName = Proto.LastChild.InnerText.Trim(' ');

                    Commands.Add(FuncName);

                    List<string> ParamTypes = new List<string>();
                    List<string> ParamNames = new List<string>();
                    XmlNodeList list = Command.SelectNodes("param");

                    foreach(XmlNode Param in list)
                    {
                        string Name = Param.LastChild.InnerText;
                        string Type = Regex.Replace(Regex.Replace(ReplaceLastOccurrence(Param.InnerText.Trim(), Name, ""), "const", ""), "struct", "");

                        if(Type == "const")
                        {
                            Console.Write("");
                        }

                        ParamNames.Add(Name);
                        ParamTypes.Add(Type);
                    }

                    string Args = "";
                    foreach(string param in ParamTypes)
                    {
                        Args += param + ", ";
                    }

                    if(RetType == "void")
                    {
                        if(list.Count == 0)
                        {
                            Lines.Add("private static Action __" + FuncName + ";");
                            PublicFunctions.Add("public static Action " + FuncName.Substring(2) + "{ get{ return __" + FuncName + ";} }");
                        }
                        else
                        {
                            Lines.Add("private static Action<" + Args.Substring(0, Args.Length - 2) +  "> __" + FuncName + ";");
                            PublicFunctions.Add("public static Action<" + Args.Substring(0, Args.Length - 2) + "> " + FuncName.Substring(2) + "{ get{ return __" + FuncName + ";} }");
                        }
                    }
                    else
                    {
                        if(list.Count == 0)
                        {
                            Lines.Add("private static Func<" + RetType + "> __" + FuncName + ";");
                            PublicFunctions.Add("public static Func<" + RetType + "> " + FuncName.Substring(2) + "{ get{ return __" + FuncName + ";} }");
                        }
                        else
                        {
                            Lines.Add("private static Func<" + Args + RetType + "> __" + FuncName + ";");
                            PublicFunctions.Add("public static Func<" + Args + RetType + "> " + FuncName.Substring(2) + "{ get { return __" + FuncName + ";} }");
                        }
                    }
                }

                Lines.AddRange(PublicFunctions);
            }

            Lines.Add("\t}");
            Lines.Add("}");

            StreamWriter file = new StreamWriter("D:\\Programs\\RendererEngine\\LodPlanet\\GL.cs");

            int size = Lines.Count;
            for (int i = 0; i < size; i++)
            {
                Lines[i] = Regex.Replace(Lines[i], "GLvoid", "void");
                Lines[i] = Regex.Replace(Lines[i], "void*", "IntPtr");
                Lines[i] = Regex.Replace(Lines[i], "  ", " ");
            }

            foreach (string line in Lines)
            {
                file.WriteLine(line);
            }

            file.Close();
        }
    }
}
