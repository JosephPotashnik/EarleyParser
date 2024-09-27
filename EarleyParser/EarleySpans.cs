using System.Collections.Generic;
using System.Text;

namespace EarleyParser
{
    /// <summary>
    /// this class is responsible of holding all ambiguous completed states with the same given LHS and same span.
    /// it contains a list of Reductor States/Items (completed Items).
    /// </summary>
    public class EarleySpan 
    {
        public EarleySpan(EarleyState state)
        {
            Reductors = [];
            StartColumn = state.StartColumn;
            EndColumn = state.EndColumn;
            LeftHandSide = state.Rule.LeftHandSide;
        }

        public string LeftHandSide { get; set; }
        public EarleyColumn StartColumn { get; }
        public EarleyColumn EndColumn { get; set; }
        public List<EarleyState> Reductors { get; }
        public void Add(EarleyState state) => Reductors.Add(state);

        //this function returns the total number of strings rooted at this Span
        //strings can be either:
        //(i) bracketed representation of the subtree, e.g. (START (NP (PN John)) (VP (V0 cried)))
        //(ii) parts of speech sequences of the subtree, e.g. PN V0 (PN = proper noun).
        public List<StringBuilder> GetFormattedString(Grammar g, Dictionary<EarleySpan, Color> visited, bool onlyPartsOfSpeechSequences = false)
        {
            //the visited dictionary is aimed at detecting cycles in the parse forest 
            //(which may happen with grammars containing unit productions, eg. NP -> NP2, NP2 -> NP, NP -> D N)
            if (!visited.ContainsKey(this))
            {
                visited.Add(this, Color.Gray);
            }

            List<StringBuilder> containedWrapperSBS = [];
            foreach (var reductor in Reductors)
            {
                var containedSBS = reductor.GetFormattedString(g, visited, onlyPartsOfSpeechSequences);
                foreach (var sb in containedSBS)
                {
                    if (!onlyPartsOfSpeechSequences)
                    {
                        sb.Insert(0, " ");
                        sb.Insert(0, LeftHandSide);
                        sb.Insert(0, "(");
                        sb.Append(')');
                    }
                    containedWrapperSBS.Add(sb);
                }
            }
            visited[this] = Color.Black;
            return containedWrapperSBS;
        }


        //this function counts the total number of subtrees rooted at this Span.
        public int CountDerivations(Dictionary<EarleySpan, int> visited)
        {
            int totalDerivations = 0;

            if (!visited.ContainsKey(this))
            {
                visited.Add(this, 0); //0 derivations means GRAY color - span is being processed but its descendants are still explored in the DFS.
            }

            foreach (var reductor in Reductors)
            {
                int count = reductor.CountDerivations(visited);
                totalDerivations += count;
            }
            visited[this] = totalDerivations; //after all descendants are processed, store the count. count != 0 means BLACK color.

            return totalDerivations;
        }

    }

    /// <summary>
    /// this class manages a dictionary of completed states of same given LHS and all ranges of spans.
    /// </summary>
    internal class EarleySpansOfCompletedCategory 
    {
        internal Dictionary<int, EarleySpan> _reductorsWithSpanDic = [];
       
        //Spontaneous dot shift creates new consequent States/Items for a given predecessor and all possible existing reductors.
        public void SpontaneousDotShift(EarleyState state, Grammar grammar)
        {
            foreach (var reductorWithspan in _reductorsWithSpanDic.Values)
            {
                {
                    var newState = new EarleyState(state.Rule, state.DotIndex + 1, state.StartColumn);
                    newState.Predecessor = state;
                    newState.ReductorSpan = reductorWithspan;
                    reductorWithspan.EndColumn.AddState(newState, grammar);
                }
            }
        }

        public bool TryGetValue(int span, out EarleySpan _val) => _reductorsWithSpanDic.TryGetValue(span, out _val);
        public void Add(int span, EarleySpan val) => _reductorsWithSpanDic.Add(span, val);

    }

    /// <summary>
    /// this class manages a dictionary of completed nonterminals, and all their span objects
    /// each nonterminal may have several spans (of length 0,1,2,..etc). Each Span may have ambiguous Reductor States/Items.
    /// </summary>
    internal class EarleySpans 
    {
        internal Dictionary<string, EarleySpansOfCompletedCategory> _reductorDic = [];

        //This function adds a new reductor to an appropriate Span object.
        //If the span object already exists, the function reports that it encountered local ambiguity.

        public void Clear()
        {
            _reductorDic.Clear();
        }
        public (EarleySpan, bool) AddState(EarleyState reductor)
        {
            bool localAmbguityFound = false;


            var completedCat = reductor.Rule.LeftHandSide;

            if (!_reductorDic.TryGetValue(completedCat, out var reductorsDictPerLength))
            {
                reductorsDictPerLength = new EarleySpansOfCompletedCategory();
                _reductorDic.Add(completedCat, reductorsDictPerLength);
            }

            int span = reductor.EndColumn.Index - reductor.StartColumn.Index;

            if (!reductorsDictPerLength.TryGetValue(span, out var reductorsInLength))
            {
                reductorsInLength = new EarleySpan(reductor);
                reductorsDictPerLength.Add(span, reductorsInLength);
            }
            else
            {
                //same span with same category - local ambiguity.
                localAmbguityFound = true;
            }

            reductorsInLength.Add(reductor);

            return (reductorsInLength, localAmbguityFound);
        }

        public bool TryGetValue(string completedCat, out EarleySpansOfCompletedCategory _innerDic)
        {
            return _reductorDic.TryGetValue(completedCat, out _innerDic);
        }
    }

}
