using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace EarleyParser
{

    /// <summary>
    /// This class implements a production rule, which is of the form: Left Hand Side -> Right Hand Side
    /// Left hand side can be a single nonterminal, right hand side can be a sequence of nonterminals. 
    /// see class description of Nonterminal for more details about the nonterminal type.
    /// (Right hand side with length 0 expands to the empty string, i.e. an epsilon rule)
    /// </summary>
    public class Rule : IEquatable<Rule>
    {
        public string LeftHandSide { get; set; }
        public string[] RightHandSide { get; set; }
        [JsonIgnore]
        public bool IsLexical { get; set; }

        public Rule(string leftHandSide, string[] rightHandSide)
        {
            LeftHandSide = leftHandSide;
            if (rightHandSide != null)
            {
                var length = rightHandSide.Length;
                RightHandSide = new string[length];
                for (var i = 0; i < length; i++)
                {
                    RightHandSide[i] = rightHandSide[i];
                }
            }
        }

        public Rule(Rule otherRule)
        {
            LeftHandSide = otherRule.LeftHandSide;
            if (otherRule.RightHandSide != null)
            {
                var length = otherRule.RightHandSide.Length;
                RightHandSide = new string[length];
                for (var i = 0; i < length; i++)
                {
                    RightHandSide[i] = otherRule.RightHandSide[i];
                }
            }

            IsLexical = otherRule.IsLexical;
        }


        public string ToFormattedStackString()
        {
            var p = RightHandSide.Select(x => x).ToArray();
            return $"{LeftHandSide} -> {string.Join(" ", p)}";
        }


        public bool ExamineRuleForLexicalizedTokens()
        {
            bool foundLexicalRule = false;
            bool foundNonTerminal = false;
            for (int i = 0; i < RightHandSide.Length; i++)
            {
                var s = RightHandSide[i].ToString();
                if (s[0] == '\'' && s[s.Length - 1] == '\'')
                {
                    foundLexicalRule = true;

                    if (foundNonTerminal)
                    {
                        throw new Exception("lexical Rule is of the wrong format. lexical rule may begin with any prefix of lexical tokens. tokens cannot follow non-terminals on the right hand side (e.g, no X -> Y 'token')");
                    }
                }
                else
                {
                    foundNonTerminal = true;
                }
            }

            IsLexical = foundLexicalRule;

            return foundLexicalRule;
        }

        public override string ToString()
        {
            //var p = RightHandSide.Select(x => x.ToString()).ToArray();
            //return $"{NumberOfGeneratingRule}. {LeftHandSide} -> {string.Join(" ", p)}";
            var p = RightHandSide.Select(x => x).ToArray();
            return $"{LeftHandSide} -> {string.Join(" ", p)}";
        }

        public bool Equals(Rule other)
        {
            if (!LeftHandSide.Equals(other.LeftHandSide))
            {
                return false;
            }

            if (RightHandSide.Length != other.RightHandSide.Length)
            {
                return false;
            }

            for (var i = 0; i < RightHandSide.Length; i++)
            {
                if (!RightHandSide[i].Equals(other.RightHandSide[i]))
                {
                    return false;
                }
            }

            return true;
        }
        public override int GetHashCode()
        {
            unchecked
            {
                var hash = 17;
                hash = hash * 23 + LeftHandSide.GetHashCode();
                for (var i = 0; i < RightHandSide.Length; i++)
                    hash = hash * 23 + RightHandSide[i].GetHashCode();

                return hash;
            }

        }

        public override bool Equals(object obj) => Equals(obj as Rule);
    }
}
