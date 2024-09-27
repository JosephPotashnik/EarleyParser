using System;
using System.Collections.Generic;
using System.Text;

namespace EarleyParser
{
    //Color is used to detect cycles when visiting completed States (Gray to black transition).
    public enum Color
    {
        White,
        Gray,
        Black
    }
    /// <summary>
    /// This class implements Earley Item / Earley State (Item and State are interchangeable terms).
    /// It contains :
    /// (i) the Rule, the position of the dot, reference to its Start and Earley Set (Earley Column)
    /// (ii) pointers to its antecedents (children): reductorSpan (span with all ambiguous reductors) and predecesssor, if any.
    /// (iii) pointers to its consequents (parents).
    /// (iv) pointer to the Span object if the State is completed. (the Span contains all ambiguous states spanning same input)
    /// (v) boolean fields of Added/Removed. They are used to keep track of the latest reparse(s) changes.
    /// </summary>
    /// 
    
    public class EarleyState : IEquatable<EarleyState>
    {
        //(i) basic members:
        public Rule Rule { get; }
        public EarleyColumn StartColumn { get; }
        public EarleyColumn EndColumn { get; set; }
        public int DotIndex { get; }
        //(ii) antecedents (children) pointers:
        public EarleyState Predecessor { get; set; }
        public EarleySpan ReductorSpan { get; set; }
        public bool IsCompleted => DotIndex >= Rule.RightHandSide.Length;
        //Nonterminal Following the dot:
        public string NextTerm => IsCompleted ? null : Rule.RightHandSide[DotIndex];
        public EarleyState(Rule r, int dotIndex, EarleyColumn c)
        {
            Rule = r;
            DotIndex = dotIndex;
            StartColumn = c;
            EndColumn = null;
        }
        public bool Equals(EarleyState other) => this == other;

        //this function counts the total number of subtrees rooted at this State/Item.
        public int CountDerivations(Dictionary<EarleySpan, int> visited)
        {

            int derviationsFromReductorSpan = 1;
            int derivationsFromPredecessor = 0;
            int totalDerivations = 0;

            //reductor
            if (ReductorSpan != null)
            {
                //derivationsFromReductor = 0;
                //note: cycles are counted zero times (discarded)
                if (visited.TryGetValue(ReductorSpan, out derviationsFromReductorSpan))
                {
                    if (derviationsFromReductorSpan == 0)
                    {
                        //means GRAY color - a cycle occurred.
                        //leave the number of derivations 0. - do not count cycles.
                    }
                }

                derviationsFromReductorSpan = ReductorSpan.CountDerivations(visited);
            }

            //predecessor
            if (DotIndex > 1)
            {
                if (Predecessor != null)
                {
                    derivationsFromPredecessor = Predecessor.CountDerivations(visited);
                }
            }
            if (derivationsFromPredecessor > 0)
            {
                totalDerivations = derivationsFromPredecessor * derviationsFromReductorSpan;
            }
            else
            {
                totalDerivations = derviationsFromReductorSpan;
            }

            return totalDerivations;
        }

        //this function returns the total number of strings rooted at this State/Item
        //strings can be either:
        //(i) bracketed representation of the subtree, e.g. (START (NP (PN John)) (VP (V0 cried)))
        //(ii) parts of speech sequences of the subtree, e.g. PN V0 (PN = proper noun).
        public List<StringBuilder> GetFormattedString(Grammar g, Dictionary<EarleySpan, Color> visited, bool onlyPartsOfSpeechSequence = false)
        {
            List<StringBuilder> containedSBSReductor = null;
            List<StringBuilder> containedSBSPredecessor = null;
            List<StringBuilder> combinedSBS = containedSBSReductor;


            if (ReductorSpan != null)
            {
                //discard cycles. see documentation in CountDerivations()
                if (visited.TryGetValue(ReductorSpan, out var color))
                {
                    if (color == Color.Gray)
                    {
                        return [];
                    }
                }

                containedSBSReductor = ReductorSpan.GetFormattedString(g, visited, onlyPartsOfSpeechSequence);
            }
            else
            {
                StringBuilder sb = new StringBuilder();

                if (onlyPartsOfSpeechSequence)
                {
                    if (Grammar.PartsOfSpeech.Contains(Rule.LeftHandSide))
                    {
                        sb.Append($"{Rule.LeftHandSide}");
                    }
                }
                else
                {
                    for (int i = 0; i < Rule.RightHandSide.Length; i++)
                    {
                        //(Reductor == null) and nonterminal in right hand side can only occur when 
                        //the rule is a lexical rule specifying the token directly, with subsequent nonterminals,
                        //.e.g. the rule has the form X -> 'token' Y.
                        //in this case, ignore printing the nonterminal lexically - it will be expanded from its proper
                        //place in the parse tree.

                        //note: when asked to print only part of speech sequences, (onlyPartsOfSpeechSequence = true)
                        //lexicalized rules will be ignored, because their lexical tokens bypass parts of speech layer,
                        //the lexical token is forcibly encoded into the rule.
                        if (g.Rules.ContainsKey(Rule.RightHandSide[i]) || (Grammar.PartsOfSpeech.Contains(Rule.RightHandSide[i])))
                        {
                            continue;
                        }

                        if (i > 0)
                        {
                            sb.Append(' ');
                        }

                        sb.Append($"{Rule.RightHandSide[i]}");
                    }
                }

                containedSBSReductor = [sb];
            }

            //predecessor
            if (Predecessor != null && Predecessor.ReductorSpan != null)
            {
                containedSBSPredecessor = Predecessor.GetFormattedString(g, visited, onlyPartsOfSpeechSequence);
            }

            StringBuilder sb3;
            if (containedSBSPredecessor != null)
            {
                //if (containedSBSPredecessor.Count > 1)
                {
                    combinedSBS = [];
                    foreach (var sb1 in containedSBSPredecessor)
                    {
                        foreach (var sb2 in containedSBSReductor)
                        {
                            sb3 = new StringBuilder();
                            sb3.Append(sb1);
                            if (sb2.Length != 0)
                            {
                                sb3.Append(' ');
                            }

                            sb3.Append(sb2);
                            combinedSBS.Add(sb3);
                        }
                    }
                }
                //else
                //{
                //    combinedSBS = containedSBSReductor;
                //    var sb1 = containedSBSPredecessor[0];


                //    foreach (var sb2 in combinedSBS)
                //    {
                //        if (sb2.Length != 0)
                //            sb2.Insert(0, " ");
                //        sb2.Insert(0, sb1);
                //    }
                //}             
            }
            else
            {
                combinedSBS = containedSBSReductor;
            }

            return combinedSBS;
        }

        //print dotted rule
        private static string RuleWithDotNotation(Rule rule, int dotIndex)
        {
            string[] s = new string[rule.RightHandSide.Length + 2];

            s[0] = rule.LeftHandSide.ToString() + " -> ";
            s[1 + dotIndex] = "$";
            for (int i = 0; i < dotIndex; i++)
            {
                s[1 + i] = rule.RightHandSide[i].ToString();
            }

            for (int i = dotIndex; i < rule.RightHandSide.Length; i++)
            {
                s[2 + i] = rule.RightHandSide[i].ToString();
            }

            return string.Join(" ", s);
        }

        //print the basic members of the State/Item: the dotted rule and the span indices.
        public override string ToString()
        {
            var endColumnIndex = "None";
            if (EndColumn != null)
            {
                endColumnIndex = EndColumn.Index.ToString();
            }

            return string.Format("{0} [{1}-{2}]", RuleWithDotNotation(Rule, DotIndex),
                StartColumn.Index, endColumnIndex);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = 17;
                hash = hash * 23 + Rule.GetHashCode();
                hash = hash * 23 + DotIndex;
                hash = hash * 23 + StartColumn.Index;
                return hash;
            }
        }
        public override bool Equals(object obj) => Equals(obj as EarleyState);
    }
}
