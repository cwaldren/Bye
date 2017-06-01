using System;
using System.Collections.Generic;
using System.Linq;
using FuzzyString;

namespace bye
{

    class Program
    {
        interface Matcher
        {
            bool Match(InstalledProgram program, SearchTerm term);
        }

        class RankingPair : IComparable
        {
            public InstalledProgram Program;
            public int LevenDistance;

            public RankingPair(InstalledProgram p, int levenDistance)
            {
                Program = p;
                LevenDistance = levenDistance;
            }

            public int CompareTo(object other)
            {
                return LevenDistance.CompareTo(((RankingPair)other).LevenDistance);
            }
        }

        class SearchTerm
        {
            private string originalTerm;
            private string[] normalizedTokens;
            private string fusedTerm;

            public string Original
            {
                get { return originalTerm; }
            }

            public string[] Tokens
            {
                get { return normalizedTokens; }
            }

            public string Fused
            {
                get { return fusedTerm; }
            }

            public string Normalized
            {
                get { return Normalize(originalTerm); }
            }

            

            public SearchTerm(string query)
            {
                originalTerm = query;
                string normalized = Normalize(query);

                normalizedTokens = normalized.Split(' ');
                fusedTerm = normalized.Replace(" ", string.Empty);

            }

            private string Normalize(string query)
            {
                return query.ToLower();
            }


        }

        class FuzzyMatcher : Matcher
        {
            private List<FuzzyStringComparisonOptions> options;

            public FuzzyMatcher()
            {
                options = new List<FuzzyStringComparisonOptions>();
                options.Add(FuzzyStringComparisonOptions.UseLongestCommonSubstring);
            }

            public bool Match(InstalledProgram program, SearchTerm term)
            {
                var programNameTokens = program.DisplayName
                    .Trim()
                    .Split(' ')
                    .Where(result => result.Length > 0)
                    .Select(raw => raw.ToLower());

                foreach (var token in programNameTokens)
                {
                    if (token.ApproximatelyEquals(term.Original, options, FuzzyStringComparisonTolerance.Strong)
                      ||token.ApproximatelyEquals(term.Fused, options, FuzzyStringComparisonTolerance.Strong))
                    {
                        return true;
                    }

                }

                return false;

            }
        }

        static void PrintUsage()
        {
            Console.WriteLine("Usage: bye [progam-name]\nEx: bye chrome\n");
            Console.ReadLine();
        }

        static void Main(string[] args)
        {
            
            if (args.Length < 1)
            {
                PrintUsage();
                return;
            }

            string searchTerm = args[0];

            if (searchTerm.Length == 0)
            {
                PrintUsage();
                return;
            }

            SearchTerm search = new SearchTerm(searchTerm);
            
            var programs = InstalledProgram.GetInstalledPrograms(false);

            HashSet<InstalledProgram> candidates = new HashSet<InstalledProgram>();


            Matcher matcher = new FuzzyMatcher();


            foreach (var program in programs)
            {
                if (matcher.Match(program, search))
                {
                    candidates.Add(program);
                }
            }

            var candidateList = candidates.ToList();
          
            if (candidateList.Count == 1)
            {
                var uninstallCandidate = candidateList.Single();
                Console.WriteLine("Uninstall " + uninstallCandidate.DisplayName + "?");
                if (Console.ReadLine().Contains("y"))
                {
                    Uninstall(uninstallCandidate);
                }
                return;
            }
            else if (candidateList.Count > 1)
            {
                var levens = new List<RankingPair>(candidateList.Count);
                foreach (var candidate in candidateList)
                {
                    var parts = candidate.DisplayName.Trim().Split(' ').Where(raw => raw.Length > 0).ToArray();
                  
                    int partsCountHeuristic = Math.Abs(search.Tokens.Length - parts.Count());

                    //int lowestDist = int.MaxValue;
                    //for (int i = 0; i < parts.Length; i++)
                    //{
                    //    lowestDist = Math.Min(lowestDist, parts[i].LevenshteinDistance(search.Original));
                    //}

                    int lowestDist = parts.Min(part => part.LevenshteinDistance(search.Original));
                    levens.Add(new RankingPair(candidate, lowestDist + partsCountHeuristic));
                }

                levens.Sort();
                Console.WriteLine("Which?");

                for (int i = 0; i < levens.Count; i++)
                {
                    Console.WriteLine(string.Format("[{0}] {1}", i, levens[i].Program.DisplayName));
                }
                int result = 0;
                if(int.TryParse(Console.ReadLine(), out result))
                {
                    Uninstall(levens[result].Program);
                    
                }else
                {
                    Console.WriteLine("Write a number next time, please!");
                }
            } else
            {
                Console.WriteLine("Nothing found. Bye.");
            }
        }

        private static void Uninstall(InstalledProgram program)
        {
            if (program.Guid != null && program.Guid.Length > 0)
            {
                UninstallFromGuid(program.Guid);
            } else if (program.UninstallString.Length > 0)
            {
                UninstallFromUninstaller(program.UninstallString);
            } else
            {
                Console.WriteLine("I'm sorry, I couldn't figure out how to uninstall it");
            }
          
        }

        private static void UninstallFromUninstaller(string uninstallString)
        {

            System.Diagnostics.Process.Start(uninstallString);

            Console.WriteLine("..bye");
        

        }

        private static void UninstallFromGuid(string guid)
        {
            System.Diagnostics.Process process = new System.Diagnostics.Process();
            System.Diagnostics.ProcessStartInfo startInfo = new System.Diagnostics.ProcessStartInfo();
            startInfo.WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden;
            startInfo.FileName = "msiexec.exe";
            startInfo.Arguments = "/x " + guid + " /promptrestart";
          //  Console.WriteLine("msiexec.exe with args: " + startInfo.Arguments);

            process.StartInfo = startInfo;
            process.Start();
            process.WaitForExit();
            Console.WriteLine("..bye");
        }
    }
}
