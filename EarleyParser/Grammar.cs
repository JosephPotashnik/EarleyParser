using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;

namespace EarleyParser
{
    /// <summary>
    /// Grammar object encapsulates a set of production rules (which may be Context Free or Linear Indexed).
    /// It supports the tentative addition and substraction of rules, as well as the possibility to accept/reject these changes.
    /// Grammar object allows public access to Rules dictionary, which is used by EarleyParser for prediction of new Earley Items.
    /// 
    /// Grammar object is abstract. Instantiate either ContextFreeGrammar or LinearIndexedGrammar (see their classes).
    /// 
    /// Note: The terminals corresponding to parts of speech (e.g. D -> 'the', A -> 'big') appear in a separate vocabulary.json file
    /// See CFGExample.txt and LIGExample.txt for examples of grammars.
    /// </summary>
    public abstract class Grammar
    {
        public const string GammaSymbol = "Gamma";
        public const string StartSymbol = "START";
        public const string EpsilonSymbol = "Epsilon";
        public const string StarSymbol = "*";
        public static HashSet<string> PartsOfSpeech;

        //in the member 'Rules' below, rules are organized into a dictionary with their Left Hand Side as key.
        //Those are Context Free. If Grammar is Linear Indexed, then multiple Context Free rules may be generated for one LIG rule.
        //Those are all CFG Rules reachable from START nonterminal. Rules that are in the grammar but not reachable are not included here.
        //Rules dictionary is accessed by EarleyParser in order to perform prediction.
        public readonly Dictionary<string, List<Rule>> Rules = [];

        //_schematicRules are all rules of the Grammar, whether they are reachable from START nonterminal or not.
        //_schematicRules do not have to be context free.
        protected HashSet<Rule> _schematicRules = [];
        //_LHSToSchematicRules indexes all rules of the grammar by their LHS side (modulo the stack, if the Rule is LIG).
        protected Dictionary<string, List<Rule>> _LHSToSchematicRules = [];

        protected virtual Dictionary<Rule, HashSet<Rule>> GetSchematicRuleToCFGRealizations() { return null; }
        protected virtual Rule GenerateCFGRule(Rule schematicRule, string leftHandSide) => throw new NotImplementedException();
        protected virtual HashSet<string> GetLHSNonterminalsOfCorrespondingCFG(Rule r) => throw new NotImplementedException();

        public int HighIndex { get; set; } //X1...X(HighIndex) nonterminals are in use.

        public Grammar(List<Rule> rules)
        {
            Construct(rules);
        }

        private void Construct(IEnumerable<Rule> rules)
        {
            GenerateRules(rules);
        }

        private List<string> AddToSchemaDictionary(IEnumerable<Rule> xy)
        {
            List<string> keys = [];
            foreach (var r in xy)
            {
                var newSynCat = r.LeftHandSide;
                if (!_LHSToSchematicRules.TryGetValue(newSynCat, out var rules))
                {
                    rules = [];
                    _LHSToSchematicRules.Add(newSynCat, rules);
                    keys.Add(newSynCat);
                }
                rules.Add(r);

                AddToSchematicTables(r);
            }
            return keys;
        }
        public List<Rule> GetSchematicRules() => _schematicRules.Select(x => new Rule(x)).ToList();
        public override string ToString()
        {

            var s = string.Join("\r\n", _schematicRules.Select(x => x.ToFormattedStackString()));
            s += "\r\n";
            s += "Count: " + _schematicRules.Count + "\r\n";
            return s;
        }

        private void AddCFGRule(Rule derivedRule, bool restoring = false)
        {
            if (derivedRule == null)
            {
                return;
            }

            if (!Rules.TryGetValue(derivedRule.LeftHandSide, out var rules))
            {
                rules = [];
                Rules.Add(derivedRule.LeftHandSide, rules);
            }
            rules.Add(derivedRule);
        }

        public void DFS(string currentCategory, HashSet<string> visited)
        {
            visited.Add(currentCategory);
            foreach (var rule in Rules[currentCategory])
            {
                foreach (var rhs in rule.RightHandSide)
                {
                    if (Rules.ContainsKey(rhs) && !visited.Contains(rhs))
                    {
                        DFS(rhs, visited);
                    }
                }
            }
        }

        private void InsertBFS(Queue<(Rule, string)> toVisit, HashSet<(Rule, string)> visited)
        {
            while (toVisit.Count > 0)
            {
                var tu = toVisit.Dequeue();
                var leftHandSideNonTerminal = tu.Item2;
                var rule = tu.Item1;
                var nonterminals = GetLHSNonterminalsOfCorrespondingCFG(rule);

                if (!nonterminals.Contains(leftHandSideNonTerminal))
                {
                    var derivedRule = GenerateCFGRule(rule, leftHandSideNonTerminal);
                    AddCFGRule(derivedRule);
                    if (derivedRule != null)
                    {
                        foreach (var rhs in derivedRule.RightHandSide)
                        {
                            _LHSToSchematicRules.TryGetValue(rhs, out var ruleList);
                            if (ruleList != null)
                            {
                                foreach (var ruleToPredict in ruleList)
                                {
                                    var nonterminals1 = GetLHSNonterminalsOfCorrespondingCFG(ruleToPredict);

                                    if (!visited.Contains((ruleToPredict, rhs)) && (!nonterminals1.Contains(rhs)))
                                    {
                                        visited.Add((ruleToPredict, rhs));
                                        toVisit.Enqueue((ruleToPredict, rhs));
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        //Insert new rule intends to insert a rule which does not change the contents of the stack,
        //i.e, either (i) a CFG rule such as X -> Y Z, or (ii) LIG rule such as X[*] -> Y Z[*]
        //the latter option corresponds to many possible CFG rules (X -> Y Z, X[Z] -> Y Z[Z]..)
        public void InsertNewRule(Rule r)
        {
            AddToSchemaDictionary(new List<Rule> { r });
            //generate basic rule(with an empty stack).
            var baseSyntacticCategory = r.LeftHandSide;

            var toVisit = new Queue<(Rule, string)>();
            var visited = new HashSet<(Rule, string)>();
            var nonterminals = GetLHSNonterminalsOfCorrespondingCFG(r);

            //if lhs is START, always add
            if (baseSyntacticCategory == StartSymbol)
            {
                //if (string.IsNullOrEmpty(r.LeftHandSide.Stack) || r.LeftHandSide.Stack == StarSymbol)
                {
                    var startCategory = StartSymbol;

                    if (!visited.Contains((r, startCategory)) && (nonterminals == null || !nonterminals.Contains(startCategory)))
                    {
                        visited.Add((r, startCategory));
                        toVisit.Enqueue((r, startCategory));
                    }
                }
            }


            //if the new Rule has different stack counterparts, generate the rule for the different stacks.
            //add to CFG RULES only if reachable.
            //needs to be further optimised. Instead of searching over RHS, keep data structure.
            foreach (var existingRuleList in Rules.Values)
            {
                foreach (var existingRule in existingRuleList)
                {
                    foreach (var rhs in existingRule.RightHandSide)
                    {
                        if (rhs == baseSyntacticCategory)
                        {
                            if (!visited.Contains((r, rhs)) && (!nonterminals.Contains(rhs)))
                            {
                                visited.Add((r, rhs));
                                toVisit.Enqueue((r, rhs));
                            }

                        }
                    }
                }
            }

            if (toVisit.Count > 0)
            {
                InsertBFS(toVisit, visited);
            }
        }

        protected void GenerateRules(IEnumerable<Rule> rules)
        {
            foreach (var r in rules)
            {
                InsertNewRule(r);
            }
        }

        protected virtual void AddToSchematicTables(Rule r) => _schematicRules.Add(r);
        public static Grammar CreateGrammar(List<Rule> rules)
        {
            var g = new ContextFreeGrammar(rules);
            return g;
        }

        public static int RenameVariables(List<Rule> rules, HashSet<string> partOfSpeechCategories)
        {

            var startNT = StartSymbol;
            var grammarNTs = new HashSet<string>();
            var replaceDic = new Dictionary<string, string>();

            bool encounteredStart = false;
            int runningIndex = 1;
            //get all nonterminals of rules.
            foreach (var r1 in rules)
            {
                if (r1.LeftHandSide.Equals(startNT))
                {
                    if (encounteredStart)
                    {
                        throw new Exception("grammar is in illegal format. Please use a START -> X rule exactly once");
                    }
                    else
                    {
                        encounteredStart = true;
                    }
                }
                else
                {
                    if (!replaceDic.ContainsKey(r1.LeftHandSide.ToString()))
                    {
                        replaceDic.Add(r1.LeftHandSide.ToString(), $"X{runningIndex++}");
                    }
                }

                foreach (var rhs in r1.RightHandSide)
                {
                    if (rhs.Equals(startNT))
                    {
                        throw new Exception("grammar is in illegal format. Please do not use START symbol on the right hand side of rules");
                    }

                    if (!partOfSpeechCategories.Contains(rhs.ToString()))
                    {
                        if (!replaceDic.ContainsKey(rhs.ToString()))
                        {
                            replaceDic.Add(rhs.ToString(), $"X{runningIndex++}");
                        }
                    }
                }
            }

            ReplaceVariables(replaceDic, rules);

            //var startCategory = new Nonterminal(Grammar.StartSymbol);
            //var startRulesToReplace = new List<Rule>();
            //foreach (var rule in rules)
            //{
            //    if (rule.RightHandSide.Length == 2)
            //        if (rule.LeftHandSide.Equals(startCategory) ||
            //            rule.RightHandSide[0].Equals(startCategory) ||
            //            rule.RightHandSide[1].Equals(startCategory))
            //            startRulesToReplace.Add(rule);

            //    //if (rule.RightHandSide.Length == 1)
            //    //{
            //    //    var baseCat = new Nonterminal(rule.RightHandSide[0]);
            //    //    if (partOfSpeechCategories.Contains(baseCat.ToString()))
            //    //        startRulesToReplace.Add(rule);
            //    //}
            //}

            //if (startRulesToReplace.Count > 0)
            //{
            //    replaceDic[Grammar.StartSymbol] = startRenamedVariable;
            //    ReplaceVariables(replaceDic, startRulesToReplace);
            //    var newStartRule = new Rule(Grammar.StartSymbol, new[] { startRenamedVariable });
            //    rules.Add(newStartRule);
            //}

            return replaceDic.Count;
        }


        public static void ReplaceVariables(Dictionary<string, string> replaceDic, IEnumerable<Rule> rules)
        {
            foreach (var rule in rules)
            {
                if (replaceDic.TryGetValue(rule.LeftHandSide, out var value))
                {
                    rule.LeftHandSide = value;
                }

                for (var i = 0; i < rule.RightHandSide.Length; i++)
                {
                    if (replaceDic.TryGetValue(rule.RightHandSide[i], out var value1))
                    {
                        rule.RightHandSide[i] = value1;
                    }
                }
            }
        }


        public static void CreatePOSAssignmentRules(List<Rule> rules, HashSet<string> partOfSpeechCategories, int highIndex)
        {

            int nextAvailableIndex = highIndex + 1;
            Dictionary<string, string> containedPOS = [];
            foreach (var rule in rules)
            {
                foreach (var rhs in rule.RightHandSide)
                {
                    if (partOfSpeechCategories.Contains(rhs.ToString()))
                    {
                        if (!containedPOS.ContainsKey(rhs.ToString()))
                        {
                            string nextNT = $"X{nextAvailableIndex++}";
                            containedPOS[rhs.ToString()] = nextNT;

                        }
                    }
                }
            }

            ReplaceVariables(containedPOS, rules);

            foreach (var item in containedPOS)
            {
                var pos = item.Key;
                var NT = item.Value;

                var newRule = new Rule(NT, [pos]);
                rules.Add(newRule);
            }

        }

    }


}
