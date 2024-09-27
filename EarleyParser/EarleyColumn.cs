using System.Collections.Generic;

namespace EarleyParser
{
    /// <summary>
    /// This class implements the Earley Set (interchangeable with the term Earley Column). It represents a single
    /// column in the Parser Chart, corresponding to a certain input index.
    /// 
    /// The class encapsulates the following:
    /// (i) Dictionaries:
    ///     Predecessors dictionary, holding all noncompleted Items. Predicted dictionary is a subset of Predecessors, indexed by LHS key.
    ///     Reductors dictionary, holding all completed Items in present or further Columns with a given key/span. 
    /// The two above dictionaries make Spontaneous Dot Shift trivial. Please see Finite Difference Paper for discussion.
    /// 
    /// (ii) Agendas for traversing actionable Items/States.
    ///     ActionableCompleteStates for completed states, and ActionableNonTerminalsToPredict for predicted states.
    ///     ActionableDeletedStates for uncompleted states, and NonTerminalsCandidatesToUnpredict for unpredicted states.
    ///     
    /// (iii) Data structures to keep track of last reparse(s) actions.
    ///       Reparses can be either accepted or rejected. These data Structures are used to revert or affirm the changes.
    ///     
    ///  EarleyColumn implements 
    ///  AddState - decides how to handle the added State/Item 
    ///         If it not completed, add to predecessor dictionary,
    ///         Otherwise, push to the completed States Agenda (in order to preserve BFS order over the parse forest).

    /// </summary>
    public class EarleyColumn
    {
        //Input index in the chart
        internal int Index { get; }
        //the lexical token associated with the Column/Set.
        internal string Token { get; set; }

        //(i) Dictionaries
        internal Dictionary<string, List<EarleyState>> Predecessors { get; }
        internal EarleySpans Reductors { get; } = new EarleySpans();

        //(ii) Agendas
        internal CompletedStatesHeap ActionableCompleteStates { get; set; }
        internal Queue<string> ActionableNonTerminalsToPredict { get; }
        internal int CompletedStateCount { get; private set; }

        internal EarleyColumn(int index, string token)
        {
            Index = index;
            Token = token;
            //completed agenda is ordered in decreasing order of start indices (see Stolcke 1995 about completion priority queue).
            ActionableCompleteStates = new CompletedStatesHeap();
            ActionableNonTerminalsToPredict = new Queue<string>();
            Predecessors = [];
            Reductors = new EarleySpans();

            //TODO: re-enable epsilon states.
            //CreateEpsilonState();
        }

        public void Reset()
        {
            Predecessors.Clear();
            Reductors.Clear();
            ActionableCompleteStates.Clear();
            ActionableNonTerminalsToPredict.Clear();
            CompletedStateCount = 0;
        }

        private void CreateEpsilonState()
        {
            var epsilonCat = Grammar.EpsilonSymbol;
            var epsilonRule = new Rule(epsilonCat.ToString(), [""]);
            var epsilonCompletedState = new EarleyState(epsilonRule, 1, this);
            epsilonCompletedState.EndColumn = this;
            Reductors.AddState(epsilonCompletedState);
        }


        internal void AddState(EarleyState newState, Grammar grammar)
        {
            newState.EndColumn = this;

            if (!newState.IsCompleted)
            {
                var term = newState.NextTerm;

                //when WildcardPOSPrediction, the assumption is that the grammar never contains POS rules of the form
                //Xi -> POS or Xi -> POS POS, i.e, the right hand side are always nonterminals. 
                //the dictionary WildCardPOSDic in EarleyParser populates the entries according to input.
                bool addTermToPredict = grammar.Rules.ContainsKey(term);

                if (!Predecessors.TryGetValue(term, out var predecessors))
                {
                    predecessors = [];
                    Predecessors.Add(term, predecessors);
                }
                else
                {
                    addTermToPredict = false;
                }
                if (addTermToPredict)
                {
                    ActionableNonTerminalsToPredict.Enqueue(term);
                }

                predecessors.Add(newState);

                if (Reductors.TryGetValue(term, out var reductorsWithSpans))
                {
                    reductorsWithSpans.SpontaneousDotShift(newState, grammar);
                }
            }
            else
            {
                CompletedStateCount++;
                ActionableCompleteStates.Enqueue(newState);
            }
        }
    }
}
