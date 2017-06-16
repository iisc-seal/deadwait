using System;
using System.IO;
using System.Collections.Generic;
using System.Text;
using System.Diagnostics;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters;
using System.Runtime.Serialization.Formatters.Binary;
using System.ComponentModel;
using System.Security.Cryptography;

using SafetyAnalysis.Purity;
using SafetyAnalysis.Framework.Graphs;
using SafetyAnalysis.Util;

using QuickGraph;
using QuickGraph.Algorithms.Search;
using SafetyAnalysis.Purity.ControlFlowAnalysisPhase;
using SafetyAnalysis.Purity.Summaries;

namespace SafetyAnalysis.Checker
{
    public class Checker
    {        
        //inputs
        public static Phx.Controls.CumulativeStringControl input;
        public static Phx.Controls.StringControl configfile;
        public static Phx.Controls.StringControl outdirname;
        public static Phx.Controls.StringControl IsFramework;
        
        //public static string sealHome = Environment.GetEnvironmentVariable("SEALHOME");
        public static string sealHome = @"C:\SEAL";
        public static string DefaultConfigFile = sealHome + @"\Configs\Default.config";
        public static string SourceSinkConfigFile = sealHome + @"\Configs\sourcesink.config";      

        //name of the ouput binary if any
        public static Phx.Controls.StringControl output;
        public static Phx.Controls.StringControl pdbout;

        //console options for sourcesink or cast analysis
        public static Phx.Controls.StringControl analysistype;

        public static Phx.Controls.StringControl sourcefile;
        public static Phx.Controls.StringControl sourceline;
        public static Phx.Controls.StringControl sinkfile;
        public static Phx.Controls.StringControl sinkline;
        public static Phx.Controls.StringControl castfile;
        public static Phx.Controls.StringControl castline;
        public static Phx.Controls.StringControl checkingFunction;
       
        public static void StaticInitialize(string[] arguments)
        {
            // Initialize the available targets.
            Checker.InitializeTargets();

            // Start initialization of the Phoenix framework.
            Phx.Initialize.BeginInitialization();
            
            // Initialize the command line string controls
            Checker.InitializeCommandLine();

            // Initialize the component control for the static analysis phase.
            // This is included so that standard Phoenix controls can be used 
            // on this too.

            PurityAnalysisPhase.Initialize();

            Phx.Initialize.EndInitialization("PHX|*|_PHX_", arguments);

            // Check the processed command line options against those required
            // by the tool for execution.  If they are not present, exit the 
            // app.
            if (!Checker.CheckCommandLine())
            {
                Checker.Usage();
                Phx.Term.All(Phx.Term.Mode.Fatal);
                Environment.Exit(1);
            }
        }
        
        public static void Usage()
        {
            Phx.Output.WriteLine("Usage: Checker /in image1 /in image2 /in ... [/config-file <config-filename>] [/outdir <output-dir>]");
        }
        
        private static bool CheckCommandLine()
        {
            return Checker.input.GetValue(null).Count() > 0;                          
        }

        //Registers string controls with Phoenix for command line option processing.        
        private static void InitializeCommandLine()
        {
            // Initialize each command line option (string controls), so that
            // the framework knows about them.

            // C# doesn't have a __LINE__ macro, so instead we have chosen a 
            // hardcoded value, with some room to add other values in between
            Checker.input = Phx.Controls.CumulativeStringControl.New("in",
                "input file",
                Phx.Controls.Control.MakeFileLineLocationString("Checker.cs",100));
           
            Checker.configfile = Phx.Controls.StringControl.New("config-file",
                "Configuration file",
                Phx.Controls.Control.MakeFileLineLocationString("Checker.cs", 100));

            Checker.outdirname = Phx.Controls.StringControl.New("outdir",
                "output directory",
                Phx.Controls.Control.MakeFileLineLocationString("Checker.cs", 100));

            Checker.IsFramework = Phx.Controls.StringControl.New("framework",
                "are we processing framework libraries?",
                Phx.Controls.Control.MakeFileLineLocationString("Checker.cs", 100));

            //Added for sourcesink and cast analysis

            Checker.analysistype = Phx.Controls.StringControl.New("analysistype", "analysis type", Phx.Controls.Control.MakeFileLineLocationString("Checker.cs", 100));

            Checker.sourcefile = Phx.Controls.StringControl.New("sourcefile", "source file", Phx.Controls.Control.MakeFileLineLocationString("Checker.cs", 100));
            Checker.sourceline = Phx.Controls.StringControl.New("sourceline", "source line", Phx.Controls.Control.MakeFileLineLocationString("Checker.cs", 100));
            Checker.sinkfile = Phx.Controls.StringControl.New("sinkfile", "sink file", Phx.Controls.Control.MakeFileLineLocationString("Checker.cs", 100));
            Checker.sinkline = Phx.Controls.StringControl.New("sinkline", "sink line", Phx.Controls.Control.MakeFileLineLocationString("Checker.cs", 100));
            Checker.castfile = Phx.Controls.StringControl.New("castfile", "cast file", Phx.Controls.Control.MakeFileLineLocationString("Checker.cs", 100));
            Checker.castline = Phx.Controls.StringControl.New("castline", "cast line", Phx.Controls.Control.MakeFileLineLocationString("Checker.cs", 100));
            Checker.checkingFunction = Phx.Controls.StringControl.New("function", "checking function", Phx.Controls.Control.MakeFileLineLocationString("Checker.cs", 100));

        }

        //Initialize the dependent architectures and runtimes.
        private static void InitializeTargets()
        {
            // Setup targets available in the RDK.
            Phx.Targets.Architectures.Architecture x86Arch =
                Phx.Targets.Architectures.X86.Architecture.New();
            Phx.Targets.Runtimes.Runtime win32x86Runtime =
                Phx.Targets.Runtimes.Vccrt.Win32.X86.Runtime.New(x86Arch);
            Phx.GlobalData.RegisterTargetArchitecture(x86Arch);
            Phx.GlobalData.RegisterTargetRuntime(win32x86Runtime);

            Phx.Targets.Architectures.Architecture msilArch =
                Phx.Targets.Architectures.Msil.Architecture.New();
            Phx.Targets.Runtimes.Runtime win32MSILRuntime =
                Phx.Targets.Runtimes.Vccrt.Win.Msil.Runtime.New(msilArch);
            Phx.GlobalData.RegisterTargetArchitecture(msilArch);
            Phx.GlobalData.RegisterTargetRuntime(win32MSILRuntime);
        }

        private static void InitializeProperties()
        {
            PurityAnalysisPhase.sealHome = sealHome + "\\";                                                
            FileStream fs;
            string configfilename = "";

            if (PurityAnalysisPhase.analysistype == "sourcesinkanalysis")
            {
                configfilename = SourceSinkConfigFile;
            }
            else if(PurityAnalysisPhase.analysistype == "castanalysis")
            {
                configfilename = SourceSinkConfigFile;
            }
            else if (PurityAnalysisPhase.analysistype == "")
            {
                configfilename = Checker.configfile.GetValue(null);
            }

            if (String.IsNullOrEmpty(configfilename))
                fs = File.OpenRead(DefaultConfigFile);
            else
                fs = File.OpenRead(configfilename);

            var reader = new StreamReader(fs);
            while (!reader.EndOfStream)
            {
                var entry = reader.ReadLine();
                if (String.IsNullOrEmpty(entry) || entry.StartsWith("#"))
                    continue; 
              
                var fields = entry.Split('=');
                if (fields.Length != 2)
                    throw new ArgumentException("entry not in valid format: " + entry);

                var property = fields[0].Trim().ToLower();
                var value = fields[1].Trim().ToLower();
                //add to property map
                PurityAnalysisPhase.properties.Add(property, value);
            }
            fs.Close();

            PurityAnalysisPhase.InitializeProperties();            
        }

        public Phx.Term.Mode ProcessModules(Phx.Collections.PhxStringList inputs)
        {           
            //parse the string to get the dlls            
            List<string> dllnames = new List<string>();

            //Clear previously stored summaries
            //ClearDBContents(dllnames);

            try
            {
                //initialize the program unit                
                var lifetime = Phx.Lifetime.New(Phx.LifetimeKind.Global, null);
                //// Create an empty program unit                                            
                var programUnit = Phx.ProgramUnit.New(lifetime, null, Phx.GlobalData.TypeTable, null, null);
                
                foreach (var dllpath in inputs)
                {
                    //Phx.Output.WriteLine("Adding " + dllpath + " ...");
                    var moduleunit = Phx.PEModuleUnit.Open((string)dllpath);
                    programUnit.AddModuleUnit(moduleunit);                    
                }

                //execute the main purity analysis phase
                Phx.Phases.PhaseConfiguration mainconfig =
                    Phx.Phases.PhaseConfiguration.New(lifetime, "Bottom-up Analysis phase");
                mainconfig.PhaseList.AppendPhase(PurityAnalysisPhase.New(mainconfig));

                //add client phases
                string value;
                Phx.Phases.PhaseConfiguration clientConfig = 
                    Phx.Phases.PhaseConfiguration.New(lifetime, "Client phases");
                if (!PurityAnalysisPhase.properties.TryGetValue("disableallclients", out value)
                    || !Boolean.Parse(value))
                {
                    clientConfig.PhaseList.AppendPhase(PurityReportGenerationPhase.New(clientConfig));
                    //clientConfig.PhaseList.AppendPhase(LINQPurityPhase.New(
                    //    clientConfig, MethodLevelAnalysis.MethodsPassedToLinqQueries));                
                    //clientConfig.PhaseList.AppendPhase(DependenceAnalysisPhase.New(clientConfig));
                }

                Phx.Phases.PhaseConfiguration finalizeConfig
                        = Phx.Phases.PhaseConfiguration.New(lifetime, "DiscardPhase");
                finalizeConfig.PhaseList.AppendPhase(Phx.PE.DiscardIRPhase.New(finalizeConfig));
                
                var moduleUnit = programUnit.ModuleUnitList;
                while (moduleUnit != null)
                {
                    //TODO: automatically discover dll dependencies
                    moduleUnit.LoadGlobalSymbols();
                    moduleUnit.LoadPdb();
                    
                    dllnames.Add(moduleUnit.Manifest.Name.NameString);
                    
                    mainconfig.PhaseList.DoPhaseList(moduleUnit);

                    if (PurityAnalysisPhase.analysistype == "")
                    {
                        clientConfig.PhaseList.DoPhase(moduleUnit);
                    }

                    finalizeConfig.PhaseList.DoPhase(moduleUnit);
                                                         
                    // The dll containing the main method is expected to be passed in last
                    if (moduleUnit.Next == null && PurityAnalysisPhase.IsFramework == false)
                    {
                        Stopwatch sw = new Stopwatch();
                        sw.Start();                        
                        Console.WriteLine("Beginning top down phase");

                        var p = new PurityAnalysisPhase();

                        // Trigger the top down phase beginning at main
                        p.AnalyzeTopDown(moduleUnit);

                        if(PurityAnalysisPhase.DumpPointsToGraphs)
                            p.PrintDataFlowFacts();
                        
                        // Construct the CSG and populate the node to thread mapping
                        CSG programCSG = new CSG();
                        AliasQueries.seen = new HashSet<Pair<WholeCGNode, WholeCGNode>>();
                        var interestingPairs = AliasQueries.GetAliasingPairs();
                        var modeledPairs = programCSG.GetModeledPairs().Distinct();
                        //Console.WriteLine("Interesting Pairs: ");
                        //foreach (var val in interestingPairs)
                        //{
                        //    Console.WriteLine("Block Interesting" + val.blockingNode);
                        //    Console.WriteLine("Signal Interesting" + val.signalingNode);
                        //    //Console.WriteLine("Block " + val.blockingNode);
                        //}
                        //Console.WriteLine("Modeled Pairs: ");
                        //foreach(var val in modeledPairs)
                        //{
                        //    Console.WriteLine("Block " + val.blockingNode);
                        //    Console.WriteLine("Signal " + val.signalingNode);
                        //    //Console.WriteLine("Block " + val.blockingNode);
                        //}
                        var mainVertex = programCSG.Vertices.Where(x => x.Name.Contains("::Main") || x.Name.Contains("::main")).First();
                        Console.WriteLine("\n ******************************* ");
                        Console.WriteLine("Printing interesting paths: ");
                        Console.WriteLine("\n ******************************* ");
                        int total = 0;
                        var allPairs = new List<AliasingPair>(interestingPairs.Union(modeledPairs));
                        foreach (var ws in allPairs)
                        {                           
                            var signalVertices = programCSG.GetFinalStatesFor(ws.signalingNode);
                            var path = new List<CSGEdgeBase>();
                            bool flag = false;
                            Console.WriteLine("Considering Blocking vertex {0}", ws.blockingNode);
                            foreach (var signalVertex in signalVertices)
                            {
                                var curr = new Pair<WholeCGNode, WholeCGNode>(ws.blockingNode, signalVertex);
                                if (AliasQueries.seen.Contains(curr))
                                    continue;
                                var sources = programCSG.GetSources(signalVertex);
                                Console.WriteLine("Considering Signaling vertex {0}", signalVertex);
                                var nodeToThreadMap = PurityAnalysisPhase.FilterConfigured ? programCSG.GetNodeToThreadMappingAsync() : programCSG.GetNodeToThreadMappingSync();
                                    if (programCSG.existsInterestingPath(mainVertex, signalVertex, path, nodeToThreadMap, ws.signalingContext, sources))
                                {
                                    Console.WriteLine("%%%%%%%%%%%%%%%%%%%%");
                                    Console.WriteLine("Printing blocking vertex: ");
                                    Console.WriteLine(ws.blockingNode);

                                    Console.WriteLine("Printing signaling vertex: ");
                                    Console.WriteLine(ws.signalingNode);

                                    Console.WriteLine("Printing signaling path: ");
                                    foreach (var e in path)
                                        Console.WriteLine(e.Target);
                                    Console.WriteLine("%%%%%%%%%%%%%%%%%%%%");
                                    flag = true;
                                    AliasQueries.seen.Add(curr);
                                    total++;
                                    break;
                                }                                
                            }
                            if (flag == false)
                            {
                                Console.WriteLine("No interesting path for " + ws.signalingNode);
                                Console.WriteLine("Printing final states");
                                foreach (var v in signalVertices)
                                    Console.WriteLine(v);
                            }
                        }
                        var allStates = new HashSet<Purity.Summaries.WholeCGNode>(programCSG.moveNextTracker.SelectMany(x => x.Value));                        
                        var fishy = allStates.Except(programCSG.moveNextTracker.Keys).Where(v => !programCSG.InEdges(v).OfType<CallBackEdge>().Any());

                        Console.WriteLine("Printing fishy vertices");
                        foreach(var vertex in fishy)
                        {
                            Console.WriteLine(vertex);
                        }
                        Console.WriteLine("#Reports = {0}", total);
                        programCSG.DumpAsDGML("csg.dgml");

                        Console.WriteLine("# Memory {0} KB", (System.Diagnostics.Process.GetCurrentProcess().PeakVirtualMemorySize64 / 1000));

                        sw.Stop();
                        Console.WriteLine("Top down phase took: " + sw.ElapsedMilliseconds + "ms");

                    }


                    //Cleanup resources attached to the module unit
                    //moduleUnit.CallGraph.SummaryManager.Delete();
                    //TypeUtil.CombinedTypeHierarchy.ReleaseModuleUnit(moduleUnit);

                    moduleUnit = moduleUnit.Next;
                    
                }

                // Postponed clean-up
                moduleUnit = programUnit.ModuleUnitList;
                while (moduleUnit != null)
                {
                    moduleUnit.CallGraph.SummaryManager.Delete();
                    TypeUtil.CombinedTypeHierarchy.ReleaseModuleUnit(moduleUnit);

                    moduleUnit = moduleUnit.Next;
                }

                    //dump the wholeprogram CG here                         
                    if (PurityAnalysisPhase.properties.TryGetValue("dumpwholeprogramcallgraph", out value)
                    && Boolean.Parse(value))
                {
                    if (!PurityAnalysisPhase.properties.TryGetValue("wholeprogramcallgraphfilename", out value))
                        throw new ArgumentException("unable to dump wholeprogramcallgraph: no filename specified");

                    var textFile = PurityAnalysisPhase.outputdir + value + ".txt";
                    var dgmlFile = PurityAnalysisPhase.outputdir + value + ".dgml";
                 
                    if (File.Exists(textFile))
                        File.Delete(textFile);
                    Purity.Summaries.CalleeSummaryReader.wholecg.DumpToText(new StreamWriter(File.OpenWrite(textFile)));

                    //foreach(var edge in Purity.Summaries.CalleeSummaryReader.wholecg.Edges)
                    //{
                    //    Console.WriteLine("Edge {0} --({1})--> {2}", edge.Source.Name, edge.callId, edge.Target.Name);
                    //}

                    GraphUtil.DumpAsDGML<Purity.Summaries.WholeCGNode, Purity.Summaries.WholeCGEdge>(
                        dgmlFile,
                        Purity.Summaries.CalleeSummaryReader.wholecg,
                        (Purity.Summaries.WholeCGNode node) => ((int)node.id),
                        (Purity.Summaries.WholeCGNode node) => (node.Name),
                        (Purity.Summaries.WholeCGEdge edge) => ((edge.call == null)? "Modeled Call" : edge.call.instructionContext + "::" + edge.call.directCallInstrID + "::" + edge.call.resolvingCallInstrID + "::" + edge.resolvingCallerID),
                        null, null);

                    var cg = Purity.Summaries.CalleeSummaryReader.wholecg;
                    var dfs = new DepthFirstSearchAlgorithm<Purity.Summaries.WholeCGNode, Purity.Summaries.WholeCGEdge>(cg);
                    Purity.Summaries.WholeCGNode root = cg.Vertices.Where(x => x.Name.Contains("::Main") || x.Name.Contains("::main")).First();
                    int vertexCount = 0;
                    int edgeCount = 0;
                    dfs.DiscoverVertex += (x) => { vertexCount++; edgeCount += cg.OutDegree(x); /*Console.WriteLine(x.Name);*/ };
                    dfs.Compute(root);
                    Console.WriteLine("Reached " + vertexCount + " methods, and " + edgeCount + " edges ");
                }


                var processed = MethodLevelAnalysis.methodToPurityData;
                //var processed = PurityAnalysisPhase.methodToPurit;

                //Console.WriteLine("\n==================Dumping call instruction ids=========================\n");
                //foreach (var entry in AnalysisUtil.idToCall)
                //{
                //    Console.Write(entry.Key + " --> ");
                //    Console.WriteLine(entry.Value);
                //}
                //Console.WriteLine("===========================================");

                if (PurityAnalysisPhase.properties.TryGetValue("dumppuritydata", out value)
                    && Boolean.Parse(value))
                {
                    if (!PurityAnalysisPhase.properties.TryGetValue("puritydatafilename", out value))
                        throw new ArgumentException("unable to dump puritydata: no filename specified");

                    var datFile = value + ".dat";

                    if (File.Exists(datFile))
                        File.Delete(datFile);

                    IFormatter formatter = new BinaryFormatter();
                    using (FileStream fs = File.OpenWrite(datFile))
                    {
                        formatter.Serialize(fs, processed.Count);
                        foreach (var pair in processed)
                        {
                            formatter.Serialize(fs, pair.Key);
                            formatter.Serialize(fs, pair.Value);
                        }
                    }
                }

                IFormatter f = new BinaryFormatter();
                var outputFile = "interestingVariables" + ".bin";
                if (File.Exists(outputFile))
                    File.Delete(outputFile);
                if (InitializationPhase.Interesting.Count != 0)
                {
                    Stream stream = new FileStream(outputFile, FileMode.Append, FileAccess.Write, FileShare.None);
                    f.Serialize(stream, InitializationPhase.Interesting);
                    stream.Close();
                }

                return Phx.Term.Mode.Normal;                
            }            
            finally
            {
                //clear db contents on termination
                if (PurityAnalysisPhase.clearDBContents)
                    ClearDBContents(dllnames);
            }
        }

        private void ClearDBContents(IEnumerable<string> dllnames)
        {
            Console.WriteLine("Cleaning up serialized summaries...");
            var dbcontext = PurityAnalysisPhase.DataContext;
            var newThEntries = from entry in dbcontext.TypeHierarchies
                               where dllnames.Contains(entry.dllname)
                               select entry;
            dbcontext.TypeHierarchies.DeleteAllOnSubmit(newThEntries);

            var newTypeEntries = from entry in dbcontext.TypeInfos
                                 where dllnames.Contains(entry.dllname)
                                 select entry;
            dbcontext.TypeInfos.DeleteAllOnSubmit(newTypeEntries);

            var newMethodEntries = from entry in dbcontext.MethodInfos
                                   where dllnames.Contains(entry.dllname)
                                   select entry;
            dbcontext.MethodInfos.DeleteAllOnSubmit(newMethodEntries);

            var newSummaryEntries = from entry in dbcontext.puritysummaries
                                    where dllnames.Contains(entry.dllname)
                                    select entry;
            dbcontext.puritysummaries.DeleteAllOnSubmit(newSummaryEntries);
            try
            {
                dbcontext.SubmitChanges();
            }
            catch (System.Data.SqlClient.SqlException sqlE)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Exception occurred on deleting entries: " + sqlE.Message);
                Console.ResetColor();
            }
            finally
            {
                dbcontext.Dispose();
            }
        }                                                            

        static int Main(string[] arguments)
        {
            //for (int i = 0; i < arguments.Length; i++)
            //    System.Console.WriteLine(arguments[i]);
            Console.WriteLine(sealHome);
            // Do static initialization of the Phoenix framework.            
            Checker.StaticInitialize(arguments);

            //read and intialize input
            //Phx.Output.WriteLine("Processing " + Checker.input.GetValue(null) + " ...");
            var inputs = Checker.input.GetValue(null);            

            //initialize analyze list from the console.
            string configfilename = Checker.configfile.GetValue(null);
            //if (!String.IsNullOrEmpty(configfilename))
            //{
            //    Console.WriteLine("Config filename: " + configfilename);
            //}

            string outdirname = Checker.outdirname.GetValue(null);
            
            if (String.IsNullOrEmpty(outdirname))
                PurityAnalysisPhase.outputdir = ".\\";
            else            
                PurityAnalysisPhase.outputdir = outdirname + "\\";

            string isFramework = Checker.IsFramework.GetValue(null);
            Console.WriteLine("isframework " + isFramework);
            if (String.IsNullOrEmpty(isFramework))
                PurityAnalysisPhase.IsFramework = false;
            else
                PurityAnalysisPhase.IsFramework = true;
            Console.WriteLine("Is Framework value " + PurityAnalysisPhase.IsFramework);
          
            PurityAnalysisPhase.analysistype = Checker.analysistype.GetValue(null).ToLower();

            //load config file
            InitializeProperties();

            if (PurityAnalysisPhase.analysistype == "sourcesinkanalysis")
            {
                SafetyAnalysis.Util.SourceSinkUtil.sourceFileName = sourcefile.GetValue(null).ToLower();
                bool isn = int.TryParse(sourceline.GetValue(null), out SafetyAnalysis.Util.SourceSinkUtil.sourceLineNumber);
                SafetyAnalysis.Util.SourceSinkUtil.sinkFileName = sinkfile.GetValue(null).ToLower();
                isn = int.TryParse(sinkline.GetValue(null), out SafetyAnalysis.Util.SourceSinkUtil.sinkLineNumber);
                SafetyAnalysis.Util.SourceSinkUtil.checkingFunction = checkingFunction.GetValue(null);
                SourceSinkUtil.generateEdgeLabels();

                /*
                SafetyAnalysis.Util.SourceSinkUtil.hashFile = sealHome + "\\Hash.txt";

                var dllstream = File.OpenRead(input.GetValue(null).Head.Data);

                SourceSinkUtil.hashNew = BitConverter.ToString(MD5.Create().ComputeHash(dllstream));
                if (PurityAnalysisPhase.EnableConsoleLogging) Console.WriteLine("Hash : "+ SourceSinkUtil.hashNew);

                if (SourceSinkUtil.hashFileExists())
                {
                    SourceSinkUtil.readHashFile();
                    if (SourceSinkUtil.hashNew == SourceSinkUtil.hashStored)
                    {
                        if (PurityAnalysisPhase.EnableConsoleLogging) Console.WriteLine("Hash Matched");
                        PurityAnalysisPhase.firstTime = false;
                    }
                }*/
            }

            else if (PurityAnalysisPhase.analysistype == "castanalysis")
            {
                
                TypeUtil.TypeCastUtil.castFileName = castfile.GetValue(null).ToLower();
                bool isn = int.TryParse(castline.GetValue(null), out TypeUtil.TypeCastUtil.castLineNumber);
                TypeUtil.TypeCastUtil.analyzingFunction = checkingFunction.GetValue(null);
                TypeUtil.TypeCastUtil.setCastEdgeLabel();
                
            }

            else if (PurityAnalysisPhase.analysistype == "")
            {
                PurityAnalysisPhase.firstTime = true;
            }

            Stopwatch sw = new Stopwatch();
            sw.Start();
            var termMode = (new Checker()).ProcessModules(inputs);
            sw.Stop();
            Console.WriteLine("Checker took: " + sw.ElapsedMilliseconds + "ms");
            if (PurityAnalysisPhase.analysistype == "sourcesinkanalysis")
            {
                string finalOutput;
                if (!SafetyAnalysis.Util.SourceSinkUtil.sourcefound)
                {
                    if (SafetyAnalysis.Util.SourceSinkUtil.error1 != "")
                    {
                        finalOutput = SafetyAnalysis.Util.SourceSinkUtil.error1;
                    }
                    else
                    {
                        finalOutput = "Source Not Found";
                    }
                }
                else if (!SafetyAnalysis.Util.SourceSinkUtil.sinkfound)
                {
                    if (SafetyAnalysis.Util.SourceSinkUtil.error2 != "")
                    {
                        finalOutput = SafetyAnalysis.Util.SourceSinkUtil.error2;
                    }
                    else
                    {
                        finalOutput = "Sink Not Found";
                    }
                }
                else if (SafetyAnalysis.Util.SourceSinkUtil.answer != "")
                {
                    finalOutput = SafetyAnalysis.Util.SourceSinkUtil.answer;
                }
                else if (SafetyAnalysis.Util.SourceSinkUtil.answer == "")
                {
                    finalOutput = "Function Not Found";
                }
                else
                {
                    finalOutput = "Error (Bug) (Should Not Happen)";
                }

                Console.WriteLine(finalOutput);
            }
            else if (PurityAnalysisPhase.analysistype == "castanalysis")
            {
                string finalOutput;

                if (TypeUtil.TypeCastUtil.instructionFound == false)
                {
                    finalOutput = "Cast Instruction Not Found";
                }
                else if (TypeUtil.TypeCastUtil.answer == "")
                {
                    finalOutput = "Function Not Found";
                }
                else
                {
                    finalOutput = TypeUtil.TypeCastUtil.answer;
                }

                Console.WriteLine(finalOutput);
            }

            return (termMode == Phx.Term.Mode.Normal ? 0 : 1);
        }
    }    
}

