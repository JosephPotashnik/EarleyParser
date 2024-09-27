using System;
using System.Collections.Generic;

namespace EarleyParser
{
    /// <summary>
    /// The main class responsible for parsing. A chart parser with dynamic programming. See Earley 1970, Stolcke 1995
    /// It holds the current Grammar and an array of Earley Sets (Earley Columns).
    /// each Set/Column in the chart table corresponds to an index of the input.
    /// The parser can also be run as a generator, to generate all possible trees of a grammar (up to input length n)
    /// </summary>
    public class EarleyParser
    {
        public Grammar Grammar;
        protected EarleyColumn[] _table;
        private readonly string[] _text;
        private readonly int _textLength;
        protected Vocabulary Voc;
        private const int MaximumCompletedStatesInColumn = 50000;
        public static Dictionary<string, Rule> ScannedRules;
        private List<(int, EarleyState)> _cachedScannedStates = new List<(int, EarleyState)> ();
        private EarleyState _startState;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="g"> CFG to be parsed.</param>
        /// <param name="v"> Vocabulary class listing all Part-Of-Speech -> token rules. </param>
        /// <param name="text"> The sentence the parser parses. Does not change throughout the lifespan of the parser. </param>
        /// <param name="treeDic"> ConcurrentDictionary accumulating the number of the trees in the parse forest. Key is sentence length.
        ///                        Useful for computing statistics and can be passed to several parsers. </param>
        /// <param name="maxWords"> length of sentence. used when the parser is run as a generator (call to GenerateSentence()). </param>               
        public EarleyParser(Grammar g, Vocabulary v, string[] text,  int maxWords = 0)
        {
            Voc = v;
            Grammar = g;
            _text = text;
            _textLength = _text.Length;
            _table = PrepareEarleyTable(_text, maxWords);
            PrepareScannedStates();
            var gammaGrammarRule = new Rule(Grammar.GammaSymbol, [Grammar.StartSymbol]);
            _startState = new EarleyState(gammaGrammarRule, 0, _table[0]);
            if (g != null)
            {
                _table[0].AddState(_startState, Grammar);
            }
        }
        public (bool, int) ParseSentence(Grammar g)
        {
            Reset(g);
            return ParseSentence();
        }

        private void Reset(Grammar g)
        {
            Grammar = g;
            for (var i = 0; i < _table.Length; i++)
            {
                _table[i].Reset();
            }

            foreach (var item in _cachedScannedStates)
            {
                _table[item.Item1].Reductors.AddState(item.Item2);
            }

            _table[0].AddState(_startState, Grammar);
        }

        /// <summary>
        /// the main loop, traversing first over completed Items (States) agenda, then over Predicted Item Agenda.
        /// Completed Items Agenda is a priority queue, ordered by decreasing order of Set Start index. See Stolcke 1995.
        /// Predicted Item agenda is a queue with nonterminals such that the nonterminal is the LHS of the list of rules to be predicted.
        /// Epsilon transition causes looping back to completion.
        /// </summary>
        /// <returns> (bool b, int i) such that b is true if parse was accepted. 
        /// Parsing is only rejected in case the numbers of completed Items in a Set exceeds a certain threshold (e.g, 10000)
        /// int i is the number of trees in the parse forest.
        /// /returns>
        public (bool, int) ParseSentence()
        {
            bool accepted = true;
            foreach (var col in _table)
            {
                var exhaustedCompletion = false;
                while (!exhaustedCompletion)
                {
                    TraverseCompletedStates(col);
                    TraversePredictableStates(col);
                    exhaustedCompletion = col.ActionableCompleteStates.Count == 0;
                }

                if (col.CompletedStateCount > MaximumCompletedStatesInColumn)
                {
                    accepted = false;
                    break;
                }
            }

            Cleanup(accepted);

            int length = 0;
            if (HasDerivation())
            {
                length = 1;
            }
            
            return (accepted, length);
        }

        /// <summary>
        /// Generates all possible trees for the grammar up to input length n. (specified in the constructor)
        /// EarleyGenerator.GetAllSequences() can later be called to get all bracketed representations, or all sequences of parts of speech.
        /// </summary>
        public void GenerateSentence()
        {

            foreach (var col in _table)
            {
                var exhaustedCompletion = false;
                while (!exhaustedCompletion)
                {
                    TraverseCompletedStates(col);
                    TraversePredictableStates(col);
                    exhaustedCompletion = col.ActionableCompleteStates.Count == 0;
                }

                int count = CountDerivationsOfLengthK(col.Index);

                if (count > MaximumCompletedStatesInColumn * 2)
                {
                    throw new TooManyEarleyItemsGeneratedException();
                }
            }
        }

        //K = col.Index, i.e., count derivations that span exactly input length k.
        private int CountDerivationsOfLengthK(int k)
        {
            var startCat = Grammar.StartSymbol;
            int count = 0;
            if (_table[0].Reductors.TryGetValue(startCat, out var val))
            {
                if (val.TryGetValue(k, out var reductor))
                {
                    if (reductor != null)
                    {
                        var visited = new Dictionary<EarleySpan, int>();
                        count = reductor.CountDerivations(visited);
                    }
                }
            }

            return count;
        }

        protected virtual EarleyColumn[] PrepareEarleyTable(string[] text, int maxWord)
        {
            var table = new EarleyColumn[text.Length + 1];
            for (var i = 1; i < table.Length; i++)
            {
                table[i] = new EarleyColumn(i, text[i - 1]);
            }

            table[0] = new EarleyColumn(0, "");
            return table;
        }

        private void TraversePredictableStates(EarleyColumn col)
        {
            while (col.ActionableNonTerminalsToPredict.Count > 0)
            {
                var nextTerm = col.ActionableNonTerminalsToPredict.Dequeue();
                Grammar.Rules.TryGetValue(nextTerm, out var ruleList);
                Predict(col, ruleList, nextTerm);
            }
        }

        private void TraverseCompletedStates(EarleyColumn col)
        {
            while (col.ActionableCompleteStates.Count > 0)
            {
                var state = col.ActionableCompleteStates.Dequeue();
                Complete(col, state);
            }
        }

        private void Predict(EarleyColumn col, List<Rule> ruleList, string nextTerm)
        {
            if (ruleList != null)
            {
                foreach (var rule in ruleList)
                {
                    if (!rule.IsLexical)
                    {
                        var newState = new EarleyState(rule, 0, col);
                        col.AddState(newState, Grammar);
                    }
                }
            }
        }

        private void Complete(EarleyColumn col, EarleyState reductorState)
        {
            var startColumn = reductorState.StartColumn;
            var completedSyntacticCategory = reductorState.Rule.LeftHandSide;

            var (reductorSpan, localAmbiguityFound) = startColumn.Reductors.AddState(reductorState);

            //if we already inserted a state/Item with the same LHS and same span, there is a parent pointer which is
            //already connected to the set of ambiguous states (the span).
            if (localAmbiguityFound)
            {
                return;
            }


            //create the consequent of the predecessor and the span according to the complete deduction rule.
            if (startColumn.Predecessors.TryGetValue(completedSyntacticCategory, out var predecessorStates))
            {
                foreach (var predecessor in predecessorStates)
                {
                    var newState = new EarleyState(predecessor.Rule, predecessor.DotIndex + 1, predecessor.StartColumn);
                    newState.Predecessor = predecessor;
                    newState.ReductorSpan = reductorSpan;
                    col.AddState(newState, Grammar);
                }
            }
        }

        public EarleySpan GetCompletedStartNonterminal(int columnIndex = 0)
        {
            var startCat = Grammar.StartSymbol;
            if (_table[0].Reductors.TryGetValue(startCat, out var val))
            {
                if (val.TryGetValue(_table.Length - 1 - columnIndex, out var res))
                {
                    return res;
                }
            }
            return null;

        }

        public string[] GetFormattedString(int columnIndex = 0, bool onlyPartsOfSpeechSequences = false)
        {
            string[] sls = [];

            var reductorSpan = GetCompletedStartNonterminal(columnIndex);
            if (reductorSpan != null)
            {
                var visited = new Dictionary<EarleySpan, Color>();
                var sbs = reductorSpan.GetFormattedString(Grammar, visited, onlyPartsOfSpeechSequences);
                sls = new string[sbs.Count];

                for (int i = 0; i < sls.Length; i++)
                {
                    sls[i] = sbs[i].ToString();
                }
            }
            return sls;
        }

        public bool HasDerivation()
        {
            return (GetCompletedStartNonterminal() != null);
        }

        public int CountDerivations()
        {
            int count = 0;
            var reductor = GetCompletedStartNonterminal();
            if (reductor != null)
            {
                var visited = new Dictionary<EarleySpan, int>();
                count = reductor.CountDerivations(visited);
            }
            return count;
        }


        private void Cleanup(bool accepted)
        {
            if (!accepted)
            {
                foreach (var col in _table)
                {
                    col.ActionableCompleteStates.Clear();
                    col.ActionableNonTerminalsToPredict.Clear();
                }
            }
        }

        protected virtual HashSet<string> GetPossibleSyntacticCategoriesForToken(string nextScannableTerm)
        {
            return Voc[nextScannableTerm];
        }


        private void PrepareScannedStates()
        {
            var lexicalizedRules = VerifyLexicalizedRules();
            string nextScannableTerm, nextNextScannableTerm;
            HashSet<string> possibleNonTerminalsOFNextScannableTerm = null;
            HashSet<string> possibleNonTerminalsOFNextNextScannableTerm = null;
            //generate completed PART-OF-SPEECH -> 'token' for each token in the sentence.

            for (int i = 0; i < _table.Length - 1; i++)
            {
                if (i == 0)
                {
                    nextScannableTerm = _table[i + 1].Token;
                    possibleNonTerminalsOFNextScannableTerm = GetPossibleSyntacticCategoriesForToken(nextScannableTerm);
                }
                else
                {
                    possibleNonTerminalsOFNextScannableTerm = possibleNonTerminalsOFNextNextScannableTerm;
                }

                if (i < _table.Length - 2)
                {
                    nextNextScannableTerm = _table[i + 2].Token;
                    possibleNonTerminalsOFNextNextScannableTerm = GetPossibleSyntacticCategoriesForToken(nextNextScannableTerm);
                }


                if (possibleNonTerminalsOFNextScannableTerm != null)
                {
                    foreach (var nonTerminal in possibleNonTerminalsOFNextScannableTerm)
                    {
                        var scannedStateRule = ScannedRules[nonTerminal];
                        var scannedState = new EarleyState(scannedStateRule, 1, _table[i]);

                        scannedState.EndColumn = _table[i + 1];
                        _table[i].Reductors.AddState(scannedState);
                        _cachedScannedStates.Add((i, scannedState));
                    }
                }
            }

            //after all part of speech categories were inserted, process lexicalized rules of the grammar, if there are any 
            for (int i = 0; i < _table.Length - 1; i++)
            {
                AddLexicalizedRules(lexicalizedRules, i);
            }
        }

        private List<Rule> VerifyLexicalizedRules()
        {
            List<Rule> lexicalizedRules = [];
            if (Grammar != null)
            { 
                foreach (var listOfRules in Grammar.Rules.Values)
                {
                    foreach (var rule in listOfRules)
                    {
                        if (rule.IsLexical)
                        {
                            lexicalizedRules.Add(rule);
                        }
                    }
                }
            }
            return lexicalizedRules;
        }

        protected virtual void AddLexicalizedRules(List<Rule> lexicalRules, int i, bool preprocess = true)
        {
            //generate State/Item corresponding to a Lexicalized Rule if the tokens of the rules are matched in the input.
            foreach (var rule in lexicalRules)
            {
                int dotIndexOfLexicalTokens = 0;
                Rule ruleWithoutSingleQuotes = null;
                bool match = MatchLexicalRuleWithNextLexicalTokens(rule, i, out dotIndexOfLexicalTokens, out ruleWithoutSingleQuotes);

                if (match)
                {
                    if (!preprocess || dotIndexOfLexicalTokens != ruleWithoutSingleQuotes.RightHandSide.Length)
                    {
                        var scannedState = new EarleyState(ruleWithoutSingleQuotes, dotIndexOfLexicalTokens, _table[i]);
                        _table[i + dotIndexOfLexicalTokens].AddState(scannedState, Grammar);
                    }
                    else
                    {
                        if (ruleWithoutSingleQuotes.RightHandSide.Length == 1)
                        {
                            var possibleNonTerminals = GetPossibleSyntacticCategoriesForToken(ruleWithoutSingleQuotes.RightHandSide[0].ToString());
                            if (possibleNonTerminals != null && possibleNonTerminals.Contains(rule.LeftHandSide.ToString()))
                            {
                                throw new Exception("lexical rule already exist in the Part-of-Speech -> 'token' Vocabulary");
                            }
                        }

                        var scannedState = new EarleyState(ruleWithoutSingleQuotes, dotIndexOfLexicalTokens, _table[i]);
                        scannedState.EndColumn = _table[i + dotIndexOfLexicalTokens];
                        _table[i].Reductors.AddState(scannedState);
                    }

                }
            }
        }

        bool MatchLexicalRuleWithNextLexicalTokens(Rule rule, int i, out int dotIndexOfLexicalTokens, out Rule ruleWithoutSingleQuotes)
        {
            bool foundNonTerminal = false;
            dotIndexOfLexicalTokens = 0;
            bool match = true;
            ruleWithoutSingleQuotes = new Rule(rule);

            for (int k = 0; k < rule.RightHandSide.Length; k++)
            {
                if (i + k + 1 >= _table.Length)
                {
                    match = false;
                    break;
                }

                var s = rule.RightHandSide[k].ToString();
                if (s[0] == '\'' && s[s.Length - 1] == '\'')
                {
                    var token = s.Substring(1, s.Length - 2);
                    var nextTermToCheck = _table[i + k + 1].Token;
                    if (token == nextTermToCheck)
                    {
                        ruleWithoutSingleQuotes.RightHandSide[k] = token;
                    }
                    else
                    {
                        match = false;
                        break;
                    }

                    dotIndexOfLexicalTokens++;
                    if (foundNonTerminal)
                    {
                        throw new Exception("lexical Rule is of the wrong format. lexical rule may begin with any prefix of lexical tokens. tokens cannot follow non-terminals on the right hand side (e.g, no X -> Y 'token')");
                    }
                }
                else
                {
                    foundNonTerminal = true;
                    break;
                }
            }
            return match;
        }
    }
}
