using System;
using System.IO;
using System.Diagnostics;
using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json;
using SharpMake.Data;

/*
-----This application uses Json.Net which is under the MIT License-----

The MIT License (MIT)

Copyright (c) 2007 James Newton-King

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.

----This application is under the MIT License----

MIT License

Copyright (c) 2016 "Eclipsing Rainbows"

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
*/

namespace SharpMake
{
    public class SharpMakeVars
    {
        //Compiler Flags
        public static string COMPILER = "mcs";
        public static readonly string COMPILER_OUT_FLAG = "-out:";
        public static readonly string COMPILER_REFERENCE = "/reference:";

        //Compile types.
        public static readonly string TYPE_EXE = "exe";
        public static readonly string TYPE_LIB = "library";

        //Compile Architectures
        public static readonly string ARCH_ANY = "anycpu";
        public static readonly string ARCH_ANY32 = "anycpu32bitpreferred";
        public static readonly string ARCH_ARM = "arm";
        public static readonly string ARCH_X86 = "x86";
        public static readonly string ARCH_X64 = "x64";
        public static readonly string ARCH_ITA = "itanium";
    }

    public class SharpMake
    {
        /*
        (makefile)            | select Makefile by name, otherwise "Makefile" is selected (if found).
        -v/--verbose          | verbose output
        -h/--help             | Show help
        -o/--out (file name)  | overwrite output file.
        --about               | about information.
        --outputdir (dir)     | overwrite output directory.
        --arch (architecture) | overwrite output target architecture.
        --binref (file)       | Adds an binary reference to a file.
        --asmref (assembly)   | Adds an reference to an .NET assembly
        --pkgref (package)    | Adds an package reference.*/

        private static bool IS_VERBOSE = false;
        private static string CMD_ARGS = "";
        private static string CRT_MKFL = "Makefile";
        private static int TARGET = 0;
        private static string TARGET_NAME = "default";
        private static MakefileData CONFIG;
        private static bool COMMAND_ONLY = false;

        private static string forced_output = "";
        private static List<string> forced_asm = new List<string>();


        public static void Main(string[] args)
        {
            if (System.Environment.OSVersion.Platform == PlatformID.Win32NT ||
                System.Environment.OSVersion.Platform == PlatformID.Win32S ||
                System.Environment.OSVersion.Platform == PlatformID.Win32Windows)
                    SharpMakeVars.COMPILER = "csc.exe";

            if (args.Length > 0)
            {
                for (int i = 0; i < args.Length; i++)
                {
                    string arg = args[i].ToLower();
                    if (arg == "--help" || arg == "-h")
                    {
                        Console.WriteLine(GetHelpStr());
                        Exit();
                    }
                    else if (arg == "--about")
                    {
                        Console.WriteLine(GetAboutStr());
                        Exit();
                    }
                    else if (arg == "--license")
                    {
                        Console.WriteLine(GetLicenseStr());
                        Exit();
                    }
                    else if (arg == "--verbose" || arg == "-v")
                    {
                        IS_VERBOSE = true;
                    }
                    else if (arg == ("--makefile") || arg == "-m")
                    {
                        var mkfile = args[i+1];
                        if (File.Exists(mkfile))
                            CRT_MKFL = mkfile;
                        i++;
                    }
                    else if (arg == "--out" || arg == "-o")
                    {
                        forced_output = args[i+1];
                        i++;
                    }
                    else if (arg == "--mono")
                    {
                        if (System.Environment.OSVersion.Platform == PlatformID.Win32NT ||
                            System.Environment.OSVersion.Platform == PlatformID.Win32S ||
                            System.Environment.OSVersion.Platform == PlatformID.Win32Windows)
                                SharpMakeVars.COMPILER = "mcs";
                        else
                            ErrorPrint("Your system uses Mono by default!", false);
                    }
                    else
                    {
                        TARGET_NAME = arg;
                    }
                }
            }
            if (File.Exists(CRT_MKFL))
            {
                var dat = File.ReadAllText(CRT_MKFL);
                try
                {
                    Print("Loading Makefile...");
                    CONFIG = JsonConvert.DeserializeObject<MakefileData>(dat);
                }
                catch
                {
                    ErrorPrint("Could not parse Makefile, try checking for syntax errors.", true);
                }
                if (CONFIG.target.Count < 1)
                {
                    ErrorPrint("No targets in file!", true);
                }

                foreach(string arg in args)
                {
                    if (arg == "--clean")
                    {
                        Print("Clearning " + CONFIG.target[TARGET].output_dir + "...");
                        foreach(string f in Directory.GetFiles(CONFIG.target[TARGET].output_dir))
                        {
                            Print("Removing " + f + "...");
                            File.Delete(f);
                        }
                        Exit();
                    }
                }


                if (ContainsTargetName(TARGET_NAME) && TARGET_NAME.ToLower() != "default")
                {
                    TARGET = GetTargetIdFromName(TARGET_NAME);
                }
                else
                {
                    if (TARGET_NAME.ToLower() != "default")
                        ErrorPrint(string.Format("Could not find target '{0}'.", TARGET_NAME), true);
                }


                BeginRecipePre();
                if (!COMMAND_ONLY)
                    BeginBuild();
                else
                    Print("Command-only recipe rules, skipping building steps.");
                BeginRecipePost();
            }
            else
                ErrorPrint(string.Format("Could not find a Makefile with the name \"{0}\".", CRT_MKFL), true);
        }

        private static bool ContainsTargetName(string name)
        {
            foreach(Target t in CONFIG.target)
            {
                if (t.target_name == name)
                    return true;
            }
            return false;
        }

        private static int GetTargetIdFromName(string name)
        {
            int index = 0;
            foreach(Target t in CONFIG.target)
            {
                if (t.target_name == name)
                    return index;
                index++;
            }
            return -1;
        }

        private static bool ContainsRecipeWithName(string name, bool alert)
        {
            if (CONFIG.target[TARGET].recipes != null && CONFIG.target[TARGET].recipes.Count > 0)
            {
                foreach(Recipes r in CONFIG.target[TARGET].recipes)
                {
                    if (r.recipe_name == name)
                    {
                        COMMAND_ONLY = r.command_only;
                        return true;
                    }
                }
                return false;
            }
            else
            {
                if (alert) Print("No Recipes found!");
            }
            return false;
        }

        public enum RecipeTiming
        {
            Pre,
            Post
        }

        static List<string> GetFilesRecursivley(string dir)
        {
            List<string> files = new List<string>();
            try
            {
                foreach (string f in Directory.GetFiles(dir))
                {
                    files.Add(f);
                }
                foreach (string d in Directory.GetDirectories(dir))
                {
                    foreach (string fi in GetFilesRecursivley(d))
                    {
                        files.Add(fi);
                    }
                }
            }
            catch (System.Exception excpt)
            {
                Console.WriteLine(excpt.Message);
            }
            return files;
        }

        private static List<RecipeData> GetRecipesFor(string name, RecipeTiming timing)
        {
            List<RecipeData> output = new List<RecipeData>();
            foreach(Recipes r in CONFIG.target[TARGET].recipes)
            {
                if (r.recipe_name == name)
                {
                    foreach(RecipeData dat in r.recipe_data)
                    {
                        if (dat.is_post && timing == RecipeTiming.Post)
                            output.Add (dat);
                        else if (!dat.is_post && timing == RecipeTiming.Pre)
                            output.Add (dat);
                    }
                }
            }
            return output;
        }

        private static System.Diagnostics.Stopwatch recipeWatch = null;
        private static void BeginRecipePre()
        {
            Print("Running pre-recipes for " + CONFIG.target[TARGET].target_name + "...");
            if (ContainsRecipeWithName(CONFIG.target[TARGET].target_name, true))
            {
                if (COMMAND_ONLY)
                {
                    recipeWatch = System.Diagnostics.Stopwatch.StartNew();
                }
                foreach(var data in GetRecipesFor(CONFIG.target[TARGET].target_name, RecipeTiming.Pre))
                {
                    foreach(var dat in data.commands)
                    {
                        string command = dat.Split(' ')[0];
                        string args = dat.Substring(command.Length);

                        RunCommandIndep(command, args);
                    }
                }
            }
        }

        private static void BeginRecipePost()
        {
            Print("Running post-recipes for " + CONFIG.target[TARGET].target_name + "...");
            if (ContainsRecipeWithName(CONFIG.target[TARGET].target_name, false))
            {
                foreach(var data in GetRecipesFor(CONFIG.target[TARGET].target_name, RecipeTiming.Post))
                {
                    foreach(var dat in data.commands)
                    {
                        string command = dat.Split(' ')[0];
                        string args = dat.Substring(command.Length);

                        RunCommandIndep(command, args);
                    }
                }
            }
            if (COMMAND_ONLY)
            {
                recipeWatch.Stop();
                var elapsedMs = recipeWatch.ElapsedMilliseconds;
                Print(string.Format("Done! [Recipe Time {0}ms]", elapsedMs));
            }
        }


        private static void BeginBuild()
        {
            if (!COMMAND_ONLY)
            {
                var watch = System.Diagnostics.Stopwatch.StartNew();
                if (!string.IsNullOrEmpty(forced_output))
                    CONFIG.target[TARGET].output = forced_output;


                if (IS_VERBOSE)
                {
                    Console.WriteLine("[VERBOSE ENABLED]\n");
                    Console.WriteLine(RunCommand("mcs", "--about"));
                }
                //VerbosePrint("InputJSON: \n" + File.ReadAllText(CRT_MKFL));


                VerbosePrint("OutputFile: " + CONFIG.target[TARGET].output + " | OutputType: " + CONFIG.target[TARGET].output_type);
                Print("Preparing output directory...");
                GetDir(CONFIG.target[TARGET].output_dir);

                Print("Adding files to build queue...");
                foreach(string file in GetFilesRecursivley(CONFIG.target[TARGET].code_root))
                {
                    AddFile(file);
                }

                Print("Referencing Assemblies...");
                foreach(string asm in Directory.GetFiles(CONFIG.target[TARGET].lib_dir))
                {
                    VerbosePrint("Checking out assembly: " + asm + "...");
                    if (CONFIG.target[TARGET].ref_asm.Contains(asm.Replace(CONFIG.target[TARGET].lib_dir, "")))
                    {
                        ReferenceAssembly(asm);
                        if (!File.Exists(CONFIG.target[TARGET].output_dir + asm.Replace(CONFIG.target[TARGET].lib_dir, "")))
                            File.Copy(asm, CONFIG.target[TARGET].output_dir + asm.Replace(CONFIG.target[TARGET].lib_dir, ""));
                    }
                }
                Print("Referencing Packages...");
                foreach(string pkg in CONFIG.target[TARGET].ref_pkgs)
                {
                    AddPackage(pkg);
                }

                SetOutputFile();

                if (!IS_VERBOSE)
                    Print(string.Format("Compiling {0} to {1}...",CONFIG.target[TARGET].output + TranslateExtensionNoAction(CONFIG.target[TARGET].output_type), CONFIG.target[TARGET].output_dir));
                VerbosePrint("Running Compiler... [mcs " + CMD_ARGS + "]");
                if (!IS_VERBOSE) RunCommand("mcs", CMD_ARGS);
                else RunCommandIndep("mcs", CMD_ARGS);
                watch.Stop();
                var elapsedMs = watch.ElapsedMilliseconds;
                Print(string.Format("Done! [Build Time {0}ms]", elapsedMs));
            }
        }

        private static void AddPackage(string package)
        {
            VerbosePrint("Adding reference to package: " + package + "...");
            CMD_ARGS += "-pkg:" + package + " ";
        }

        private static void AddFile(string file)
        {
            VerbosePrint("Adding file: " + file + " to build queue...");
            if (!file.ToLower().EndsWith(".cs"))
                CMD_ARGS += "-resource:" + file + " ";
            else
                CMD_ARGS += file + " ";
        }

        private static void VerbosePrint(string message)
        {
            if (IS_VERBOSE)
                Console.WriteLine("[VERBOSE] " + message);
        }

        private static void Print(string message)
        {
            Console.WriteLine("[INFO] " + message);
        }

        private static void ErrorPrint(string message, bool fatal)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("[INFO] " + message);
            Console.ForegroundColor = ConsoleColor.Gray;
            if (fatal)
                Exit();
        }

        private static void SetOutputFile()
        {
            CMD_ARGS += "-o " + CONFIG.target[TARGET].output_dir + CONFIG.target[TARGET].output + TranslateExtension(CONFIG.target[TARGET].output_type) + " ";
        }

        private static string TranslateExtension(string input)
        {
            if (input.ToLower() == "lib" || input.ToLower() == "library" || input.ToLower() == "dll" || input.ToLower() == "so")
                return ".dll -target:library";
            else if (input.ToLower() == "exe" || input.ToLower() == "executable" || input.ToLower() == "app" || input.ToLower() == "application")
                return ".exe -target:exe";
            return ".o -target:library";
        }
        private static string TranslateExtensionNoAction(string input)
        {
            if (input.ToLower() == "lib" || input.ToLower() == "library" || input.ToLower() == "dll" || input.ToLower() == "so")
                return ".dll";
            else if (input.ToLower() == "exe" || input.ToLower() == "executable" || input.ToLower() == "app" || input.ToLower() == "application")
                return ".exe";
            return ".o";
        }


        private static string TranslateOutputType(string input)
        {
            if (input == "lib") return SharpMakeVars.TYPE_LIB;
            else if (input == "exe") return SharpMakeVars.TYPE_EXE;
            throw new Exception("Unrecognized Output Type!");
        }

        private static void ReferenceAssembly(string assembly)
        {
            CMD_ARGS += SharpMakeVars.COMPILER_REFERENCE + assembly + " ";
            VerbosePrint("Adding Reference: " + assembly + " to build queue...");
        }

        private static void Exit()
        {
            Environment.Exit(0);
        }

        private static void RunCommandIndep(string command, string arguments)
        {
            Process proc = new Process();
            proc.StartInfo.UseShellExecute = false;
            proc.StartInfo.RedirectStandardOutput = false;
            proc.StartInfo.RedirectStandardError = false;
            proc.StartInfo.FileName = command;
            proc.StartInfo.Arguments = arguments;
            proc.Start();
            proc.WaitForExit ();
        }

        private static string RunCommand(string command, string arguments)
        {
            Process proc = new Process();
            proc.StartInfo.UseShellExecute = false;
            proc.StartInfo.RedirectStandardOutput = true;
            proc.StartInfo.RedirectStandardError = true;
            proc.StartInfo.FileName = command;
            proc.StartInfo.Arguments = arguments;
            proc.Start();
            string output = "";
            string sec_output = "";
            string lastline = "";
            string lastErline = "";
            sec_output = proc.StandardOutput.ReadToEnd();
            sec_output += proc.StandardError.ReadToEnd();
            while (!proc.StandardOutput.EndOfStream && !proc.StandardError.EndOfStream){
                string currentLine = proc.StandardOutput.ReadLine ();
                if (currentLine != lastline)
                {
                    Console.WriteLine (currentLine);
                    output += currentLine + "\n";
                }
                lastline = currentLine;

                string currentErLine = proc.StandardError.ReadLine ();
                if (currentErLine != lastErline)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine (currentErLine);
                    Console.ForegroundColor = ConsoleColor.Gray;
                    output += currentErLine + "\n";
                }
                lastErline = currentErLine;
            }
            proc.WaitForExit ();
            if (output != "")
                return output;
            else
                return sec_output;
        }

        // Gets a directory, makes it if not found
        private static string GetDir(string dir)
        {
            try
            {
                string[] dirs = Directory.GetDirectories(dir);
                if (dirs.Length > 1)
                    return dirs[0];
            }
            catch
            {
                Directory.CreateDirectory(dir);
            }
            return dir;
        }

        private static string GetLicenseStr()
        {
            return @"
-----This application uses Json.Net which is under the MIT License-----

The MIT License (MIT)

Copyright (c) 2007 James Newton-King

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the 'Software'), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED 'AS IS', WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.

----This application is under the MIT License----

MIT License

Copyright (c) 2016 'Eclipsing Rainbows'

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the 'Software'), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED 'AS IS', WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.";
        }

        private static string GetAboutStr()
        {
            return @"---About---
#Make ('smake') JSON based .NET application Makefile System by Eclipsing Rainbows [https://twitter.com/EclipsingR]
This application is using Newtonsoft's JSON.Net library [https://github.com/JamesNK/Newtonsoft.Json]
Run 'smake --license' to see license information, or consult the LICENSE file bundled with this application.
";
        }

        private static string GetHelpStr()
        {
            return @"How To:
    To run this application, just run 'smake' and the output file should be made.
    If the makefile is not called 'Makefile', please select the name of the makefile as an argument.
    Note for Makefile creators: Default makefile is the first makefile in the list!
Example Usage:
    smake                                   | Default run
    smake -m OtherMakefile build_and_clean  | Select Makefile 'OtherMakeFile' and run 'build_and_clean' in it.
    smake --verbose --mono --arch x86       | Run verbose, force 32 bit and force to use mono.
Arguments (Optional):
    [target]              | Select target to be run.
    -m/--makefile <file>  | Select Makefile by name, otherwise 'Makefile' is selected (if found).
    -v/verbose            | Enable verbose logging output.
    -h/--help             | Show help.
    -o/--out <file name>  | Overwrite output file.
    --about               | Shows about information.
    --license             | Shows license information.
    --outputdir <dir>     | Overrides output directory.
    --arch <architecture> | Overrides output target architecture.
    --binref <file>       | Adds an binary reference to a file.
    --asmref <assembly>   | Adds an reference to an .NET assembly
    --pkgref <package>    | Adds an package reference.
    --mono                | Force to use mcs. (Windows only)
    --clean               | Cleans build output folder.";
        }
    }
}
