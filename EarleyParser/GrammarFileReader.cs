using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace EarleyParser
{
    /// <summary>
    /// This class generates list of Context Free or Context Sensitive Rules from a text file.
    /// see CFGExample.txt and LIGExample.txt for example grammars.
    /// </summary>
    public partial class GrammarFileReader
    {
        public static List<Rule> ReadRulesFromFile(string filename, bool isTest = false)
        {
            string line;
            var comment = '#';
            var dir = Directory.GetCurrentDirectory();

            if (!isTest)
            {
                filename = Path.Combine([".", "InputData", "ContextFreeGrammars", filename]);
            }
            else
            {
                filename = Path.Combine([".", "InputData", "TestGrammars", filename]);
            }

            var rules = new List<Rule>();
            using (var file = File.OpenText(filename))
            {
                while ((line = file.ReadLine()) != null)
                {

                    if (line[0] == comment)
                    {
                        continue;
                    }

                    int found = line.IndexOf(". ");
                    if (found >= 0)
                    {
                        line = line.Substring(found + 2);
                    }

                    var r = CreateRule(line);
                    if (r != null)
                    {
                        rules.Add(r);
                    }
                }
            }

            return rules;
        }

        public static Rule CreateRule(string s)
        {
            var removeArrow = s.Replace("->", "");
            //string formatted incorrectly. (no "->" found).
            if (s == removeArrow)
            {
                return null;
            }

            var nonTerminals = removeArrow.Split().Select(p => p.Trim()).Where(p => !string.IsNullOrWhiteSpace(p))
                .ToArray();

            var leftHandCat = nonTerminals[0];
            var rightHandCategories = new string[nonTerminals.Length - 1];
            for (var i = 1; i < nonTerminals.Length; i++)
            {
                rightHandCategories[i - 1] = nonTerminals[i];
            }
            return new Rule(leftHandCat, rightHandCategories);
        }
    }
}
