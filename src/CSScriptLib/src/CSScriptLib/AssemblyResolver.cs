using CSScripting;
using CSScriptLib;
using System.Collections.Generic;
using System.IO;

namespace csscript
{
    /// <summary>
    /// Delegate implementing source file probing algorithm.
    /// </summary>
    /// <param name="file">The file.</param>
    /// <param name="searchDirs">The extra dirs.</param>
    /// <param name="throwOnError">if set to <c>true</c> [throw on error].</param>
    /// <returns>Location of the files matching the resolution input. </returns>
    public delegate string[] ResolveSourceFileAlgorithm(string file, string[] searchDirs, bool throwOnError);

    /// <summary>
    /// Delegate implementing assembly file probing algorithm.
    /// </summary>
    /// <param name="file">The file.</param>
    /// <param name="searchDirs">The extra dirs.</param>
    /// <returns>Location of the files matching the resolution input.</returns>
    public delegate string[] ResolveAssemblyHandler(string file, string[] searchDirs);

    /// <summary>
    /// Utility class for assembly probing.
    /// </summary>
    public class AssemblyResolver
    {
        /// <summary>
        /// File to be excluded from assembly search
        /// </summary>
        static public string ignoreFileName = "";

        static readonly char[] illegalChars = ":*?<>|\"".ToCharArray();

        /// <summary>
        /// Determines whether the string is a legal path token.
        /// </summary>
        /// <param name="name">The name.</param>
        /// <returns>
        /// 	<c>true</c> if the string is a legal path token; otherwise, <c>false</c>.
        /// </returns>
        static public bool IsLegalPathToken(string name)
        {
            return name.IndexOfAny(illegalChars) != -1;
        }

        static internal ResolveAssemblyHandler FindAssemblyAlgorithm = DefaultFindAssemblyAlgorithm;

        /// <summary>
        /// Resolves namespace/assembly(file) name into array of assembly locations (local and GAC ones).
        /// </summary>
        /// <param name="name">'namespace'/assembly(file) name</param>
        /// <param name="searchDirs">Assembly search directories</param>
        /// <para>If the default implementation isn't suitable then you can set <c>CSScript.FindAssemblyAlgorithm</c>
        /// to the alternative implementation of the probing algorithm.</para>
        /// <returns>collection of assembly file names where namespace is implemented</returns>
        static public string[] FindAssembly(string name, string[] searchDirs)
        {
            return FindAssemblyAlgorithm(name, searchDirs);
        }

        static string[] DefaultFindAssemblyAlgorithm(string name, string[] searchDirs)
        {
            List<string> retval = new List<string>();

            if (!IsLegalPathToken(name))
            {
                foreach (string dir in searchDirs)
                {
                    foreach (string asmLocation in FindLocalAssembly(name, dir))	//local assemblies alternative locations
                        retval.Add(asmLocation);

                    if (retval.Count != 0)
                        break;
                }

                if (retval.Count == 0)
                {
                    string nameSpace = name.RemoveAssemblyExtension();
                    foreach (string asmGACLocation in FindGlobalAssembly(nameSpace))
                    {
                        retval.Add(asmGACLocation);
                    }
                }
            }
            else
            {
                try
                {
                    if (Path.IsPathRooted(name) && File.Exists(name)) //note relative path will return IsLegalPathToken(name)==true
                        retval.Add(name);
                }
                catch { } //does not matter why...
            }
            return retval.ToArray();
        }

        /// <summary>
        /// Resolves namespace into array of local assembly locations.
        /// (Currently it returns only one assembly location but in future
        /// it can be extended to collect all assemblies with the same namespace)
        /// </summary>
        /// <param name="name">namespace/assembly name</param>
        /// <param name="dir">directory</param>
        /// <returns>collection of assembly file names where namespace is implemented</returns>
        public static string[] FindLocalAssembly(string name, string dir)
        {
            //We are returning and array because name may represent assembly name or namespace
            //and as such can consist of more than one assembly file (multiple assembly file is not supported at this stage).
            try
            {
                string asmFile = Path.Combine(dir, name);

                //cannot just check Directory.Exists(dir) as "name" can contain sum subDir parts
                if (Directory.Exists(Path.GetDirectoryName(asmFile)))
                {
                    //test first the exact file name and then well-known assembly extensions first
                    var possibleExtensions = new string[] { "", ".dll", ".exe" };

                    // if user specified file name has no extension test first any file that has well-known assembly extension
                    // triggered by https://github.com/oleg-shilo/cs-script/issues/319
                    if (name.GetExtension() == "")
                        possibleExtensions = new string[] { ".dll", ".exe", "" };

                    foreach (string ext in possibleExtensions)
                    {
                        string file = asmFile + ext; //just in case if user did not specify the extension
                        if (ignoreFileName != Path.GetFileName(file) && File.Exists(file))
                            return new string[] { file };
                    }

                    if (asmFile != Path.GetFileName(asmFile) && File.Exists(asmFile))
                        return new string[] { asmFile };
                }
            }
            catch { } //name may not be a valid path name
            return new string[0];
        }

        /// <summary>
        /// Resolves namespace into array of global assembly (GAC) locations.
        /// <para>NOTE: this method does nothing on .NET Core as it offers no GAC discovery mechanism.</para>
        /// </summary>
        /// <param name="namespaceStr">'namespace' name</param>
        /// <returns>collection of assembly file names where namespace is implemented</returns>
        public static string[] FindGlobalAssembly(string namespaceStr)
        {
            // .NET Core does not offer any asm discovery mechanism
            // Thus just check for the candidates in the "global shared" folder

            string shared_dir = Path.GetDirectoryName("".GetType().Assembly.Location);
            return FindLocalAssembly(namespaceStr, shared_dir);
        }
    }
}