using System;
using System.Collections.Generic;

namespace EarleyParser
{
    /// <summary>
    /// Implements Context Free Grammar - a list of context free production Rules.
    /// It supports the tentative addition and substraction of rules, as well as the possibility to accept/reject these changes.
    /// 
    /// Note: The terminals corresponding to parts of speech (e.g. D -> 'the', A -> 'big') appear in a separate vocabulary.json file
    /// See CFGExample.txt for an example. Many other example grammar appear also in the unit test project. 
    /// </summary>
    public class ContextFreeGrammar : Grammar
    {
        protected override HashSet<string> GetLHSNonterminalsOfCorrespondingCFG(Rule r)
        {
            var nonterminals = new HashSet<string>();

            if (Rules.TryGetValue(r.LeftHandSide, out var rules))
            {
                if (rules.Contains(r))
                {
                    nonterminals.Add(r.LeftHandSide);
                }
            }
            return nonterminals;
        }
        protected override Rule GenerateCFGRule(Rule schematicRule, string leftHandSide) => schematicRule;
        public ContextFreeGrammar(List<Rule> rules) : base(rules) { }
    }
}
